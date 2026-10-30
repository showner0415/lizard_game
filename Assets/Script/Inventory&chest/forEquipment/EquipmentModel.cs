using System;

/// <summary>
/// 장비칸 네 자리의 데이터. UI 타입을 모른다.
/// 칸마다 아이템 하나, 스택 없음.
/// </summary>
public sealed class EquipmentModel
{
    /// <summary>장전 표시, 갑옷, 꼬리, 가방. <see cref="EquipmentSlotType"/> 순서와 같다.</summary>
    public const int SlotCount = 4;

    private readonly ItemData[] _slots = new ItemData[SlotCount];

    /// <summary>슬롯 종류, 새로 장착된 아이템(없으면 null).</summary>
    public event Action<EquipmentSlotType, ItemData> OnEquipmentChanged;

    /// <summary>해당 칸의 아이템. 없으면 null.</summary>
    public ItemData Get(EquipmentSlotType slot) => IsValid(slot) ? _slots[(int)slot] : null;

    /// <summary>그 칸에 아이템이 있으면 true.</summary>
    public bool HasItem(EquipmentSlotType slot) => Get(slot) != null;

    /// <summary>그 칸 종류와 맞는 장비만 장착할 수 있다. 장전 표시 칸에는 아무것도 넣지 못한다.</summary>
    public bool CanEquip(EquipmentSlotType slot, ItemData item)
    {
        if (slot == EquipmentSlotType.LoadedScale)
            return false;

        return IsValid(slot) && item is EquipmentItemData eq && eq.slotType == slot;
    }

    /// <summary>장착. 기존 장비는 previous로 돌려준다.</summary>
    public bool TryEquip(EquipmentSlotType slot, ItemData item, out ItemData previous)
    {
        previous = null;
        if (!CanEquip(slot, item))
            return false;

        int index = (int)slot;
        previous = _slots[index];
        _slots[index] = item;
        OnEquipmentChanged?.Invoke(slot, item);
        return true;
    }

    /// <summary>해제하고 있던 아이템을 돌려준다. 빈 칸이면 null.</summary>
    public ItemData Unequip(EquipmentSlotType slot)
    {
        if (!IsValid(slot))
            return null;

        int index = (int)slot;
        ItemData old = _slots[index];
        _slots[index] = null;
        OnEquipmentChanged?.Invoke(slot, null);
        return old;
    }

    /// <summary>핫바/세이브용 ID 배열. 빈 칸은 -1.</summary>
    public int[] ToSaveIds()
    {
        var ids = new int[SlotCount];
        for (int i = 0; i < SlotCount; i++)
            ids[i] = _slots[i] != null ? _slots[i].itemNum : -1;
        return ids;
    }

    /// <summary>저장 배열을 칸에 넣는다. 종류가 안 맞으면 그 칸은 비운다.</summary>
    public void LoadFromSave(int[] ids, ItemDatabase database)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            int num = ids != null && i < ids.Length ? ids[i] : -1;
            ItemData found = num >= 0 && database != null ? database.Get(num) : null;
            EquipmentSlotType slot = (EquipmentSlotType)i;
            _slots[i] = CanEquip(slot, found) ? found : null;
        }

        PublishFullState();
    }

    /// <summary>구독 직후 첫 그리기용.</summary>
    public void PublishFullState()
    {
        for (int i = 0; i < SlotCount; i++)
            OnEquipmentChanged?.Invoke((EquipmentSlotType)i, _slots[i]);
    }

    private static bool IsValid(EquipmentSlotType slot)
    {
        int index = (int)slot;
        return index >= 0 && index < SlotCount;
    }
}
