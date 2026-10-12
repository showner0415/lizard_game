using UnityEngine;

/// <summary>장착하면 인벤토리 칸을 늘려 주는 가방.</summary>
[CreateAssetMenu(fileName = "Bag", menuName = "Item/Bag")]
public class BagItemData : EquipmentItemData
{
    /// <summary>기본 인벤 칸 수에 더해지는 칸 수.</summary>
    [Min(0)] public int extraSlots = 10;

    private void Reset()
    {
        slotType = EquipmentSlotType.Bag;
        maxStack = 1;
    }

    /// <inheritdoc />
    protected override void OnValidate()
    {
        base.OnValidate();
        slotType = EquipmentSlotType.Bag;
        maxStack = 1;
        if (extraSlots >= 0)
            return;

        Debug.LogWarning($"[BagItemData] '{name}'의 extraSlots가 0보다 작아 0으로 맞춥니다.", this);
        extraSlots = 0;
    }
}
