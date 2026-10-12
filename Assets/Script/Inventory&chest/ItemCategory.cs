public enum ItemCategory
{
    Equipment,
    Consumable,
    MagicBook,
    gita

}

public enum EquipmentType
{
    Weapon,
    Accessory
}

/// <summary>장비칸 네 자리. 저장 배열 순서와 같다.</summary>
public enum EquipmentSlotType
{
    /// <summary>무기칸.</summary>
    Weapon = 0,

    /// <summary>갑옷칸.</summary>
    Armor = 1,

    /// <summary>꼬리 아이템 칸.</summary>
    Tail = 2,

    /// <summary>가방칸. 장착 시 인벤 칸 수가 늘어난다.</summary>
    Bag = 3
}

public enum ConsumableType
{
    Potion,
    Food
}
public enum ChestType
{
    Food,
    Weapon,
    MagicBook,
    Accessory,
    Potion,
    DarkMagic
}

public enum Monster
{
    skeleton
}