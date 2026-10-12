// PROPOSED — not applied. Target: Assets/Script/Inventory&chest/InventoryScrollRect.cs (new file)
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ScrollRect with one extra rule: mouse-wheel input is ignored while an item drag is in progress.
/// <para>
/// MID-DRAG SCROLL POLICY: between a slot's OnBeginDrag and OnEndDrag, <see cref="isItemDragInProgress"/> is true
/// and wheel events are dropped. At all other times the wheel behaves exactly like a stock ScrollRect, whether the
/// pointer is over a slot or over empty Content (slots do not implement IScrollHandler, so the event bubbles here).
/// Carrying an item on the cursor (this game's click-to-pick-up) is NOT a drag, so scrolling while carrying works.
/// </para>
/// Everything else (Clamped movement, inertia, deceleration, scrollSensitivity) is the stock serialized ScrollRect
/// state, configured in the Inspector.
/// </summary>
public sealed class InventoryScrollRect : ScrollRect
{
    // The single guard. Set only by the InventoryUI presenter.
    private bool isItemDragInProgress;

    /// <summary>True while a slot drag gesture is active.</summary>
    public bool IsItemDragInProgress => isItemDragInProgress;

    /// <summary>Called by the presenter when a slot drag begins or ends (or is force-reset on close).</summary>
    public void SetItemDragInProgress(bool value)
    {
        isItemDragInProgress = value;
    }

    /// <inheritdoc />
    public override void OnScroll(PointerEventData data)
    {
        if (isItemDragInProgress)
            return;

        base.OnScroll(data);
    }
}
