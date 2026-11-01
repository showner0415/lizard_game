/// <summary>
/// 거점인지 레이드인지에 따라 비늘과 마나를 내는지 정한다.
/// 탄창은 이 규칙을 모른다.
/// </summary>
public static class PlayerCostRules
{
    /// <summary>거점이면 true. 레이드이거나 인벤토리 컨트롤러가 없으면 false.</summary>
    public static bool IsFree
    {
        get
        {
            InventoryController inventory = InventoryController.Instance;
            return inventory != null && !inventory.InRaid;
        }
    }
}
