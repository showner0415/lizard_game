/// <summary>마우스가 아이템을 집은 칸. 모델 칸이거나 장비칸이다.</summary>
public enum SlotRefKind
{
    /// <summary>출처 없음.</summary>
    None = 0,

    /// <summary>장비칸.</summary>
    Equipment = 1,

    /// <summary>어떤 <see cref="InventoryModel"/>의 몇 번 칸. 인벤, 창고, 상자 모두.</summary>
    Container = 2
}

/// <summary>집은 위치를 하나만 기억한다.</summary>
public struct SlotRef
{
    /// <summary>어느 종류의 칸인지.</summary>
    public SlotRefKind Kind;

    /// <summary><see cref="SlotRefKind.Equipment"/>일 때 장비칸.</summary>
    public EquipmentSlotType EquipmentSlot;

    /// <summary><see cref="SlotRefKind.Container"/>일 때 칸이 속한 모델.</summary>
    public InventoryModel Container;

    /// <summary><see cref="SlotRefKind.Container"/>일 때 칸 번호.</summary>
    public int ContainerIndex;

    /// <summary>출처 없음.</summary>
    public static SlotRef None => default;

    /// <summary>플레이어 인벤 칸이면 true. 창고·상자는 false.</summary>
    public bool IsPlayerInventory =>
        Kind == SlotRefKind.Container &&
        Container != null &&
        InventoryController.Instance != null &&
        Container == InventoryController.Instance.Inventory;

    /// <summary>모델 칸.</summary>
    public static SlotRef FromContainer(InventoryModel model, int index)
    {
        return new SlotRef
        {
            Kind = SlotRefKind.Container,
            Container = model,
            ContainerIndex = index
        };
    }

    /// <summary>장비칸.</summary>
    public static SlotRef FromEquipment(EquipmentSlotType slot)
    {
        return new SlotRef
        {
            Kind = SlotRefKind.Equipment,
            EquipmentSlot = slot
        };
    }
}
