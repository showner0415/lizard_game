using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns a character's scale slots: discovery, ordering, sequential regrowth, loading and reservations.
/// This is the only class allowed to know concrete <see cref="ScaleSlot"/> instances; consumers use <see cref="IScaleMagazine"/>.
/// It knows nothing about players, inventory or mana, so enemies can share it.
/// <para>
/// <b>Order:</b> discovery order (Hierarchy order). Regrowth, loading and reservation all pick the front-most
/// matching slot.
/// </para>
/// <para>
/// <b>Regrowth:</b> one timer for the whole magazine. While any slot is Empty, every
/// <c>_regenTimePerSlot</c> seconds the front-most Empty slot becomes Grown. Only one slot grows at a time.
/// </para>
/// <para>
/// <b>Ownership:</b> slots under a nested GameObject that has its own <see cref="ScaleMagazine"/> are never claimed.
/// </para>
/// <para>
/// <b>enabled:</b> 끄면 재생 타이머만 멈춘다. 칸 집계, 장전, 예약, 해제는 enabled와 무관하게 동작한다.
/// </para>
/// <para>
/// <b>Time semantics:</b> scaled <see cref="Time.time"/>. The regrowth deadline is an absolute timestamp and is
/// re-armed in <c>OnEnable</c>, so disabling the component never loses it.
/// </para>
/// <para>
/// <b>Reservations never expire.</b> The owner (e.g. <see cref="ScaleSpender"/>) must confirm or release them;
/// it releases them itself in <c>OnDisable</c>.
/// </para>
/// Expected: exactly one <see cref="ScaleMagazine"/> per character (placed on the character root).
/// </summary>
public sealed class ScaleMagazine : MonoBehaviour, IScaleMagazine, IHomeRestorable, IDeathPenalty
{
    private const float MinRegenTimePerSlot = 0.05f;
    private const float DefaultRegenTimePerSlot = 0.6f;

    [Header("Regeneration")]
    [Tooltip("Seconds (scaled time) for one Empty slot to grow back. Slots regrow one at a time, front first.")]
    [SerializeField, Min(MinRegenTimePerSlot)] private float _regenTimePerSlot = DefaultRegenTimePerSlot;

    private readonly List<ScaleSlot> _slots = new List<ScaleSlot>();
    private readonly HashSet<ScaleSlot> _slotSet = new HashSet<ScaleSlot>();
    private readonly List<ScaleReservation> _activeReservations = new List<ScaleReservation>();
    private bool _isSubscribed;

    private bool _isRegenerating;
    private float _regenCompleteAtTime;
    private Coroutine _regenRoutine;
    private bool _regenRoutineRunning;
    private int _regenGeneration;

    /// <inheritdoc />
    public event Action SlotsChanged;

    /// <inheritdoc />
    public int Capacity => _slots.Count;

    /// <inheritdoc />
    public int LoadedCount => CountState(ScaleSlotState.Loaded);

    /// <inheritdoc />
    public int ReservedCount => CountState(ScaleSlotState.Reserved);

    /// <inheritdoc />
    public int GrownCount => CountState(ScaleSlotState.Grown);

    /// <inheritdoc />
    public int EmptyCount => CountState(ScaleSlotState.Empty);

    /// <inheritdoc />
    public ScaleData CurrentScale
    {
        get
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                ScaleSlot slot = _slots[i];
                if (slot == null)
                    continue;
                if (slot.State != ScaleSlotState.Loaded && slot.State != ScaleSlotState.Reserved)
                    continue;
                if (slot.Scale != null)
                    return slot.Scale;
            }

            return null;
        }
    }

    private void Awake()
    {
        if (float.IsNaN(_regenTimePerSlot) || float.IsInfinity(_regenTimePerSlot) || _regenTimePerSlot < MinRegenTimePerSlot)
        {
            float clamped = float.IsNaN(_regenTimePerSlot) || float.IsInfinity(_regenTimePerSlot) ? DefaultRegenTimePerSlot : MinRegenTimePerSlot;
            Debug.LogWarning($"[ScaleMagazine] Invalid _regenTimePerSlot ({_regenTimePerSlot}); using {clamped}s.", this);
            _regenTimePerSlot = clamped;
        }

        DiscoverScales();
    }

    private void OnEnable()
    {
        SubscribeAll();

        // Slots may have been emptied while unsubscribed; the deadline itself survives disabling.
        EnsureRegenerating();
    }

    private void OnDisable()
    {
        UnsubscribeAll();
        StopRegenRoutine();
    }

    private void OnValidate()
    {
        if (_regenTimePerSlot < MinRegenTimePerSlot) _regenTimePerSlot = MinRegenTimePerSlot;
    }

    /// <summary>Adds a runtime-created slot at the back. Returns false for null or already-registered slots.</summary>
    public bool RegisterScale(ScaleSlot slot)
    {
        if (slot == null)
        {
            Debug.LogWarning("[ScaleMagazine] RegisterScale called with null.", this);
            return false;
        }

        if (!_slotSet.Add(slot))
            return false;

        _slots.Add(slot);
        if (_isSubscribed)
            slot.StateChanged += HandleScaleStateChanged;

        SlotsChanged?.Invoke();
        EnsureRegenerating();
        return true;
    }

    /// <summary>Removes a slot. Returns false if it was not registered.</summary>
    public bool UnregisterScale(ScaleSlot slot)
    {
        if (slot == null || !_slotSet.Remove(slot))
            return false;

        _slots.Remove(slot);
        slot.StateChanged -= HandleScaleStateChanged;
        if (IndexOfFirst(ScaleSlotState.Empty) < 0)
            StopRegeneration();

        SlotsChanged?.Invoke();
        return true;
    }

    /// <inheritdoc />
    public bool TryReserveScale(out IScaleReservation reservation)
    {
        reservation = null;

        for (int i = 0; i < _slots.Count; i++)
        {
            ScaleSlot slot = _slots[i];
            if (slot == null || slot.State != ScaleSlotState.Loaded)
                continue;

            ScaleData scale = slot.Scale;
            if (!slot.TryReserve(out int token))
                continue;

            var created = new ScaleReservation(this, slot, token, scale);
            _activeReservations.Add(created);
            reservation = created;
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public int LoadGrown(ScaleData scale, int count)
    {
        if (scale == null)
        {
            Debug.LogWarning("[ScaleMagazine] LoadGrown called with null scale.", this);
            return 0;
        }

        if (count < 1)
            return 0;

        ScaleData current = CurrentScale;
        if (current != null && current != scale)
        {
            Debug.LogWarning($"[ScaleMagazine] LoadGrown rejected: magazine holds '{current.name}', not '{scale.name}'.", this);
            return 0;
        }

        int loaded = 0;
        for (int i = 0; i < _slots.Count && loaded < count; i++)
        {
            ScaleSlot slot = _slots[i];
            if (slot == null || slot.State != ScaleSlotState.Grown)
                continue;

            if (slot.TryLoad(scale))
                loaded++;
        }

        return loaded;
    }

    /// <inheritdoc />
    public int RestoreRegeneratingScales(int count)
    {
        if (count < 1)
            return 0;

        int restored = GrowEmpty(count);
        if (IndexOfFirst(ScaleSlotState.Empty) < 0)
            StopRegeneration();

        return restored;
    }

    /// <inheritdoc />
    public int UnloadAll(out ScaleData unloadedScale)
    {
        unloadedScale = null;
        int unloaded = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            ScaleSlot slot = _slots[i];
            if (slot == null || slot.State != ScaleSlotState.Loaded)
                continue;

            ScaleData held = slot.Scale;
            if (!slot.TryUnload())
                continue;

            if (unloadedScale == null)
                unloadedScale = held;
            else if (held != null && held != unloadedScale)
                Debug.LogWarning($"[ScaleMagazine] UnloadAll found '{held.name}' after '{unloadedScale.name}'. A magazine should hold one scale.", this);

            unloaded++;
        }

        if (unloaded == 0)
            unloadedScale = null;

        return unloaded;
    }

    /// <inheritdoc />
    public void RestoreForHome()
    {
        ReleaseActiveReservations();
        GrowEmpty(int.MaxValue);
        StopRegeneration();
    }

    /// <inheritdoc />
    public void ApplyDeathPenalty()
    {
        // 선딜 중인 칸은 Loaded가 아니라서, 먼저 예약을 풀어야 장전 해제에 걸린다.
        // 안 그러면 씬 이동 때 예약이 Loaded로 돌아가 거점까지 비늘이 남는다.
        ReleaseActiveReservations();
        UnloadAll(out _);
    }

    private void ReleaseActiveReservations()
    {
        var pending = new List<ScaleReservation>(_activeReservations);
        for (int i = 0; i < pending.Count; i++)
        {
            if (pending[i] != null && pending[i].IsActive)
                pending[i].Release();
        }
    }

    /// <summary>Grows up to <paramref name="count"/> Empty slots, front first.</summary>
    private int GrowEmpty(int count)
    {
        int grown = 0;
        for (int i = 0; i < _slots.Count && grown < count; i++)
        {
            ScaleSlot slot = _slots[i];
            if (slot == null || slot.State != ScaleSlotState.Empty)
                continue;

            if (slot.TryGrow())
                grown++;
        }

        return grown;
    }

    private int CountState(ScaleSlotState state)
    {
        int count = 0;
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i] != null && _slots[i].State == state) count++;
        return count;
    }

    private int IndexOfFirst(ScaleSlotState state)
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i] != null && _slots[i].State == state) return i;
        return -1;
    }

    /// <summary>Starts the regrowth timer if any slot is Empty and none is growing yet.</summary>
    private void EnsureRegenerating()
    {
        if (_isRegenerating)
        {
            if (!_regenRoutineRunning)
                StartRegenRoutine();
            return;
        }

        if (IndexOfFirst(ScaleSlotState.Empty) < 0)
            return;

        _isRegenerating = true;
        _regenCompleteAtTime = Time.time + _regenTimePerSlot;
        StartRegenRoutine();
    }

    private void StopRegeneration()
    {
        _isRegenerating = false;
        StopRegenRoutine();
    }

    private void StartRegenRoutine()
    {
        StopRegenRoutine();
        if (!isActiveAndEnabled)
            return;

        // The routine can finish synchronously inside StartCoroutine (deadline already passed),
        // so only keep the handle if this generation is still running afterwards.
        int generation = _regenGeneration;
        _regenRoutineRunning = true;
        Coroutine routine = StartCoroutine(RegenerationRoutine(generation));
        if (generation == _regenGeneration && _regenRoutineRunning)
            _regenRoutine = routine;
    }

    private void StopRegenRoutine()
    {
        // Bumping the generation makes an orphaned routine (one we could not hold a handle to) exit on its own.
        _regenGeneration++;
        _regenRoutineRunning = false;
        if (_regenRoutine == null)
            return;

        StopCoroutine(_regenRoutine);
        _regenRoutine = null;
    }

    private IEnumerator RegenerationRoutine(int generation)
    {
        while (_isRegenerating && generation == _regenGeneration)
        {
            float remaining = _regenCompleteAtTime - Time.time;
            if (remaining > 0f)
            {
                yield return new WaitForSeconds(remaining);
                continue;
            }

            GrowEmpty(1);
            if (generation != _regenGeneration)
                yield break;

            if (IndexOfFirst(ScaleSlotState.Empty) < 0)
            {
                _isRegenerating = false;
                break;
            }

            // Chain from the previous deadline, not from now, so a long disable catches up instead of drifting.
            _regenCompleteAtTime += _regenTimePerSlot;
        }

        if (generation != _regenGeneration)
            yield break;

        _regenRoutine = null;
        _regenRoutineRunning = false;
    }

    private void DiscoverScales()
    {
        ScaleSlot[] found = GetComponentsInChildren<ScaleSlot>(true);
        for (int i = 0; i < found.Length; i++)
        {
            ScaleSlot slot = found[i];
            if (slot == null || !IsOwnedByThisMagazine(slot))
                continue;

            if (_slotSet.Add(slot))
                _slots.Add(slot);
        }

        if (_slots.Count == 0)
            Debug.LogWarning("[ScaleMagazine] No scale slots discovered; spends requiring a scale will not start.", this);
    }

    /// <summary>Walks up from the slot; a different ScaleMagazine on the way means another character owns it.</summary>
    private bool IsOwnedByThisMagazine(ScaleSlot slot)
    {
        Transform current = slot.transform;
        while (current != null && current != transform)
        {
            if (current.TryGetComponent(out ScaleMagazine other) && other != this)
                return false;
            current = current.parent;
        }

        return true;
    }

    private void SubscribeAll()
    {
        if (_isSubscribed) return;
        _isSubscribed = true;

        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i] != null) _slots[i].StateChanged += HandleScaleStateChanged;
    }

    private void UnsubscribeAll()
    {
        if (!_isSubscribed) return;
        _isSubscribed = false;

        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i] != null) _slots[i].StateChanged -= HandleScaleStateChanged;
    }

    private void HandleScaleStateChanged(IScaleSlot slot, ScaleSlotState previous, ScaleSlotState next)
    {
        SlotsChanged?.Invoke();

        if (next == ScaleSlotState.Empty)
            EnsureRegenerating();
    }

    private void CloseReservation(ScaleReservation reservation)
    {
        _activeReservations.Remove(reservation);
    }

    /// <summary>Concrete reservation handle. Guarded by the slot's ownership token.</summary>
    private sealed class ScaleReservation : IScaleReservation
    {
        private readonly ScaleMagazine _owner;
        private readonly ScaleSlot _slot;
        private readonly int _token;

        public bool IsActive { get; private set; } = true;

        public ScaleData Scale { get; }

        public ScaleReservation(ScaleMagazine owner, ScaleSlot slot, int token, ScaleData scale)
        {
            _owner = owner;
            _slot = slot;
            _token = token;
            Scale = scale;
        }

        public bool Confirm()
        {
            if (!Deactivate("Confirm")) return false;
            return _slot != null && _slot.Confirm(_token);
        }

        public bool Release()
        {
            if (!Deactivate("Release")) return false;
            return _slot != null && _slot.Release(_token);
        }

        private bool Deactivate(string action)
        {
            if (!IsActive)
            {
                Debug.LogWarning($"[ScaleMagazine] {action} ignored: reservation is no longer active.");
                return false;
            }

            IsActive = false;
            if (_owner != null) _owner.CloseReservation(this);
            return true;
        }
    }
}
