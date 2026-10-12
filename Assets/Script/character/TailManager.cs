using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns a character's tails: discovery, ownership, selection and reservations.
/// This is the only class allowed to know concrete <see cref="Tail"/> instances; consumers use <see cref="ITailProvider"/>.
/// <para>
/// <b>Selection strategy:</b> the first <see cref="TailState.Available"/> tail in discovery/registration order
/// (deterministic).
/// </para>
/// <para>
/// <b>Ownership:</b> tails under a nested GameObject that has its own <see cref="TailManager"/> are never claimed.
/// </para>
/// <para>
/// <b>Time semantics:</b> scaled <see cref="Time.time"/>. The reservation timeout is an absolute deadline and is
/// re-armed in <c>OnEnable</c>.
/// </para>
/// Expected: exactly one <see cref="TailManager"/> per character (placed on the character root).
/// </summary>
public sealed class TailManager : MonoBehaviour, ITailProvider
{
    private const float MinReservationTimeout = 0.5f;

    [Header("Regeneration")]
    [Tooltip("Project-wide default regeneration delay (seconds, scaled time). Tails may override it individually.")]
    [SerializeField, Min(Tail.MinRegenerationDelay)] private float _defaultRegenerationDelay = 6f;

    [Header("Safety")]
    [Tooltip("An unconfirmed/unreleased reservation older than this is auto-released with a warning.")]
    [SerializeField, Min(MinReservationTimeout)] private float _reservationTimeoutSeconds = 15f;

    private readonly List<Tail> _tails = new List<Tail>();
    private readonly HashSet<Tail> _tailSet = new HashSet<Tail>();
    private readonly List<TailReservation> _activeReservations = new List<TailReservation>();
    private bool _isSubscribed;

    /// <inheritdoc />
    public event Action AvailabilityChanged;

    /// <inheritdoc />
    public int AvailableTailCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _tails.Count; i++)
                if (_tails[i] != null && _tails[i].IsAvailable) count++;
            return count;
        }
    }

    private void Awake()
    {
        _defaultRegenerationDelay = Tail.SanitizeDelay(_defaultRegenerationDelay, 6f, this, "_defaultRegenerationDelay");

        if (_reservationTimeoutSeconds < MinReservationTimeout || float.IsNaN(_reservationTimeoutSeconds) || float.IsInfinity(_reservationTimeoutSeconds))
        {
            Debug.LogWarning($"[TailManager] Invalid _reservationTimeoutSeconds ({_reservationTimeoutSeconds}); using {MinReservationTimeout}s.", this);
            _reservationTimeoutSeconds = MinReservationTimeout;
        }

        DiscoverTails();
    }

    private void OnEnable()
    {
        SubscribeAll();

        // Re-arm timeouts for reservations that were pending while this component was disabled.
        for (int i = _activeReservations.Count - 1; i >= 0; i--)
            StartTimeout(_activeReservations[i]);
    }

    private void OnDisable()
    {
        UnsubscribeAll();

        for (int i = 0; i < _activeReservations.Count; i++)
            StopTimeout(_activeReservations[i]);
    }

    private void OnValidate()
    {
        if (_defaultRegenerationDelay < Tail.MinRegenerationDelay) _defaultRegenerationDelay = Tail.MinRegenerationDelay;
        if (_reservationTimeoutSeconds < MinReservationTimeout) _reservationTimeoutSeconds = MinReservationTimeout;
    }

    /// <summary>Adds a runtime-created tail. Returns false for null or already-registered tails.</summary>
    public bool RegisterTail(Tail tail)
    {
        if (tail == null)
        {
            Debug.LogWarning("[TailManager] RegisterTail called with null.", this);
            return false;
        }

        if (!_tailSet.Add(tail))
            return false;

        _tails.Add(tail);
        tail.InjectDefaultRegenerationDelay(_defaultRegenerationDelay);
        if (_isSubscribed)
            tail.StateChanged += HandleTailStateChanged;

        AvailabilityChanged?.Invoke();
        return true;
    }

    /// <summary>Removes a tail. Returns false if it was not registered.</summary>
    public bool UnregisterTail(Tail tail)
    {
        if (tail == null || !_tailSet.Remove(tail))
            return false;

        _tails.Remove(tail);
        tail.StateChanged -= HandleTailStateChanged;
        AvailabilityChanged?.Invoke();
        return true;
    }

    /// <inheritdoc />
    public bool TryReserveTail(out ITailReservation reservation)
    {
        reservation = null;

        for (int i = 0; i < _tails.Count; i++)
        {
            Tail tail = _tails[i];
            if (tail == null || !tail.TryReserve(out int token))
                continue;

            var created = new TailReservation(this, tail, token);
            _activeReservations.Add(created);
            StartTimeout(created);
            reservation = created;
            return true;
        }

        return false;
    }

    private void DiscoverTails()
    {
        Tail[] found = GetComponentsInChildren<Tail>(true);
        for (int i = 0; i < found.Length; i++)
        {
            Tail tail = found[i];
            if (tail == null || !IsOwnedByThisManager(tail))
                continue;

            if (_tailSet.Add(tail))
            {
                _tails.Add(tail);
                tail.InjectDefaultRegenerationDelay(_defaultRegenerationDelay);
            }
        }

        if (_tails.Count == 0)
            Debug.LogWarning("[TailManager] No tails discovered; casts requiring a tail will not start.", this);
    }

    /// <summary>Walks up from the tail; a different TailManager on the way means another character owns it.</summary>
    private bool IsOwnedByThisManager(Tail tail)
    {
        Transform current = tail.transform;
        while (current != null && current != transform)
        {
            if (current.TryGetComponent(out TailManager other) && other != this)
                return false;
            current = current.parent;
        }

        return true;
    }

    private void SubscribeAll()
    {
        if (_isSubscribed) return;
        _isSubscribed = true;

        for (int i = 0; i < _tails.Count; i++)
            if (_tails[i] != null) _tails[i].StateChanged += HandleTailStateChanged;
    }

    private void UnsubscribeAll()
    {
        if (!_isSubscribed) return;
        _isSubscribed = false;

        for (int i = 0; i < _tails.Count; i++)
            if (_tails[i] != null) _tails[i].StateChanged -= HandleTailStateChanged;
    }

    private void HandleTailStateChanged(ITailConsumable tail, TailState previous, TailState next)
    {
        if (previous == TailState.Available || next == TailState.Available)
            AvailabilityChanged?.Invoke();
    }

    private void StartTimeout(TailReservation reservation)
    {
        StopTimeout(reservation);
        if (isActiveAndEnabled && reservation.IsActive)
            reservation.TimeoutRoutine = StartCoroutine(TimeoutRoutine(reservation));
    }

    private void StopTimeout(TailReservation reservation)
    {
        if (reservation.TimeoutRoutine == null) return;
        StopCoroutine(reservation.TimeoutRoutine);
        reservation.TimeoutRoutine = null;
    }

    private IEnumerator TimeoutRoutine(TailReservation reservation)
    {
        float remaining = reservation.ExpiresAt - Time.time;
        while (reservation.IsActive && remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
            remaining = reservation.ExpiresAt - Time.time;
        }

        if (reservation.IsActive)
        {
            Debug.LogWarning($"[TailManager] Reservation exceeded {_reservationTimeoutSeconds}s without Confirm/Release; auto-releasing.", this);
            reservation.Release();
        }
    }

    private void CloseReservation(TailReservation reservation)
    {
        StopTimeout(reservation);
        _activeReservations.Remove(reservation);
    }

    /// <summary>Concrete reservation handle. Guarded by the tail's ownership token.</summary>
    private sealed class TailReservation : ITailReservation
    {
        private readonly TailManager _owner;
        private readonly Tail _tail;
        private readonly int _token;

        public Coroutine TimeoutRoutine;
        public readonly float ExpiresAt;

        public bool IsActive { get; private set; } = true;

        public TailReservation(TailManager owner, Tail tail, int token)
        {
            _owner = owner;
            _tail = tail;
            _token = token;
            ExpiresAt = Time.time + owner._reservationTimeoutSeconds;
        }

        public bool Confirm()
        {
            if (!Deactivate("Confirm")) return false;
            return _tail != null && _tail.Confirm(_token);
        }

        public bool Release()
        {
            if (!Deactivate("Release")) return false;
            return _tail != null && _tail.Release(_token);
        }

        private bool Deactivate(string action)
        {
            if (!IsActive)
            {
                Debug.LogWarning($"[TailManager] {action} ignored: reservation is no longer active.");
                return false;
            }

            IsActive = false;
            if (_owner != null) _owner.CloseReservation(this);
            return true;
        }
    }
}
