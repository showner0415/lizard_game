/// <summary>마우스가 올라가 있을 때 툴팁이 물어볼 현재 아이템.</summary>
public interface IItemTooltipSource
{
    /// <summary>지금 이 칸에 있는 아이템. 비었으면 null.</summary>
    ItemData TooltipItem { get; }
}
