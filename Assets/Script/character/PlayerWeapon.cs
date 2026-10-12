using UnityEngine;

/// <summary>
/// 무기칸만 조회한다. 모델은 캐시하지 않고 물어볼 때마다 읽는다.
/// </summary>
public sealed class PlayerWeapon : MonoBehaviour
{
    /// <summary>장착된 무기. 칸이 비었거나 인벤 컨트롤러가 없으면 null.</summary>
    public EquipmentItemData CurrentWeapon
    {
        get
        {
            InventoryController controller = InventoryController.Instance;
            if (controller == null || controller.Equipment == null)
                return null;

            return controller.Equipment.Get(EquipmentSlotType.Weapon) as EquipmentItemData;
        }
    }
}
