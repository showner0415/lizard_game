using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Chest : SearchableObject
{
    public ChestType chestType;

    static bool _warnedMagicBookChest;

    public override List<ItemData> GenerateItems()
    {
        generatedItems = new List<ItemData>();
        List<ItemData> filteredItems = FilterItemsByChestType();
        if (filteredItems.Count == 0)
        {
            Debug.LogWarning(DescribeEmptyFilter(), this);
            return generatedItems;
        }

        int count = Random.Range(minItemCount, maxItemCount + 1);
        for (int i = 0; i < count; i++)
            generatedItems.Add(filteredItems[Random.Range(0, filteredItems.Count)]);

        return generatedItems;
    }

    string DescribeEmptyFilter()
    {
        if (possibleItems == null || possibleItems.Count == 0)
            return $"[Chest] '{name}' ({chestType}) Possible Items가 비어 있습니다.";

        int missing = 0;
        var names = new System.Text.StringBuilder();
        for (int i = 0; i < possibleItems.Count; i++)
        {
            ItemData item = possibleItems[i];
            if (item == null)
            {
                missing++;
                continue;
            }

            if (names.Length > 0)
                names.Append(", ");
            names.Append(item.name).Append(" (").Append(item.GetType().Name).Append(')');
        }

        if (missing == possibleItems.Count)
            return $"[Chest] '{name}' ({chestType}) Possible Items가 Missing입니다. 아이템을 다시 넣어 주세요.";

        return $"[Chest] '{name}' ({chestType})에 맞는 아이템이 없습니다. 후보: {names}";
    }

    void WarnMagicBookChest()
    {
        if (_warnedMagicBookChest)
            return;

        _warnedMagicBookChest = true;
        Debug.LogWarning("[Chest] 마법서 상자: 꼬리 장식 상자로 바꾸세요.", this);
    }


    List<ItemData> FilterItemsByChestType()
    {
        if (possibleItems == null)
            return new List<ItemData>();

        switch (chestType)
        {
            case ChestType.Food:
                return possibleItems.Where(i =>
                    i is ConsumableItemData food &&
                    food.consumableType == ConsumableType.Food
                ).ToList();

            case ChestType.Potion:
                return possibleItems.Where(i =>
                    i is ConsumableItemData potion &&
                    potion.consumableType == ConsumableType.Potion
                ).ToList();

            case ChestType.Ornament:
                return possibleItems.Where(i => i is TailOrnamentItemData).ToList();

            case ChestType.Accessory:
                return possibleItems.Where(i =>
                    i is EquipmentItemData accessory &&
                    accessory.equipmentType == EquipmentType.Accessory
                ).ToList();

            case ChestType.MagicBook:
            case ChestType.DarkMagic:
                WarnMagicBookChest();
                return possibleItems.Where(i => i is TailOrnamentItemData).ToList();

            case ChestType.Scale:
                return possibleItems.Where(i =>
                    i is ScaleItemData scaleItem &&
                    scaleItem.Scale != null
                ).ToList();
        }

        return new List<ItemData>();
    }
}