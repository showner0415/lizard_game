/// <summary>
/// 사망 패널티. 거점 회복(<see cref="IHomeRestorable"/>)과 다르다.
/// 탈출로 거점에 들어올 때는 호출되지 않고, 사망할 때만 호출된다.
/// </summary>
public interface IDeathPenalty
{
    /// <summary>사망으로 잃는 것을 적용한다. 이미 적용된 상태에서 다시 불러도 안전해야 한다.</summary>
    void ApplyDeathPenalty();
}
