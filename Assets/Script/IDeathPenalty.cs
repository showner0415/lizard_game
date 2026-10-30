/// <summary>
/// 사망 패널티. 거점 회복(<see cref="IHomeRestorable"/>)과 다르다.
/// 탈출로 거점에 들어올 때는 호출되지 않고, 사망할 때만 호출된다.
/// 구현체는 인벤토리에 아이템을 넣지 않는다. 레이드 인벤토리 소실은 PlayerDeath가 마지막에 처리한다.
/// PlayerDeath.ApplyPenalties()가 호출한다. 꺼진 컴포넌트도 호출된다.
/// </summary>
public interface IDeathPenalty
{
    /// <summary>사망으로 잃는 것을 적용한다. 이미 적용된 상태에서 다시 불러도 안전해야 한다.</summary>
    void ApplyDeathPenalty();
}
