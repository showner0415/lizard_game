using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Only layer that references both <see cref="InventoryModel"/> and <see cref="InventorySlotView"/>.
/// Observer: subscribe in OnEnable, unsubscribe the same delegates in OnDisable. No duplicate subscribe on reopen.
///
/// Mid-hold scroll policy (this project uses click-pickup, not IBeginDrag):
///   _isItemHeldOnCursor == true  → ScrollRect disabled; wheel over slots/content does nothing.
///   _isItemHeldOnCursor == false → wheel works the same over a slot or empty Content
///     because InventorySlotView does not implement IScrollHandler (EventSystem bubbles to ScrollRect).
/// </summary>
public sealed class InventoryPresenter : MonoBehaviour
{
    /// <summary>One GameObject per slot is acceptable up to this count. Beyond it, virtualize.</summary>
    public const int SupportedMaxSlots = 200;

    [Header("View wiring")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _content;
    [SerializeField] private InventorySlotView _slotPrefab;
    [SerializeField] private float _scrollSensitivity = 30f;

    private readonly List<InventorySlotView> _views = new List<InventorySlotView>(64);
    private InventoryModel _model;
    private bool _subscribed;
    private SlotRef _hoveredSlot;

    /// <summary>
    /// Single named guard: an item is on the cursor (click-pickup in progress).
    /// While true, mouse-wheel must not scroll this panel.
    /// </summary>
    private bool _isItemHeldOnCursor;

    /// <summary>Bound model, or null before BindModel.</summary>
    public InventoryModel Model => _model;

    /// <summary>True when this presenter can draw the inventory.</summary>
    public bool IsDriving => _model != null && _slotPrefab != null && _content != null;

    /// <summary>Wires the model. Safe to call again after a scene reload.</summary>
    public void BindModel(InventoryModel model)
    {
        UnsubscribeModel();
        _model = model;
        if (isActiveAndEnabled)
            SubscribeAndSync();
    }

    private void OnEnable()
    {
        ApplyScrollDefaults();

        if (_model == null && InventoryController.Instance != null)
            _model = InventoryController.Instance.Inventory;

        if (_model != null)
            SubscribeAndSync();

        if (MouseItemUI.Instance != null)
            MouseItemUI.Instance.HeldItemChanged += SetItemHeldOnCursor;
        SetItemHeldOnCursor(MouseItemUI.Instance != null && MouseItemUI.Instance.HasItem());
    }

    private void OnDisable()
    {
        if (MouseItemUI.Instance != null)
            MouseItemUI.Instance.HeldItemChanged -= SetItemHeldOnCursor;

        UnsubscribeModel();
        UnbindViewEvents();
        ItemTransfer.ClearHovered(_hoveredSlot);
        _hoveredSlot = SlotRef.None;
        SetItemHeldOnCursor(false);
    }

    private void OnDestroy()
    {
        UnsubscribeModel();
        UnbindViewEvents();
    }

    private void ApplyScrollDefaults()
    {
        if (_scrollRect == null)
            return;

        _scrollRect.scrollSensitivity = _scrollSensitivity;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.inertia = true;
        _scrollRect.decelerationRate = 0.135f;
    }

    private void SubscribeAndSync()
    {
        if (_model == null || _subscribed)
            return;

        _model.OnSlotChanged += HandleSlotChanged;
        _model.OnCapacityChanged += HandleCapacityChanged;
        _model.OnWarning += HandleModelWarning;
        _subscribed = true;

        if (!IsDriving)
        {
            Debug.LogWarning("[InventoryPresenter] ScrollRect/Content/Slot Prefab이 비어 있습니다. Inspector에서 연결하세요.", this);
            return;
        }

        RebuildViews(_model.Capacity);
        _model.PublishFullState();
        ResetScrollToTopAfterLayout();
    }

    private void UnsubscribeModel()
    {
        if (!_subscribed || _model == null)
            return;

        _model.OnSlotChanged -= HandleSlotChanged;
        _model.OnCapacityChanged -= HandleCapacityChanged;
        _model.OnWarning -= HandleModelWarning;
        _subscribed = false;
    }

    private void HandleModelWarning(string message)
    {
        Debug.LogWarning("[InventoryModel] " + message, this);
    }

    private void HandleCapacityChanged(int newCapacity)
    {
        if (!IsDriving)
            return;

        RebuildViews(newCapacity);
        _model.PublishFullState();
        ResetScrollToTopAfterLayout();
    }

    private void HandleSlotChanged(int index, ItemData item, int amount)
    {
        if (index < 0 || index >= _views.Count)
            return;
        if (!_views[index].gameObject.activeSelf)
            return;
        _views[index].Render(item, amount);
    }

    private void RebuildViews(int capacity)
    {
        if (capacity > SupportedMaxSlots)
            Debug.LogWarning($"[InventoryPresenter] {capacity} slots exceeds supported max {SupportedMaxSlots}. Consider virtualizing.", this);

        while (_views.Count < capacity)
        {
            InventorySlotView view = Instantiate(_slotPrefab, _content);
            int index = _views.Count;
            view.BindIndex(index, _model);
            _views.Add(view);
        }

        for (int i = 0; i < _views.Count; i++)
        {
            bool inRange = i < capacity;
            _views[i].gameObject.SetActive(inRange);
            if (inRange)
            {
                _views[i].BindIndex(i, _model);
                _views[i].Clicked -= HandleSlotClicked;
                _views[i].PointerEntered -= HandlePointerEntered;
                _views[i].PointerExited -= HandlePointerExited;
                _views[i].Clicked += HandleSlotClicked;
                _views[i].PointerEntered += HandlePointerEntered;
                _views[i].PointerExited += HandlePointerExited;
            }
        }
    }

    private void UnbindViewEvents()
    {
        for (int i = 0; i < _views.Count; i++)
        {
            if (_views[i] == null)
                continue;
            _views[i].Clicked -= HandleSlotClicked;
            _views[i].PointerEntered -= HandlePointerEntered;
            _views[i].PointerExited -= HandlePointerExited;
        }
    }

    private void ResetScrollToTopAfterLayout()
    {
        if (_content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        Canvas.ForceUpdateCanvases();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void SetItemHeldOnCursor(bool held)
    {
        _isItemHeldOnCursor = held;
        if (_scrollRect != null)
            _scrollRect.enabled = !held;
    }

    private void HandleSlotClicked(InventorySlotView view, PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (_model == null)
        {
            Debug.LogWarning("[InventoryPresenter] 인벤 데이터가 연결되지 않아 아이템을 넣을 수 없습니다.", this);
            return;
        }

        SlotRef slot = SlotRef.FromContainer(_model, view.SlotIndex);
        Keyboard keyboard = Keyboard.current;
        bool shift = keyboard != null && keyboard.leftShiftKey.isPressed;
        TransferResult result = shift ? ItemTransfer.QuickMove(slot) : ItemTransfer.Click(slot);
        if (result == TransferResult.Rejected)
            view.FlashReject();
    }

    private void HandlePointerEntered(InventorySlotView view)
    {
        if (_model == null)
            return;

        _hoveredSlot = SlotRef.FromContainer(_model, view.SlotIndex);
        ItemTransfer.SetHovered(_hoveredSlot);
    }

    private void HandlePointerExited(InventorySlotView view)
    {
        if (_model == null)
            return;

        ItemTransfer.ClearHovered(SlotRef.FromContainer(_model, view.SlotIndex));
        if (_hoveredSlot.Container == _model && _hoveredSlot.ContainerIndex == view.SlotIndex)
            _hoveredSlot = SlotRef.None;
    }
}
