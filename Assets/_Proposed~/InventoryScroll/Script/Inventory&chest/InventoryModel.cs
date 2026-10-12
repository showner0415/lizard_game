// PROPOSED — not applied. Target: Assets/Script/Inventory&chest/InventoryModel.cs (new file)
using System;

/// <summary>Result of <see cref="InventoryModel.TrySetCapacity"/>.</summary>
public enum CapacityChangeResult
{
    /// <summary>Requested capacity equals the current one.</summary>
    Unchanged,

    /// <summary>Capacity changed to the requested value.</summary>
    Changed,

    /// <summary>Request was below <see cref="InventoryModel.MinCapacity"/>; capacity set to the minimum.</summary>
    ClampedToMinimum,

    /// <summary>Reduction refused because at least one slot at or beyond the new limit holds an item. Nothing changed.</summary>
    BlockedByOccupiedSlots
}

/// <summary>
/// MODEL — inventory state and rules. No UI, no MonoBehaviour, no scene references
/// (the only engine type it touches is the <see cref="ItemData"/> asset it stores).
/// <para>
/// Observer: every mutation raises <see cref="OnSlotChanged"/> or <see cref="OnCapacityChanged"/>.
/// The model never knows who listens.
/// </para>
/// <para>
/// Capacity rules:
/// - Capacity below <see cref="MinCapacity"/> is clamped to it (caller logs the warning).
/// - Capacity never drops below the last occupied slot: a reduction that would cut off an item is BLOCKED
///   (<see cref="CapacityChangeResult.BlockedByOccupiedSlots"/>). Items are never deleted or moved implicitly.
/// - Slot indices are stable: index i always means the same storage cell.
/// </para>
/// </summary>
public sealed class InventoryModel
{
    /// <summary>Smallest allowed capacity.</summary>
    public const int MinCapacity = 1;

    private ItemData[] items;
    private int[] amounts;

    /// <summary>Raised as (index, item or null, amount) after a slot's content changes.</summary>
    public event Action<int, ItemData, int> OnSlotChanged;

    /// <summary>Raised with the new capacity after it changes.</summary>
    public event Action<int> OnCapacityChanged;

    /// <summary>Creates a model. Capacity is clamped to <see cref="MinCapacity"/>.</summary>
    public InventoryModel(int capacity)
    {
        int clamped = SanitizeCapacity(capacity, out _);
        items = new ItemData[clamped];
        amounts = new int[clamped];
    }

    /// <summary>Number of slots.</summary>
    public int Capacity => items.Length;

    /// <summary>Clamps a requested capacity to <see cref="MinCapacity"/>.</summary>
    public static int SanitizeCapacity(int requested, out bool wasClamped)
    {
        wasClamped = requested < MinCapacity;
        return wasClamped ? MinCapacity : requested;
    }

    /// <summary>True if <paramref name="index"/> addresses an existing slot.</summary>
    public bool IsValidIndex(int index) => index >= 0 && index < items.Length;

    /// <summary>True if the slot holds an item.</summary>
    public bool HasItem(int index) => IsValidIndex(index) && items[index] != null && amounts[index] > 0;

    /// <summary>Item in the slot, or null.</summary>
    public ItemData GetItem(int index) => HasItem(index) ? items[index] : null;

    /// <summary>Amount in the slot (0 when empty).</summary>
    public int GetAmount(int index) => HasItem(index) ? amounts[index] : 0;

    /// <summary>Highest occupied index, or -1 when empty.</summary>
    public int HighestOccupiedIndex
    {
        get
        {
            for (int i = items.Length - 1; i >= 0; i--)
                if (HasItem(i)) return i;
            return -1;
        }
    }

    /// <summary>Overwrites a slot. A null item or a non-positive amount clears it.</summary>
    public void Set(int index, ItemData item, int amount)
    {
        if (!IsValidIndex(index))
            return;

        if (item == null || amount <= 0)
        {
            item = null;
            amount = 0;
        }

        items[index] = item;
        amounts[index] = amount;
        OnSlotChanged?.Invoke(index, item, amount);
    }

    /// <summary>Empties a slot.</summary>
    public void Clear(int index) => Set(index, null, 0);

    /// <summary>Empties every occupied slot (one event per cleared slot).</summary>
    public void ClearAll()
    {
        for (int i = 0; i < items.Length; i++)
            if (HasItem(i)) Clear(i);
    }

    /// <summary>Same rule as the old ItemSlotUI.CanStackWith: same asset, stackable, not full.</summary>
    public bool CanStackWith(int index, ItemData other)
    {
        if (!HasItem(index))
            return false;

        ItemData current = items[index];
        return current == other && current.stackable && amounts[index] < current.maxStack;
    }

    /// <summary>Adds up to <paramref name="value"/> to an occupied slot; returns how many were added.</summary>
    public int AddToStack(int index, int value)
    {
        if (!HasItem(index) || value <= 0)
            return 0;

        int space = items[index].maxStack - amounts[index];
        int added = Math.Max(0, Math.Min(space, value));
        if (added > 0)
            Set(index, items[index], amounts[index] + added);
        return added;
    }

    /// <summary>
    /// Old InventoryUI.TryAddItem behavior, unchanged: fill matching stacks first, then empty slots.
    /// Returns false if anything was left over. Items placed before running out stay placed (non-atomic, like before).
    /// </summary>
    public bool TryAdd(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return false;

        int remain = amount;
        StackInto(item, ref remain);
        if (remain <= 0)
            return true;

        return FillEmpty(item, ref remain);
    }

    /// <summary>Old InventoryUI.TryStackOnly: merge into matching stacks only.</summary>
    public bool TryStackOnly(ItemData item, int amount, out int remainingAmount)
    {
        remainingAmount = amount;
        if (item == null || amount <= 0)
            return false;

        bool anyStacked = StackInto(item, ref remainingAmount);
        return anyStacked;
    }

    /// <summary>Old InventoryUI.TryAddToEmptySlot: use empty slots only.</summary>
    public bool TryAddToEmptySlot(ItemData item, int amount)
    {
        if (item == null || amount <= 0)
            return false;

        int remain = amount;
        return FillEmpty(item, ref remain);
    }

    /// <summary>
    /// Changes capacity. Growing always succeeds. Shrinking is blocked if any slot at or beyond the new limit
    /// is occupied (the player must move or drop those items first). Nothing is ever deleted.
    /// </summary>
    public CapacityChangeResult TrySetCapacity(int requested)
    {
        int target = SanitizeCapacity(requested, out bool clamped);
        if (target == items.Length)
            return clamped ? CapacityChangeResult.ClampedToMinimum : CapacityChangeResult.Unchanged;

        for (int i = target; i < items.Length; i++)
            if (HasItem(i)) return CapacityChangeResult.BlockedByOccupiedSlots;

        Array.Resize(ref items, target);
        Array.Resize(ref amounts, target);
        OnCapacityChanged?.Invoke(target);
        return clamped ? CapacityChangeResult.ClampedToMinimum : CapacityChangeResult.Changed;
    }

    private bool StackInto(ItemData item, ref int remain)
    {
        bool anyStacked = false;
        for (int i = 0; i < items.Length && remain > 0; i++)
        {
            if (!CanStackWith(i, item))
                continue;

            remain -= AddToStack(i, remain);
            anyStacked = true;
        }
        return anyStacked;
    }

    private bool FillEmpty(ItemData item, ref int remain)
    {
        for (int i = 0; i < items.Length && remain > 0; i++)
        {
            if (HasItem(i))
                continue;

            int addAmount = Math.Min(item.maxStack, remain);
            Set(i, item, addAmount);
            remain -= addAmount;
        }
        return remain <= 0;
    }
}
