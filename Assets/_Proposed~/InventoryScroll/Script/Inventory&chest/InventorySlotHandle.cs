// PROPOSED — not applied. Target: Assets/Script/Inventory&chest/InventorySlotHandle.cs (new file)

/// <summary>
/// PRESENTER-side adapter: exposes one model index as an <see cref="IItemSlot"/> so MouseItemUI, HotbarSlot and the
/// Q-drop path can keep talking to "a slot" without touching a View. Every call goes straight to the model;
/// the View updates only through the model's OnSlotChanged event.
/// <para>
/// Stable indexing: one handle per (model, index), created once by InventoryUI and reused across reopen and
/// capacity changes. If capacity shrinks below <see cref="Index"/>, the handle becomes inert (no-op), so a stale
/// reference held by MouseItemUI can never write to a different slot.
/// </para>
/// </summary>
public sealed class InventorySlotHandle : IItemSlot
{
    private readonly InventoryModel model;

    /// <summary>Model slot index this handle addresses.</summary>
    public int Index { get; }

    /// <summary>Creates a handle for one model index.</summary>
    public InventorySlotHandle(InventoryModel model, int index)
    {
        this.model = model;
        Index = index;
    }

    /// <summary>True if this handle targets <paramref name="other"/>.</summary>
    public bool BelongsTo(InventoryModel other) => ReferenceEquals(model, other);

    /// <inheritdoc />
    public bool HasItem() => model.HasItem(Index);

    /// <inheritdoc />
    public ItemData GetItem() => model.GetItem(Index);

    /// <inheritdoc />
    public int GetAmount() => model.GetAmount(Index);

    /// <inheritdoc />
    public void SetItem(ItemData item, int amount = 1) => model.Set(Index, item, amount);

    /// <inheritdoc />
    public void Clear() => model.Clear(Index);

    /// <inheritdoc />
    public void DropItemToWorld()
    {
        if (WorldItemDrop.TryDrop(GetItem(), GetAmount(), "InventorySlot"))
            model.Clear(Index);
    }
}
