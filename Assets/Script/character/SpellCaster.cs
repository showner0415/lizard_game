using System;
using System.Collections;
using UnityEngine;

/*
 * TAIL-CONSUMING SPELLCASTING - SUMMARY
 *
 * Reservation lifecycle
 *   TryBeginCast()   -> ITailProvider.TryReserveTail(). Fails => the cast does not start. Nothing is visible yet.
 *   CompleteCast()   -> reservation.Confirm(): the tail's visuals are disabled and its regeneration timer starts.
 *                       This is the ONLY place a tail is consumed, so never on cast start or mid-cast.
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
    private ITailReservation _reservation;
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

    /// <summary>
    /// Reserves a tail and starts a cast that the caller finishes with <see cref="CompleteCast"/> or <see cref="CancelCast"/>.
    /// Returns false (cast must not start) if already casting, no provider is wired, or no tail is available.
    /// </summary>
    public bool TryBeginCast()
    {
        if (_isCasting || _tailProvider == null)
            return false;

        if (!_tailProvider.TryReserveTail(out ITailReservation reservation))
            return false;

        _reservation = reservation;
        _isCasting = true;
        _castStartTime = Time.time;
        OnSpellCastStarted?.Invoke(new SpellCastEventArgs(0f, false));
        return true;
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

        ITailReservation reservation = _reservation;
        float elapsed = Time.time - _castStartTime;
        EndCastState();

        bool consumed = reservation != null && reservation.Confirm();
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

        ITailReservation reservation = _reservation;
        float elapsed = Time.time - _castStartTime;
        EndCastState();

        if (reservation != null && reservation.IsActive)
            reservation.Release();

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
        _reservation = null;

        if (_timedCastRoutine != null)
        {
            StopCoroutine(_timedCastRoutine);
            _timedCastRoutine = null;
        }
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
