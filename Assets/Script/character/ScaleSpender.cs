using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/*
 * 비늘 소모 요약
 *
 * 탄창은 Awake에서 한 번 찾는다. 다른 시스템은 ScaleSpender.Magazine 또는 FindMagazineFor만 쓴다.
 * 예약은 타임아웃이 없다. 이 컴포넌트가 OnDisable에서 푼다.
 *
 * TryBeginCast(n)  장전된 칸을 앞에서부터 n개 예약한다. 실패하면 시전을 시작하지 않고 잡은 것을 되돌린다.
 * CanCompleteCast  시전 중이고 예약이 전부 확정 가능하면 true. 비용 0(예약 없음)도 true. 상태를 바꾸지 않는다.
 * CompleteCast     먼저 전부 CanConfirm인지 본다. 아니면 풀어 되돌리고 Cancelled.
 *                  consume이 false면 풀어 되돌리고 Completed(Consumed false).
 *                  true면 전부 확정하고 Completed(Consumed true). 확정이 여기서 실패하면 버그라 오류 로그를 남긴다.
 * TryConsumeNow    consume이 false면 장전 수만 본다. true면 전부 예약한 뒤 CanConfirm을 보고, 통과한 것만 확정한다.
 * CancelCast       예약을 풀고 Cancelled.
 */

/// <summary>
/// 한 시전자의 비늘 예약. <see cref="IScaleMagazine"/>만 알고, 플레이어는 모른다.
/// <para>
/// 누르고 있는 시전은 <see cref="TryBeginCast(int)"/> 다음에 <see cref="CompleteCast"/> 또는 <see cref="CancelCast"/>를 부른다.
/// </para>
/// <para>
/// 탄창은 <c>_magazineSource</c>에 연결한다. 인터페이스는 직렬화되지 않으므로 컴포넌트를 넣는다.
/// 비우면 이 오브젝트와 부모에서 찾고, <see cref="SetMagazine"/>으로 직접 넣을 수도 있다.
/// </para>
/// </summary>
public sealed class ScaleSpender : MonoBehaviour
{
    [Header("Scale Magazine")]
    [Tooltip("Component implementing IScaleMagazine (e.g. ScaleMagazine on the character root). Empty = search this object and parents.")]
    [FormerlySerializedAs("_tailProviderSource")]
    [SerializeField] private MonoBehaviour _magazineSource;

    private IScaleMagazine _magazine;
    private readonly List<IScaleReservation> _reservations = new List<IScaleReservation>();
    private readonly List<IScaleReservation> _snapshot = new List<IScaleReservation>();
    private readonly List<IScaleReservation> _held = new List<IScaleReservation>();
    private bool _isCasting;
    private float _castStartTime;

    /// <summary>Raised when a spend begins (a scale is reserved but not yet consumed).</summary>
    public event Action<ScaleSpendEventArgs> OnScaleSpendStarted;

    /// <summary>소모가 허용되면 한 번 발생한다. 비늘이 빠졌는지는 <see cref="ScaleSpendEventArgs.Consumed"/>.</summary>
    public event Action<ScaleSpendEventArgs> OnScaleSpendCompleted;

    /// <summary>확정 전에 예약이 풀려 소모를 거절하면 한 번 발생한다.</summary>
    public event Action<ScaleSpendEventArgs> OnScaleSpendCancelled;

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

    /// <summary>
    /// 시전자에게서 탄창을 찾는다. 부모의 <see cref="ScaleSpender"/>를 먼저 보고, 없으면 자식을 본다.
    /// </summary>
    public static IScaleMagazine FindMagazineFor(GameObject caster)
    {
        if (caster == null)
            return null;

        ScaleSpender spender = caster.GetComponentInParent<ScaleSpender>();
        if (spender == null)
            spender = caster.GetComponentInChildren<ScaleSpender>(true);

        if (spender == null)
            return null;

        return spender.Magazine;
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

        IScaleMagazine magazine = Magazine;
        if (magazine == null)
            return false;

        magazine.BeginBatch();
        try
        {
            for (int i = 0; i < scaleCount; i++)
            {
                if (magazine.TryReserveScale(out IScaleReservation reservation) && reservation != null)
                {
                    _reservations.Add(reservation);
                    continue;
                }

                ReleaseReservations();
                return false;
            }
        }
        finally
        {
            magazine.EndBatch();
        }

        _isCasting = true;
        _castStartTime = Time.time;
        OnScaleSpendStarted?.Invoke(new ScaleSpendEventArgs(0f, false));
        return true;
    }

    /// <summary>
    /// 장전된 비늘을 <paramref name="count"/>개 예약하고 바로 확정한다. 채널링이 초마다 쓰는 소모다.
    /// 하나라도 예약에 실패하면 이번에 잡은 것은 전부 되돌리고 false.
    /// <paramref name="consume"/>이 false면 예약하지 않고, 장전된 칸이 충분한지만 본다.
    /// </summary>
    public bool TryConsumeNow(int count, bool consume = true)
    {
        IScaleMagazine magazine = Magazine;
        if (!consume)
            return magazine != null && magazine.LoadedCount >= count;

        if (magazine == null || count < 1)
            return false;

        magazine.BeginBatch();
        try
        {
            _held.Clear();
            for (int i = 0; i < count; i++)
            {
                if (magazine.TryReserveScale(out IScaleReservation reservation) && reservation != null)
                {
                    _held.Add(reservation);
                    continue;
                }

                ReleaseActive(_held);
                _held.Clear();
                return false;
            }

            for (int i = 0; i < _held.Count; i++)
            {
                if (_held[i] != null && _held[i].CanConfirm)
                    continue;

                ReleaseActive(_held);
                _held.Clear();
                return false;
            }

            bool confirmed = true;
            for (int i = 0; i < _held.Count; i++)
            {
                if (_held[i] != null && _held[i].Confirm())
                    continue;

                confirmed = false;
                Debug.LogError("[ScaleSpender] 확인할 수 있던 예약의 확정이 실패했습니다.", this);
            }

            _held.Clear();
            return confirmed;
        }
        finally
        {
            magazine.EndBatch();
        }
    }

    /// <summary>시전 중이고 예약이 전부 확정 가능하면 true. 예약이 없는 시전도 true. 상태를 바꾸지 않는다.</summary>
    public bool CanCompleteCast()
    {
        if (!_isCasting)
            return false;

        for (int i = 0; i < _reservations.Count; i++)
        {
            IScaleReservation reservation = _reservations[i];
            if (reservation == null || !reservation.CanConfirm)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 예약을 끝낸다. <paramref name="consume"/>이 true면 확정하고, false면 전부 풀어 장전 상태로 되돌린다.
    /// 하나라도 이미 풀려 있으면 나머지는 풀어 되돌리고 <see cref="OnScaleSpendCancelled"/>를 올린 뒤 false.
    /// 시전 중이 아니면 false.
    /// </summary>
    public bool CompleteCast(bool consume = true)
    {
        if (!_isCasting)
            return false;

        CopyReservations(_snapshot);
        float elapsed = Time.time - _castStartTime;
        EndCastState();

        IScaleMagazine magazine = Magazine;
        if (magazine != null)
            magazine.BeginBatch();

        try
        {
            for (int i = 0; i < _snapshot.Count; i++)
            {
                IScaleReservation reservation = _snapshot[i];
                if (reservation != null && reservation.CanConfirm)
                    continue;

                Debug.LogWarning("[ScaleSpender] 확정할 수 없는 예약이 있어 시전을 취소합니다.", this);
                ReleaseActive(_snapshot);
                OnScaleSpendCancelled?.Invoke(new ScaleSpendEventArgs(elapsed, false));
                return false;
            }

            if (!consume)
            {
                ReleaseActive(_snapshot);
                OnScaleSpendCompleted?.Invoke(new ScaleSpendEventArgs(elapsed, false));
                return true;
            }

            bool confirmed = true;
            for (int i = 0; i < _snapshot.Count; i++)
            {
                if (_snapshot[i] != null && _snapshot[i].Confirm())
                    continue;

                confirmed = false;
                Debug.LogError("[ScaleSpender] 확인할 수 있던 예약의 확정이 실패했습니다.", this);
            }

            OnScaleSpendCompleted?.Invoke(new ScaleSpendEventArgs(elapsed, true));
            return confirmed;
        }
        finally
        {
            if (magazine != null)
                magazine.EndBatch();
        }
    }

    /// <summary>
    /// Aborts the cast: releases the reservation (scale untouched) and raises <see cref="OnScaleSpendCancelled"/> once.
    /// Safe to call when not casting.
    /// </summary>
    public bool CancelCast()
    {
        if (!_isCasting)
            return false;

        CopyReservations(_snapshot);
        float elapsed = Time.time - _castStartTime;
        EndCastState();
        IScaleMagazine magazine = Magazine;
        if (magazine != null)
            magazine.BeginBatch();
        try
        {
            ReleaseActive(_snapshot);
        }
        finally
        {
            if (magazine != null)
                magazine.EndBatch();
        }

        OnScaleSpendCancelled?.Invoke(new ScaleSpendEventArgs(elapsed, false));
        return true;
    }

    /// <summary>이벤트보다 먼저 시전 상태를 지워서, 구독자가 바로 다음 시전을 시작해도 된다.</summary>
    private void EndCastState()
    {
        _isCasting = false;
        _reservations.Clear();
    }

    void CopyReservations(List<IScaleReservation> target)
    {
        target.Clear();
        for (int i = 0; i < _reservations.Count; i++)
            target.Add(_reservations[i]);
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
