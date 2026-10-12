using UnityEngine;

/// <summary>포션, 음식처럼 사용하고 사라지는 아이템.</summary>
[CreateAssetMenu(fileName = "Consumable", menuName = "Item/Consumable")]
public class ConsumableItemData : ItemData
{
    /// <summary>소모품 세부 종류.</summary>
    public ConsumableType consumableType;

    /// <inheritdoc />
    public override ItemCategory Category => ItemCategory.Consumable;
}
