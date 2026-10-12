using UnityEngine;

/// <summary>무기, 장신구처럼 장착 부위를 가지는 아이템.</summary>
[CreateAssetMenu(fileName = "Equipment", menuName = "Item/Equipment")]
public class EquipmentItemData : ItemData
{
    /// <summary>상자 분류용 장비 종류.</summary>
    public EquipmentType equipmentType;

    /// <summary>어느 장비칸에 들어가는지.</summary>
    public EquipmentSlotType slotType;

    /// <inheritdoc />
    public override ItemCategory Category => ItemCategory.Equipment;
}
