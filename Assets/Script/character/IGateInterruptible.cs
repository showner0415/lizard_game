/// <summary>행동 잠금이 진행 중인 행동을 끊을 때 부르는 주인.</summary>
public interface IGateInterruptible
{
    /// <summary>잠금이 이 행동을 끊었다. 비용은 내지 않고 진행만 취소한다.</summary>
    void InterruptByGate();
}
