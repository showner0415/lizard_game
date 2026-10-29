using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/*
 * SCALE-SPENDING - SUMMARY
 *
 * Reservation lifecycle
 *   TryBeginCast(n)  -> reserves n Loaded slots (front first). Fails => the spend does not start. Nothing is visible yet.
 *   CompleteCast()   -> reservation.Confirm(): the slot becomes Empty and the magazine regrows it in order.
 *   TryConsumeNow(n) -> reserves n Loaded slots and confirms them immediately. Used by channeling's per-second cost.
 *                       Charge casts still consume only in CompleteCast.
 *   CancelCast()     -> reservation.Release(): the slot returns to Loaded with its scale kept.
 *                       Also runs when this component is disabled/destroyed mid-cast.
 *   CompleteCast() is all-or-nothing and returns true only when the scales were consumed: confirm first, then fire.
 *   There is no reservation timeout; this component releases its reservations in OnDisable.
 *   Grown slots are never reserved: they must be loaded first (ScaleLoader).
 *
 * Scale slot state machine (owned by ScaleSlot, driven by ScaleMagazine)
 *   Empty -> Grown -> Loaded -> Reserved -> Empty
 *               ^        |          |
 *               +-Unload-+          +- Release() returns to Loaded
 *   Unload drops the scale. Home restore does not unload; only the death penalty does.
 *   Confirm/Release outside Reserved, or with a stale token, are no-ops that warn and return false.
 *
 * Time semantics
 *   Everything uses scaled time (Time.time / WaitForSeconds): pausing with Time.timeScale = 0 pauses casts,
 *   and regrowth together. Regrowth deadlines are absolute timestamps, so disabling/re-enabling a component
 *   never loses them.
 *
 * Observer wiring without a direct dependency
 *   ScaleSpender references only IScaleMagazine / IScaleReservation (resolved once in Awake). ScaleMagazine
 *   implements IScaleMagazine, observes each ScaleSlot's StateChanged event (subscribed in OnEnable, unsubscribed in
 *   OnDisable) and republishes it as IScaleMagazine.SlotsChanged for UI/AI. ScaleSpender itself publishes
 *   OnScaleSpendStarted / Completed / Cancelled.
 */

/// <summary>
/// Cast timing and scale-reservation transaction for one caster. Depends only on <see cref="IScaleMagazine"/>.
/// <para>
/// Two ways to drive it: hold-to-cast systems call <see cref="TryBeginCast()"/> then <see cref="CompleteCast"/> /
/// <see cref="CancelCast"/>; fixed-duration casts call <see cref="TryBeginTimedCast"/>.
/// </para>
/// <para>
/// Magazine wiring: assign any component implementing <see cref="IScaleMagazine"/> to <c>_magazineSource</c>
/// (Unity cannot serialize interfaces), or call <see cref="SetMagazine"/> for manual/DI wiring. If the field is
/// empty, a magazine on this object or its parents is used.
/// </para>
/// </summary>
public sealed class ScaleSpender : MonoBehaviour
{
    [Header("Scale Magazine")]
    [Tooltip("Component implementing IScaleMagazine (e.g. ScaleMagazine on the character root). Empty = search this object and parents.")]
    [FormerlySerializedAs("_tailProviderSource")]
    [SerializeField] private MonoBehaviour _magazineSource;

    [Header("Timing")]
    [Tooltip("Shortest accepted duration (seconds, scaled time) for timed casts.")]
    [SerializeField, Min(0.01f)] private float _minTimedCastDuration = 0.05f;

    private IScaleMagazine _magazine;
    private readonly List<IScaleReservation> _reservations = new List<IScaleReservation>();
    private Coroutine _timedCastRoutine;
    private bool _isCasting;
    private float _castStartTime;

    /// <summary>Raised when a spend begins (a scale is reserved but not yet consumed).</summary>
    public event Action<ScaleSpendEventArgs> OnScaleSpendStarted;

    /// <summary>Raised once when a spend finishes and its scale has been consumed.</summary>
    public event Action<ScaleSpendEventArgs> OnScaleSpendCompleted;

    /// <summary>Raised once when a spend is cancelled/interrupted and its reservation released.</summary>
    public event Action<ScaleSpendEventArgs> OnScaleSpendCancelled;

    /// <summary>True between a successful begin and its complete/cancel.</summary>
    public bool IsCasting => _isCasting;

    /// <summary>Awake에서 찾은 비늘 탄창. 아직 없으면 그 시점에 한 번 더 찾는다.</summary>
    public IScaleMagazine Magazine
    {
        get
        {
            if (_magazine == null)
                ResolveMagazine();

            return _magazine;
        }
    }

    private void Awake()
    {
        ResolveMagazine();
    }

    private void OnDisable()
    {
        CancelCast();
    }

    private void OnValidate()
    {
        if (_magazineSource != null && !(_magazineSource is IScaleMagazine))
        {
            Debug.LogWarning($"[ScaleSpender] '{_magazineSource.name}' does not implement IScaleMagazine.", this);
            _magazineSource = null;
        }
    }

    /// <summary>Manual/DI wiring alternative to the serialized source. Cancels any cast in progress.</summary>
    public void SetMagazine(IScaleMagazine magazine)
    {
        CancelCast();
        _magazine = magazine;
    }

    /// <summary>장전된 비늘 1개를 예약하고 시전을 시작한다. <see cref="TryBeginCast(int)"/>.</summary>
    public bool TryBeginCast()
    {
        return TryBeginCast(1);
    }

    /// <summary>
    /// 장전된 비늘을 앞 칸부터 <paramref name="scaleCount"/>개 예약하고, 호출자가 <see cref="CompleteCast"/> 또는 <see cref="CancelCast"/>로 끝내는 시전을 시작한다.
    /// 이미 시전 중이거나, 탄창이 없거나, 장전된 비늘이 모자라면 false. 모자라면 이번에 잡은 예약은 전부 되돌린다.
    /// </summary>
    public bool TryBeginCast(int scaleCount)
    {
        if (_isCasting)
            return false;

        if (scaleCount < 0)
        {
            Debug.LogWarning($"[ScaleSpender] scale cost {scaleCount}는 0으로 맞춥니다.", this);
            scaleCount = 0;
        }

        if (scaleCount == 0)
        {
            _isCasting = true;
            _castStartTime = Time.time;
            OnScaleSpendStarted?.Invoke(new ScaleSpendEventArgs(0f, false));
            return true;
        }

        if (_magazine == null)
            return false;

        for (int i = 0; i < scaleCount; i++)
        {
            if (_magazine.TryReserveScale(out IScaleReservation reservation) && reservation != null)
            {
                _reservations.Add(reservation);
                continue;
            }

            ReleaseReservations();
            return false;
        }

        _isCasting = true;
        _castStartTime = Time.time;
        OnScaleSpendStarted?.Invoke(new ScaleSpendEventArgs(0f, false));
        return true;
    }

    /// <summary>
    /// 장전된 비늘을 <paramref name="count"/>개 예약하고 바로 확정한다. 채널링이 초마다 쓰는 소모다.
    /// 하나라도 예약에 실패하면 이번에 잡은 것은 전부 되돌리고 false.
    /// </summary>
    public bool TryConsumeNow(int count)
    {
        if (_magazine == null || count < 1)
            return false;

        var held = new List<IScaleReservation>(count);
        for (int i = 0; i < count; i++)
        {
            if (_magazine.TryReserveScale(out IScaleReservation reservation) && reservation != null)
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
            Debug.LogWarning($"[ScaleSpender] Invalid cast duration ({durationSeconds}); using {_minTimedCastDuration}s.", this);
            durationSeconds = _minTimedCastDuration;
        }

        if (!TryBeginCast())
            return false;

        _timedCastRoutine = StartCoroutine(TimedCastRoutine(durationSeconds));
        return true;
    }

    /// <summary>
    /// Finishes the cast: confirms every reservation (scales consumed) and raises <see cref="OnScaleSpendCompleted"/> once.
    /// Returns true only when the scales were actually consumed; callers must fire only on true.
    /// If any reservation is no longer active, nothing is consumed, the rest are released,
    /// <see cref="OnScaleSpendCancelled"/> is raised and false is returned. Also false if no cast is in progress.
    /// </summary>
    public bool CompleteCast()
    {
        if (!_isCasting)
            return false;

        List<IScaleReservation> reservations = CopyReservations();
        float elapsed = Time.time - _castStartTime;
        EndCastState();

        // All-or-nothing: a partially released spend must not consume the remaining scales.
        for (int i = 0; i < reservations.Count; i++)
        {
            if (reservations[i] != null && reservations[i].IsActive)
                continue;

            Debug.LogWarning("[ScaleSpender] Cast could not complete: a scale reservation was already released.", this);
            ReleaseActive(reservations);
            OnScaleSpendCancelled?.Invoke(new ScaleSpendEventArgs(elapsed, false));
            return false;
        }

        bool consumed = true;
        for (int i = 0; i < reservations.Count; i++)
        {
            if (!reservations[i].Confirm())
                consumed = false;
        }

        if (!consumed)
            Debug.LogWarning("[ScaleSpender] Cast completed but a scale reservation could not be confirmed.", this);

        OnScaleSpendCompleted?.Invoke(new ScaleSpendEventArgs(elapsed, consumed));
        return consumed;
    }

    /// <summary>
    /// Aborts the cast: releases the reservation (scale untouched) and raises <see cref="OnScaleSpendCancelled"/> once.
    /// Safe to call when not casting.
    /// </summary>
    public bool CancelCast()
    {
        if (!_isCasting)
            return false;

        List<IScaleReservation> reservations = CopyReservations();
        float elapsed = Time.time - _castStartTime;
        EndCastState();
        ReleaseActive(reservations);

        OnScaleSpendCancelled?.Invoke(new ScaleSpendEventArgs(elapsed, false));
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

    List<IScaleReservation> CopyReservations()
    {
        return new List<IScaleReservation>(_reservations);
    }

    void ReleaseReservations()
    {
        ReleaseActive(_reservations);
        _reservations.Clear();
    }

    static void ReleaseActive(List<IScaleReservation> reservations)
    {
        for (int i = 0; i < reservations.Count; i++)
        {
            if (reservations[i] != null && reservations[i].IsActive)
                reservations[i].Release();
        }
    }

    private void ResolveMagazine()
    {
        if (_magazineSource != null)
        {
            _magazine = _magazineSource as IScaleMagazine;
            if (_magazine == null)
                Debug.LogWarning($"[ScaleSpender] '{_magazineSource.name}' does not implement IScaleMagazine.", this);
        }

        if (_magazine == null)
            _magazine = GetComponentInParent<IScaleMagazine>();

        if (_magazine == null)
            Debug.LogWarning("[ScaleSpender] No IScaleMagazine resolved; casts requiring a scale cannot start.", this);
    }
}
