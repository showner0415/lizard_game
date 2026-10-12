using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 창고와 상자가 같이 쓰는 칸 창. 모델은 호출하는 쪽이 <see cref="BindModel"/>로 넘긴다.
/// 가로 칸 수는 이 컴포넌트 설정이다.
/// </summary>
public sealed class ContainerPresenter : MonoBehaviour
{
    /// <summary>칸마다 오브젝트를 만들어도 되는 상한.</summary>
    public const int SupportedMaxSlots = 200;

    static readonly List<ContainerPresenter> Active = new List<ContainerPresenter>();

    /// <summary>지금 켜져 있고 모델에 구독 중인 창. 여러 개면 가장 나중에 켜진 것.</summary>
    public static InventoryModel OpenModel
    {
        get
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                ContainerPresenter presenter = Active[i];
                if (presenter._model != null && presenter._subscribed)
                    return presenter._model;
            }

            return null;
        }
    }

    [Header("View wiring")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _content;
    [SerializeField] private InventorySlotView _slotPrefab;
    [SerializeField] private float _scrollSensitivity = 30f;

    [SerializeField, Min(1), Tooltip("이 창의 가로 칸 수. 창고와 상자가 달라도 된다.")]
    private int _columnCount = 5;

    readonly List<InventorySlotView> _views = new List<InventorySlotView>(64);
    InventoryModel _model;
    bool _subscribed;
    SlotRef _hoveredSlot;
    bool _isItemHeldOnCursor;

    /// <summary>연결된 모델. 없으면 null.</summary>
    public InventoryModel Model => _model;

    /// <summary>칸을 그릴 준비가 됐으면 true.</summary>
    public bool IsDriving => _model != null && _slotPrefab != null && _content != null;

    /// <summary>이 창에 모델을 연결한다. 같은 모델이면 다시 그리지 않는다.</summary>
    public void BindModel(InventoryModel model)
    {
        if (model == _model && _subscribed)
            return;

        Unsubscribe();
        _model = model;
        if (isActiveAndEnabled && _model != null)
            SubscribeAndSync();
    }

    /// <summary>연결을 끊는다. 모델의 아이템은 그대로다.</summary>
    public void Unbind()
    {
        Unsubscribe();
        _model = null;
        ItemTransfer.ClearHovered(_hoveredSlot);
        _hoveredSlot = SlotRef.None;
    }

    void OnEnable()
    {
        if (!Active.Contains(this))
            Active.Add(this);

        ApplyScrollDefaults();
        if (MouseItemUI.Instance != null)
            MouseItemUI.Instance.HeldItemChanged += SetItemHeldOnCursor;
        SetItemHeldOnCursor(MouseItemUI.Instance != null && MouseItemUI.Instance.HasItem());
        if (_model != null)
            SubscribeAndSync();
    }

    void OnDisable()
    {
        if (MouseItemUI.Instance != null)
            MouseItemUI.Instance.HeldItemChanged -= SetItemHeldOnCursor;

        Unsubscribe();
        UnbindViewEvents();
        ItemTransfer.ClearHovered(_hoveredSlot);
        _hoveredSlot = SlotRef.None;
        SetItemHeldOnCursor(false);
        Active.Remove(this);
    }

    void OnDestroy()
    {
        Unsubscribe();
        UnbindViewEvents();
        ItemTransfer.ClearHovered(_hoveredSlot);
        _hoveredSlot = SlotRef.None;
        _model = null;
        Active.Remove(this);
    }

    void OnValidate()
    {
        if (_columnCount < 1)
            _columnCount = 1;
    }

    void ApplyScrollDefaults()
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

    void ApplyColumnCount()
    {
        if (_columnCount < 1)
        {
            Debug.LogWarning($"[ContainerPresenter] 가로 칸 수 {_columnCount}는 1로 맞춥니다.", this);
            _columnCount = 1;
        }

        if (_content == null)
            return;

        GridLayoutGroup grid = _content.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            Debug.LogWarning("[ContainerPresenter] Content에 GridLayoutGroup이 없습니다.", this);
            return;
        }

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = _columnCount;
    }

    void SubscribeAndSync()
    {
        if (_model == null || _subscribed)
            return;

        _model.OnSlotChanged += HandleSlotChanged;
        _model.OnCapacityChanged += HandleCapacityChanged;
        _model.OnWarning += HandleModelWarning;
        _subscribed = true;

        if (!IsDriving)
        {
            Debug.LogWarning("[ContainerPresenter] Content 또는 Slot Prefab이 비어 있습니다.", this);
            return;
        }

        ApplyColumnCount();
        RebuildViews(_model.Capacity);
        _model.PublishFullState();
    }

    void Unsubscribe()
    {
        if (!_subscribed || _model == null)
            return;

        _model.OnSlotChanged -= HandleSlotChanged;
        _model.OnCapacityChanged -= HandleCapacityChanged;
        _model.OnWarning -= HandleModelWarning;
        _subscribed = false;
    }

    void HandleModelWarning(string message)
    {
        Debug.LogWarning("[InventoryModel] " + message, this);
    }

    void HandleCapacityChanged(int newCapacity)
    {
        if (!IsDriving)
            return;

        RebuildViews(newCapacity);
        _model.PublishFullState();
    }

    void HandleSlotChanged(int index, ItemData item, int amount)
    {
        if (index < 0 || index >= _views.Count)
            return;
        if (!_views[index].gameObject.activeSelf)
            return;
        _views[index].Render(item, amount);
    }

    void RebuildViews(int capacity)
    {
        if (capacity > SupportedMaxSlots)
            Debug.LogWarning($"[ContainerPresenter] 칸 수 {capacity}가 상한 {SupportedMaxSlots}를 넘습니다.", this);

        while (_views.Count < capacity)
        {
            InventorySlotView view = Instantiate(_slotPrefab, _content);
            view.BindIndex(_views.Count);
            _views.Add(view);
        }

        for (int i = 0; i < _views.Count; i++)
        {
            bool inRange = i < capacity;
            _views[i].gameObject.SetActive(inRange);
            if (!inRange)
                continue;

            _views[i].BindIndex(i);
            _views[i].Clicked -= HandleSlotClicked;
            _views[i].PointerEntered -= HandlePointerEntered;
            _views[i].PointerExited -= HandlePointerExited;
            _views[i].Clicked += HandleSlotClicked;
            _views[i].PointerEntered += HandlePointerEntered;
            _views[i].PointerExited += HandlePointerExited;
        }
    }

    void UnbindViewEvents()
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

    void HandleSlotClicked(InventorySlotView view, PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (_model == null)
        {
            Debug.LogWarning("[ContainerPresenter] 모델이 연결되지 않았습니다.", this);
            return;
        }

        SlotRef slot = SlotRef.FromContainer(_model, view.SlotIndex);
        Keyboard keyboard = Keyboard.current;
        bool shift = keyboard != null && keyboard.leftShiftKey.isPressed;
        TransferResult result = shift ? ItemTransfer.QuickMove(slot) : ItemTransfer.Click(slot);
        if (result == TransferResult.Rejected)
            view.FlashReject();
    }

    void HandlePointerEntered(InventorySlotView view)
    {
        if (_model == null)
            return;

        _hoveredSlot = SlotRef.FromContainer(_model, view.SlotIndex);
        ItemTransfer.SetHovered(_hoveredSlot);
    }

    void HandlePointerExited(InventorySlotView view)
    {
        if (_model == null)
            return;

        ItemTransfer.ClearHovered(SlotRef.FromContainer(_model, view.SlotIndex));
        if (_hoveredSlot.Container == _model && _hoveredSlot.ContainerIndex == view.SlotIndex)
            _hoveredSlot = SlotRef.None;
    }

    void SetItemHeldOnCursor(bool held)
    {
        _isItemHeldOnCursor = held;
        if (_scrollRect != null)
            _scrollRect.enabled = !held;
    }
}
