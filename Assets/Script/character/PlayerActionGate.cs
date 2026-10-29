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
/// 시간이 걸리는 행동을 한 곳에서 잠근다. 한 번에 하나의 행동만 들어간다.
/// 사망 여부는 여기서 보지 않는다.
/// </summary>
public sealed class PlayerActionGate : MonoBehaviour, IHomeRestorable
{
    /// <summary>지금 진행 중인 행동.</summary>
    public PlayerAction Current { get; private set; } = PlayerAction.None;

    /// <summary>행동이 진행 중이면 true.</summary>
    public bool IsBusy => Current != PlayerAction.None;

    /// <summary>진행 중인 행동이 없을 때만 <paramref name="action"/>으로 들어간다. None은 들어갈 수 없다.</summary>
    public bool TryEnter(PlayerAction action)
    {
        if (action == PlayerAction.None || Current != PlayerAction.None)
            return false;

        Current = action;
        return true;
    }

    /// <summary>지금 행동이 <paramref name="action"/>일 때만 비운다. 다르면 무시한다.</summary>
    public void Exit(PlayerAction action)
    {
        if (Current != action)
            return;

        Current = PlayerAction.None;
    }

    /// <summary>
    /// 지금 행동을 끊기 직전에 그 행동을 알린다.
    /// 그 행동의 주인이 정리하고 <see cref="Exit"/>까지 해야 None이 된다.
    /// </summary>
    public event Action<PlayerAction> ReleaseRequested;

    /// <summary>
    /// 진행 중인 행동의 주인에게 정리를 요청한다.
    /// 이미 없거나, 주인이 빠져나가 None이 되면 true.
    /// </summary>
    public bool RequestRelease()
    {
        if (Current == PlayerAction.None)
            return true;

        ReleaseRequested?.Invoke(Current);
        return Current == PlayerAction.None;
    }

    /// <inheritdoc />
    public void RestoreForHome()
    {
        Current = PlayerAction.None;
    }
}
