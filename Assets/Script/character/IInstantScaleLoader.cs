/// <summary>
/// 지금 선택된 비늘을 곧바로 장전하는 계약. 마나는 여기서 내지 않는다.
/// </summary>
public interface IInstantScaleLoader
{
    /// <summary>
    /// 지금 <paramref name="maxCount"/>칸까지 장전할 수 있으면 true. 상태를 바꾸지 않는다.
    /// 불가능하면 <paramref name="reason"/>에 짧은 이유를 담는다.
    /// </summary>
    bool CanLoadNow(int maxCount, out string reason);

    /// <summary>
    /// 선택된 비늘을 최대 <paramref name="maxCount"/>칸 장전한다. 실제로 채운 칸 수를 돌려준다.
    /// 마나는 내지 않는다.
    /// </summary>
    int LoadNow(int maxCount);
}
