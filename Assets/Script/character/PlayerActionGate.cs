using System;
using UnityEngine;

/// <summary>
/// 플레이어가 시간을 들여 하고 있는 행동. None이면 새 행동을 시작할 수 있다.
/// 차지와 채널링은 둘 다 <see cref="Throwing"/>이다. 어느 쪽인지는 공격 쪽이 기억한다.
/// </summary>
public enum PlayerAction
{
    /// <summary>진행 중인 행동이 없다.</summary>
    None = 0,

    /// <summary>비늘을 던져 마법을 쓰는 중이다. 차지와 채널링을 가리지 않는다.</summary>
    Throwing = 1,

    /// <summary>비늘을 장전 중이다.</summary>
    Loading = 2
}

/// <summary>
/// 시간이 걸리는 행동을 한 곳에서 잠근다. 진행도는 읽을 때 계산하고, Update는 쓰지 않는다.
/// 사망 여부는 여기서 보지 않는다.
/// </summary>
public sealed class PlayerActionGate : MonoBehaviour, IHomeRestorable
{
    PlayerAction _current = PlayerAction.None;
    IGateInterruptible _owner;
    float _startTime;
    float _duration;

    /// <summary>지금 진행 중인 행동. 주인이 파괴됐으면 None.</summary>
    public PlayerAction Current
    {
        get
        {
            ClearIfOwnerDead();
            return _current;
        }
    }

    /// <summary>행동이 진행 중이면 true.</summary>
    public bool IsBusy => Current != PlayerAction.None;

    /// <summary>시간이 있는 행동이면 true. 채널링과 즉시 시전은 시간이 0이다.</summary>
    public bool HasTimedAction => IsBusy && _duration > 0f;

    /// <summary>0부터 1까지. 시간이 없으면 0. 스케일된 시간이다.</summary>
    public float Progress
    {
        get
        {
            if (!HasTimedAction)
                return 0f;

            return Mathf.Clamp01((Time.time - _startTime) / _duration);
        }
    }

    /// <summary>진행 중인 행동이 없을 때만 들어간다. None과 빈 주인은 거부한다.</summary>
    public bool TryEnter(PlayerAction action, IGateInterruptible owner, float duration)
    {
        if (action == PlayerAction.None || owner == null || IsBusy)
            return false;

        if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
            duration = 0f;

        _current = action;
        _owner = owner;
        _startTime = Time.time;
        _duration = duration;
        return true;
    }

    /// <summary>행동과 주인이 둘 다 지금 값과 같을 때만 비운다. 아니면 false이고 아무것도 하지 않는다.</summary>
    public bool Exit(PlayerAction action, IGateInterruptible owner)
    {
        ClearIfOwnerDead();
        if (_current != action || !ReferenceEquals(_owner, owner))
            return false;

        ClearState();
        return true;
    }

    /// <summary>지금 주인을 끊는다. 상태를 먼저 비운 뒤 주인에게 알린다.</summary>
    public void RequestRelease()
    {
        ClearIfOwnerDead();
        if (_current == PlayerAction.None)
            return;

        IGateInterruptible owner = _owner;
        ClearState();
        if (owner == null)
            return;

        try
        {
            owner.InterruptByGate();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    /// <inheritdoc />
    public void RestoreForHome()
    {
        ClearState();
    }

    void ClearIfOwnerDead()
    {
        if (_owner is UnityEngine.Object unityOwner && unityOwner == null)
            ClearState();
    }

    void ClearState()
    {
        _current = PlayerAction.None;
        _owner = null;
        _startTime = 0f;
        _duration = 0f;
    }
}
