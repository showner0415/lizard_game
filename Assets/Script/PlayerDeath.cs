using System;
using UnityEngine;

/// <summary>
/// 사망으로 잃는 것을 한 순서대로 적용한다.
/// 목록은 저장하지 않고, 사망할 때마다 다시 모은다.
/// </summary>
public static class PlayerDeath
{
    /// <summary>
    /// 개별 패널티를 적용한 뒤, 마지막에 레이드 인벤토리를 버린다.
    /// </summary>
    public static void ApplyPenalties()
    {
        PlayerHooks.InvokeAll<IDeathPenalty>(penalty => penalty.ApplyDeathPenalty());

        InventoryController inventory = InventoryController.Instance;
        if (inventory == null)
            return;

        try
        {
            // 항상 마지막. 앞의 패널티가 인벤토리에 무엇을 넣어도 함께 사라지게 한다.
            inventory.LoseRaidInventory();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, inventory);
        }
    }
}
