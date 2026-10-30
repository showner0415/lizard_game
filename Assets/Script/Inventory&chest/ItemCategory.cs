public enum ItemCategory
{
    Equipment = 0,
    Consumable = 1,

    /// <summary>더 이상 쓰지 않는다. 꼬리 장식으로 바뀌었다.</summary>
    [System.Obsolete("마법서는 꼬리 장식으로 바뀌었다.")]
    MagicBook = 2,

    gita = 3,

    /// <summary>파밍 비늘. 장전하면 탄창에 들어간다.</summary>
    Scale = 4
}

public enum EquipmentType
{
    Weapon,
    Accessory
}

/// <summary>장비칸 네 자리. 저장 배열 순서와 같다.</summary>
public enum EquipmentSlotType
{
    /// <summary>장전 표시 칸. 아이템을 장착할 수 없다.</summary>
    LoadedScale = 0,

    /// <summary>갑옷칸.</summary>
    Armor = 1,

    /// <summary>꼬리 장식 칸.</summary>
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
    Food = 0,

    /// <summary>꼬리 장식 상자.</summary>
    Ornament = 1,

    MagicBook = 2,
    Accessory = 3,
    Potion = 4,
    DarkMagic = 5,

    /// <summary>파밍 비늘 상자.</summary>
    Scale = 6
}

public enum Monster
{
    skeleton
}