using System;

/// <summary>
/// Inventory item state and capacity. No UI types, no GameObject references.
/// Presenter subscribes to <see cref="OnSlotChanged"/> / <see cref="OnCapacityChanged"/>.
/// </summary>
public sealed class InventoryModel
{
    /// <summary>Lowest accepted capacity. Values below this are clamped with <see cref="OnWarning"/>.</summary>
    public const int MinCapacity = 1;

    /// <summary>Grid column count the Presenter/View must match.</summary>
    public const int ColumnCount = 5;

    private ItemStack[] _slots;

    /// <summary>Fired after one slot changes. item is null when the slot is empty.</summary>
    public event Action<int, ItemData, int> OnSlotChanged;

    /// <summary>Fired after a successful capacity change.</summary>
    public event Action<int> OnCapacityChanged;

    /// <summary>Optional warning sink (Controller/Presenter may log). Model does not call Debug.</summary>
    public event Action<string> OnWarning;

    /// <summary>Current slot count.</summary>
    public int Capacity => _slots != null ? _slots.Length : 0;

    /// <summary>Creates an empty inventory with the given capacity (clamped to <see cref="MinCapacity"/>).</summary>
    public InventoryModel(int capacity)
    {
        _slots = CreateSlots(SanitizeCapacity(capacity));
    }

    /// <summary>Read-only peek. Do not mutate the returned stack; use SetSlot/ClearSlot.</summary>
    public ItemStack Peek(int index)
    {
        if (!IsValidIndex(index))
            return null;
        return _slots[index];
    }

    /// <summary>True when the slot holds an item.</summary>
    public bool HasItem(int index)
    {
        ItemStack stack = Peek(index);
        return stack != null && stack.HasItem;
    }

    /// <summary>
    /// Shrink is blocked if any slot at index &gt;= newCapacity still has an item.
    /// Items are never deleted by a capacity change.
    /// </summary>
    public bool TrySetCapacity(int newCapacity)
    {
        int sanitized = SanitizeCapacity(newCapacity);
        if (sanitized == Capacity)
            return true;

        if (sanitized < Capacity)
        {
            for (int i = sanitized; i < Capacity; i++)
            {
                if (_slots[i] != null && _slots[i].HasItem)
                {
                    RaiseWarning($"Capacity shrink {Capacity}→{sanitized} blocked: slot {i} is occupied.");
                    return false;
                }
            }
        }

        var next = new ItemStack[sanitized];
        int copy = Math.Min(Capacity, sanitized);
        for (int i = 0; i < sanitized; i++)
            next[i] = i < copy && _slots[i] != null ? _slots[i] : new ItemStack();

        _slots = next;
        OnCapacityChanged?.Invoke(Capacity);
        return true;
    }

    /// <summary>Writes one slot and notifies observers.</summary>
    public void SetSlot(int index, ItemData item, int amount)
    {
        if (!IsValidIndex(index))
            return;

        _slots[index].Set(item, amount);
        RaiseSlot(index);
    }

    /// <summary>Clears one slot.</summary>
    public void ClearSlot(int index) => SetSlot(index, null, 0);

    /// <summary>Clears every occupied slot.</summary>
    public void ClearAll()
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (_slots[i] != null && _slots[i].HasItem)
            {
                _slots[i].Clear();
                RaiseSlot(i);
            }
        }
    }

    /// <summary>Stack into existing stacks, then empty slots. True only when the whole amount fits.</summary>
    public bool TryAddItem(ItemData item, int amount) => AddItem(item, amount) <= 0;

    /// <summary>Puts as much as possible into the inventory. Returns the amount that did not fit.</summary>
    public int AddItem(ItemData item, int amount)
    {
        if (item == null || amount <= 0)
            return amount;

        int remain = FillExistingStacks(item, amount);
        return FillEmptySlots(item, remain);
    }

    /// <summary>Merge into existing stacks only.</summary>
    public bool TryStackOnly(ItemData item, int amount, out int remaining)
    {
        remaining = amount;
        if (item == null || amount <= 0)
            return false;

        int before = remaining;
        remaining = FillExistingStacks(item, remaining);
        return remaining < before;
    }

    /// <summary>Fill empty slots only.</summary>
    public bool TryAddToEmptySlot(ItemData item, int amount)
    {
        if (item == null || amount <= 0)
            return false;

        return FillEmptySlots(item, amount) <= 0;
    }

    /// <summary>How many of <paramref name="item"/> are stored, summed across stacks. Same asset reference only.</summary>
    public int CountOf(ItemData item)
    {
        if (item == null || _slots == null)
            return 0;

        int count = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            ItemStack stack = _slots[i];
            if (stack != null && stack.HasItem && stack.item == item)
                count += stack.amount;
        }

        return count;
    }

    /// <summary>
    /// Removes <paramref name="amount"/> of <paramref name="item"/>.
    /// Removes nothing and returns false unless the whole amount is present.
    /// Each changed slot raises <see cref="OnSlotChanged"/>.
    /// </summary>
    public bool TryRemove(ItemData item, int amount)
    {
        if (item == null || amount < 1 || CountOf(item) < amount)
            return false;

        int remain = amount;
        for (int i = 0; i < _slots.Length && remain > 0; i++)
        {
            ItemStack stack = _slots[i];
            if (stack == null || !stack.HasItem || stack.item != item)
                continue;

            int take = Math.Min(stack.amount, remain);
            stack.Set(stack.item, stack.amount - take);
            remain -= take;
            RaiseSlot(i);
        }

        return true;
    }

    /// <summary>Lightweight save payload (itemNum + amount only).</summary>
    public SlotSaveData[] ToSaveArray()
    {
        var result = new SlotSaveData[Capacity];
        for (int i = 0; i < Capacity; i++)
            result[i] = _slots[i] != null ? _slots[i].ToSave() : new SlotSaveData();
        return result;
    }

    /// <summary>현재 칸 수를 유지한 채 저장 데이터를 넣는다. 가방 적용 뒤에 호출한다.</summary>
    public void LoadIntoCurrentCapacity(SlotSaveData[] saved, ItemDatabase database)
    {
        if (saved != null && saved.Length > Capacity)
            RaiseWarning($"Save has {saved.Length} inventory slots but capacity is {Capacity}. Extra slots are ignored.");

        for (int i = 0; i < Capacity; i++)
        {
            SlotSaveData save = saved != null && i < saved.Length ? saved[i] : null;
            _slots[i].FromSave(save, database);
            RaiseSlot(i);
        }
    }

    /// <summary>Restores from save. Invalid item ids become empty slots (ItemStack.FromSave).</summary>
    public void LoadFromSave(SlotSaveData[] saved, ItemDatabase database)
    {
        int count = saved != null && saved.Length > 0 ? SanitizeCapacity(saved.Length) : Capacity;
        if (count != Capacity)
        {
            _slots = CreateSlots(count);
            OnCapacityChanged?.Invoke(Capacity);
        }

        LoadIntoCurrentCapacity(saved, database);
    }

    /// <summary>Presenter must call this on subscribe so the first paint is not empty.</summary>
    public void PublishFullState()
    {
        for (int i = 0; i < Capacity; i++)
            RaiseSlot(i);
    }

    private int FillExistingStacks(ItemData item, int remain)
    {
        for (int i = 0; i < Capacity && remain > 0; i++)
        {
            ItemStack stack = _slots[i];
            if (stack == null || !stack.HasItem)
                continue;
            if (stack.item != item || !item.Stackable || stack.amount >= item.maxStack)
                continue;

            int space = item.maxStack - stack.amount;
            int added = Math.Min(space, remain);
            stack.amount += added;
            remain -= added;
            RaiseSlot(i);
        }

        return remain;
    }

    private int FillEmptySlots(ItemData item, int remain)
    {
        for (int i = 0; i < Capacity && remain > 0; i++)
        {
            ItemStack stack = _slots[i];
            if (stack != null && stack.HasItem)
                continue;

            int add = Math.Min(item.maxStack, remain);
            _slots[i].Set(item, add);
            remain -= add;
            RaiseSlot(i);
        }

        return remain;
    }

    private void RaiseSlot(int index)
    {
        ItemStack stack = _slots[index];
        bool has = stack != null && stack.HasItem;
        OnSlotChanged?.Invoke(index, has ? stack.item : null, has ? stack.amount : 0);
    }

    private void RaiseWarning(string message) => OnWarning?.Invoke(message);

    private bool IsValidIndex(int index) => _slots != null && index >= 0 && index < _slots.Length;

    private int SanitizeCapacity(int capacity)
    {
        if (capacity < MinCapacity)
        {
            RaiseWarning($"capacity {capacity} clamped to {MinCapacity}.");
            return MinCapacity;
        }

        return capacity;
    }

    private static ItemStack[] CreateSlots(int count)
    {
        var slots = new ItemStack[count];
        for (int i = 0; i < count; i++)
            slots[i] = new ItemStack();
        return slots;
    }
}
