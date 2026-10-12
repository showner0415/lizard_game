// PROPOSED — not applied. Target: Assets/Script/Inventory&chest/InventorySlotView.cs (new file)
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// VIEW — one inventory cell. Renders exactly what the presenter tells it and reports raw pointer input.
/// No item state, no inventory rules, no reference to the model or the presenter type.
/// <para>
/// Input contract:
/// - Click / hover → raised as events with this cell's <see cref="Index"/>; the presenter decides what they mean.
/// - Drag handlers exist for two reasons only: (1) they make this cell the drag target, so the parent ScrollRect
///   cannot start a content-drag that would cancel the click (UGUI cancels a click when a different object starts
///   dragging); (2) they tell the presenter a drag gesture is in progress, which suppresses wheel scrolling.
///   No item is moved by dragging (this game moves items by click), so behavior matches the old ItemSlotUI.
/// - Does NOT implement IScrollHandler, so wheel events bubble to the ScrollRect unchanged.
/// </para>
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class InventorySlotView : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Visuals (same references the old ItemSlotUI used)")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Placeholder (partial last row)")]
    [Tooltip("Alpha of filler cells that complete the last row. They never receive input.")]
    [SerializeField, Range(0f, 1f)] private float placeholderAlpha = 0.35f;

    private CanvasGroup canvasGroup;

    /// <summary>Model index assigned by the presenter (-1 until assigned).</summary>
    public int Index { get; private set; } = -1;

    /// <summary>Raised as (index, pointer data) on click.</summary>
    public event Action<int, PointerEventData> Clicked;

    /// <summary>Raised with the index when the pointer enters.</summary>
    public event Action<int> HoverEntered;

    /// <summary>Raised with the index when the pointer exits.</summary>
    public event Action<int> HoverExited;

    /// <summary>Raised with the index when a drag gesture starts on this cell.</summary>
    public event Action<int> DragBegan;

    /// <summary>Raised with the index when that drag gesture ends.</summary>
    public event Action<int> DragEnded;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>Assigns the model index this cell displays. Called once per pooled cell.</summary>
    public void AssignIndex(int index)
    {
        Index = index;
    }

    /// <summary>Shows an icon and, if requested, a count.</summary>
    public void Render(Sprite icon, int amount, bool showCount)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (countText != null)
        {
            countText.gameObject.SetActive(showCount);
            if (showCount)
                countText.text = amount.ToString();
        }
    }

    /// <summary>Shows an empty cell.</summary>
    public void RenderEmpty()
    {
        Render(null, 0, false);
    }

    /// <summary>Placeholder cells look dimmed and are invisible to raycasts (wheel input passes to the panel).</summary>
    public void SetPlaceholder(bool isPlaceholder)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = isPlaceholder ? placeholderAlpha : 1f;
        canvasGroup.interactable = !isPlaceholder;
        canvasGroup.blocksRaycasts = !isPlaceholder;

        if (isPlaceholder)
            RenderEmpty();
    }

    /// <inheritdoc />
    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(Index, eventData);

    /// <inheritdoc />
    public void OnPointerEnter(PointerEventData eventData) => HoverEntered?.Invoke(Index);

    /// <inheritdoc />
    public void OnPointerExit(PointerEventData eventData) => HoverExited?.Invoke(Index);

    /// <inheritdoc />
    public void OnBeginDrag(PointerEventData eventData) => DragBegan?.Invoke(Index);

    /// <inheritdoc />
    public void OnDrag(PointerEventData eventData)
    {
        // Intentionally empty: required for UGUI to treat this cell as the drag target.
    }

    /// <inheritdoc />
    public void OnEndDrag(PointerEventData eventData) => DragEnded?.Invoke(Index);
}
