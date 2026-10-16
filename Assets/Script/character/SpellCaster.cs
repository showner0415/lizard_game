using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * TAIL-CONSUMING SPELLCASTING - SUMMARY
 *
 * Reservation lifecycle
 *   TryBeginCast(n)  -> reserves n tails. Fails => the cast does not start. Nothing is visible yet.
 *   CompleteCast()   -> reservation.Confirm(): the tail's visuals are disabled and its regeneration timer starts.
 *   TryConsumeNow(n) -> reserves n tails and confirms them immediately. Used by channeling's per-second cost.
 *                       Charge casts still consume only in CompleteCast.
 *   CancelCast()     -> reservation.Release(): the tail is untouched and immediately available again.
 *                       Also runs when this component is disabled/destroyed mid-cast.
 *   TailManager auto-releases any reservation that is never confirmed/released (timeout safety net).
 *
 * Tail state machine (owned by Tail)
 *   Available -> Reserved -> Consumed -> Regenerating -> Available
 *                   |                                       ^
 *                   +--------- Release() -------------------+ (Reserved -> Available)
 *   Consumed is momentary: Confirm() enters it and moves to Regenerating inside the same call.
 *   Confirm/Release outside Reserved, or with a stale token, are no-ops that warn and return false.
 *   ForceRegenerate() is valid only while Regenerating.
 *
 * Time semantics
 *   Everything uses scaled time (Time.time / WaitForSeconds): pausing with Time.timeScale = 0 pauses casts,
 *   regeneration and reservation timeouts together. Regeneration and timeout deadlines are absolute
 *   timestamps, so disabling/re-enabling a component never loses them.
 *
 * Observer wiring without a direct dependency
 *   SpellCaster references only ITailProvider / ITailReservation (resolved once in Awake). TailManager
 *   implements ITailProvider, observes each Tail's StateChanged event (subscribed in OnEnable, unsubscribed in
 *   OnDisable) and republishes it as ITailProvider.AvailabilityChanged for UI/AI. SpellCaster itself publishes
 *   OnSpellCastStarted / Completed / Cancelled.
 */

/// <summary>
/// Cast timing and tail-reservation transaction for one caster. Depends only on <see cref="ITailProvider"/>.
/// <para>
/// Two ways to drive it: hold-to-cast systems call <see cref="TryBeginCast"/> then <see cref="CompleteCast"/> /
/// <see cref="CancelCast"/>; fixed-duration casts call <see cref="TryBeginTimedCast"/>.
/// </para>
/// <para>
/// Provider wiring: assign any component implementing <see cref="ITailProvider"/> to <c>_tailProviderSource</c>
/// (Unity cannot serialize interfaces), or call <see cref="SetTailProvider"/> for manual/DI wiring. If the field is
/// empty, a provider on this object or its parents is used.
/// </para>
/// </summary>
public sealed class SpellCaster : MonoBehaviour
{
    [Header("Tail Provider")]
    [Tooltip("Component implementing ITailProvider (e.g. TailManager on the character root). Empty = search this object and parents.")]
    [SerializeField] private MonoBehaviour _tailProviderSource;

    [Header("Timing")]
    [Tooltip("Shortest accepted duration (seconds, scaled time) for timed casts.")]
    [SerializeField, Min(0.01f)] private float _minTimedCastDuration = 0.05f;

    private ITailProvider _tailProvider;
    private readonly List<ITailReservation> _reservations = new List<ITailReservation>();
    private Coroutine _timedCastRoutine;
    private bool _isCasting;
    private float _castStartTime;

    /// <summary>Raised when a cast begins (a tail is reserved but not yet consumed).</summary>
    public event Action<SpellCastEventArgs> OnSpellCastStarted;

    /// <summary>Raised once when a cast finishes and its tail has been consumed.</summary>
    public event Action<SpellCastEventArgs> OnSpellCastCompleted;

    /// <summary>Raised once when a cast is cancelled/interrupted and its reservation released.</summary>
    public event Action<SpellCastEventArgs> OnSpellCastCancelled;

    /// <summary>True between a successful begin and its complete/cancel.</summary>
    public bool IsCasting => _isCasting;

    /// <summary>Awake에서 찾은 꼬리 제공자. 아직 없으면 그 시점에 한 번 더 찾는다.</summary>
    public ITailProvider TailProvider
    {
        get
        {
            if (_tailProvider == null)
                ResolveProvider();

            return _tailProvider;
        }
    }

    private void Awake()
    {
        ResolveProvider();
    }

    private void OnDisable()
    {
        CancelCast();
    }

    private void OnValidate()
    {
        if (_tailProviderSource != null && !(_tailProviderSource is ITailProvider))
        {
            Debug.LogWarning($"[SpellCaster] '{_tailProviderSource.name}' does not implement ITailProvider.", this);
            _tailProviderSource = null;
        }
    }

    /// <summary>Manual/DI wiring alternative to the serialized source. Cancels any cast in progress.</summary>
    public void SetTailProvider(ITailProvider provider)
    {
        CancelCast();
        _tailProvider = provider;
    }

    /// <summary>꼬리 1개를 예약하고 시전을 시작한다. <see cref="TryBeginCast(int)"/>.</summary>
    public bool TryBeginCast()
    {
        return TryBeginCast(1);
    }

    /// <summary>
    /// 꼬리를 <paramref name="tailCount"/>개 예약하고, 호출자가 <see cref="CompleteCast"/> 또는 <see cref="CancelCast"/>로 끝내는 시전을 시작한다.
    /// 이미 시전 중이거나, 제공자가 없거나, 꼬리가 모자라면 false. 모자라면 이번에 잡은 예약은 전부 되돌린다.
    /// </summary>
    public bool TryBeginCast(int tailCount)
    {
        if (_isCasting)
            return false;

        if (tailCount < 0)
        {
            Debug.LogWarning($"[SpellCaster] tail cost {tailCount}는 0으로 맞춥니다.", this);
            tailCount = 0;
        }

        if (tailCount == 0)
        {
            _isCasting = true;
            _castStartTime = Time.time;
            OnSpellCastStarted?.Invoke(new SpellCastEventArgs(0f, false));
            return true;
        }

        if (_tailProvider == null)
            return false;

        for (int i = 0; i < tailCount; i++)
        {
            if (_tailProvider.TryReserveTail(out ITailReservation reservation) && reservation != null)
            {
                _reservations.Add(reservation);
                continue;
            }

            ReleaseReservations();
            return false;
        }

        _isCasting = true;
        _castStartTime = Time.time;
        OnSpellCastStarted?.Invoke(new SpellCastEventArgs(0f, false));
        return true;
    }

    /// <summary>
    /// 꼬리를 <paramref name="count"/>개 예약하고 바로 확정한다. 채널링이 초마다 쓰는 소모다.
    /// 하나라도 예약에 실패하면 이번에 잡은 것은 전부 되돌리고 false.
    /// </summary>
    public bool TryConsumeNow(int count)
    {
        if (_tailProvider == null || count < 1)
            return false;

        var held = new List<ITailReservation>(count);
        for (int i = 0; i < count; i++)
        {
            if (_tailProvider.TryReserveTail(out ITailReservation reservation) && reservation != null)
            {
                held.Add(reservation);
                continue;
            }

            for (int j = 0; j < held.Count; j++)
            {
                if (held[j] != null && held[j].IsActive)
                    held[j].Release();
            }

            return false;
        }

        bool confirmed = true;
        for (int i = 0; i < held.Count; i++)
        {
            if (held[i] == null || !held[i].Confirm())
                confirmed = false;
        }

        return confirmed;
    }

    /// <summary>
    /// Begins a cast that completes automatically after <paramref name="durationSeconds"/> (scaled time).
    /// Durations below the configured minimum, or non-finite, are clamped with a warning.
    /// </summary>
    public bool TryBeginTimedCast(float durationSeconds)
    {
        if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds < _minTimedCastDuration)
        {
            Debug.LogWarning($"[SpellCaster] Invalid cast duration ({durationSeconds}); using {_minTimedCastDuration}s.", this);
            durationSeconds = _minTimedCastDuration;
        }

        if (!TryBeginCast())
            return false;

        _timedCastRoutine = StartCoroutine(TimedCastRoutine(durationSeconds));
        return true;
    }

    /// <summary>
    /// Finishes the cast: confirms the reservation (tail consumed) and raises <see cref="OnSpellCastCompleted"/> once.
    /// Returns false if no cast is in progress.
    /// </summary>
    public bool CompleteCast()
    {
        if (!_isCasting)
            return false;

        List<ITailReservation> reservations = CopyReservations();
        float elapsed = Time.time - _castStartTime;
        EndCastState();

        bool consumed = true;
        for (int i = 0; i < reservations.Count; i++)
        {
            if (reservations[i] == null || !reservations[i].Confirm())
                consumed = false;
        }

        if (reservations.Count == 0)
            consumed = true;

        if (!consumed)
            Debug.LogWarning("[SpellCaster] Cast completed but its tail reservation could not be confirmed (expired or released).", this);

        OnSpellCastCompleted?.Invoke(new SpellCastEventArgs(elapsed, consumed));
        return true;
    }

    /// <summary>
    /// Aborts the cast: releases the reservation (tail untouched) and raises <see cref="OnSpellCastCancelled"/> once.
    /// Safe to call when not casting.
    /// </summary>
    public bool CancelCast()
    {
        if (!_isCasting)
            return false;

        List<ITailReservation> reservations = CopyReservations();
        float elapsed = Time.time - _castStartTime;
        EndCastState();

        for (int i = 0; i < reservations.Count; i++)
        {
            if (reservations[i] != null && reservations[i].IsActive)
                reservations[i].Release();
        }

        OnSpellCastCancelled?.Invoke(new SpellCastEventArgs(elapsed, false));
        return true;
    }

    private IEnumerator TimedCastRoutine(float durationSeconds)
    {
        yield return new WaitForSeconds(durationSeconds);
        _timedCastRoutine = null;
        CompleteCast();
    }

    /// <summary>Clears cast state before any event/confirm so handlers may safely start a new cast.</summary>
    private void EndCastState()
    {
        _isCasting = false;
        _reservations.Clear();

        if (_timedCastRoutine != null)
        {
            StopCoroutine(_timedCastRoutine);
            _timedCastRoutine = null;
        }
    }

    List<ITailReservation> CopyReservations()
    {
        return new List<ITailReservation>(_reservations);
    }

    void ReleaseReservations()
    {
        for (int i = 0; i < _reservations.Count; i++)
        {
            if (_reservations[i] != null && _reservations[i].IsActive)
                _reservations[i].Release();
        }

        _reservations.Clear();
    }

    private void ResolveProvider()
    {
        if (_tailProviderSource != null)
        {
            _tailProvider = _tailProviderSource as ITailProvider;
            if (_tailProvider == null)
                Debug.LogWarning($"[SpellCaster] '{_tailProviderSource.name}' does not implement ITailProvider.", this);
        }

        if (_tailProvider == null)
            _tailProvider = GetComponentInParent<ITailProvider>();

        if (_tailProvider == null)
            Debug.LogWarning("[SpellCaster] No ITailProvider resolved; casts requiring a tail cannot start.", this);
    }
}
