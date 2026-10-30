using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 장비칸 UI. 칸 프리팹과 Content는 인스펙터에서 연결한다.
/// 데이터는 <see cref="EquipmentModel"/>만 읽고 쓴다.
/// </summary>
public sealed class EquipmentPresenter : MonoBehaviour
{
    [Header("View wiring")]
    [Tooltip("장비칸을 둘 부모. Grid Layout Group을 이 오브젝트에 두면 2x2가 된다.")]
    [SerializeField] private RectTransform _content;

    [Tooltip("EquipmentSlotView가 있는 칸 프리팹. 아이콘·라벨·테두리는 프리팹에서 연결한다.")]
    [SerializeField] private EquipmentSlotView _slotPrefab;

    private readonly EquipmentSlotView[] _views = new EquipmentSlotView[EquipmentModel.SlotCount];
    private EquipmentModel _model;
    private IScaleMagazine _magazine;
    private bool _subscribed;
    private EquipmentSlotType? _hovered;

    /// <summary>연결된 장비 데이터.</summary>
    public EquipmentModel Model => _model;

    /// <summary>프리팹과 Content가 있고 모델이 연결된 상태.</summary>
    public bool IsDriving => _model != null && _slotPrefab != null && _content != null;

    /// <summary>모델을 연결한다. 씬을 다시 열어도 안전하다.</summary>
    public void BindModel(EquipmentModel model)
    {
        UnsubscribeModel();
        _model = model;
        if (isActiveAndEnabled)
            SubscribeAndSync();
    }

    private void OnEnable()
    {
        if (_model == null && InventoryController.Instance != null)
            _model = InventoryController.Instance.Equipment;

        if (_model != null)
            SubscribeAndSync();

        SubscribeMagazine();
    }

    private void OnDisable()
    {
        UnsubscribeMagazine();
        UnsubscribeModel();
        UnbindViewEvents();
        if (_hovered != null)
            ItemTransfer.ClearHovered(SlotRef.FromEquipment(_hovered.Value));
        _hovered = null;
    }

    private void SubscribeAndSync()
    {
        if (_model == null || _subscribed)
            return;

        _model.OnEquipmentChanged += HandleEquipmentChanged;
        _subscribed = true;

        if (!IsDriving)
        {
            Debug.LogWarning("[EquipmentPresenter] Content 또는 Slot Prefab이 비어 있습니다. Inspector에서 연결하세요.", this);
            return;
        }

        RebuildViews();
        _model.PublishFullState();
        if (_magazine == null)
            SubscribeMagazine();
        RenderLoadedScale();
    }

    private void UnsubscribeModel()
    {
        if (!_subscribed || _model == null)
            return;

        _model.OnEquipmentChanged -= HandleEquipmentChanged;
        _subscribed = false;
    }

    private void HandleEquipmentChanged(EquipmentSlotType slot, ItemData item)
    {
        if (slot == EquipmentSlotType.LoadedScale)
            return;

        int index = (int)slot;
        if (index < 0 || index >= _views.Length || _views[index] == null)
            return;
        _views[index].Render(item);
    }

    void SubscribeMagazine()
    {
        UnsubscribeMagazine();
        if (PlayerRoot.Instance == null)
            return;

        ScaleLoader loader = PlayerRoot.Instance.GetComponentInChildren<ScaleLoader>(true);
        if (loader == null)
            return;

        _magazine = loader.Magazine;
        if (_magazine == null)
            return;

        _magazine.SlotsChanged += HandleSlotsChanged;
        RenderLoadedScale();
    }

    void UnsubscribeMagazine()
    {
        if (_magazine == null)
            return;

        _magazine.SlotsChanged -= HandleSlotsChanged;
        _magazine = null;
    }

    void HandleSlotsChanged()
    {
        RenderLoadedScale();
    }

    void RenderLoadedScale()
    {
        int index = (int)EquipmentSlotType.LoadedScale;
        if (index < 0 || index >= _views.Length || _views[index] == null)
            return;

        Sprite icon = null;
        int count = 0;
        if (_magazine != null)
        {
            ScaleData scale = _magazine.CurrentScale;
            if (scale != null)
                icon = scale.Icon;
            count = _magazine.LoadedCount + _magazine.ReservedCount;
        }

        _views[index].RenderLoadedScale(icon, count);
    }

    private void HandleSlotClicked(EquipmentSlotView view, PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (InventoryController.Instance == null || InventoryController.Instance.Equipment == null)
            return;

        Keyboard keyboard = Keyboard.current;
        bool shift = keyboard != null && keyboard.leftShiftKey.isPressed;
        SlotRef slot = SlotRef.FromEquipment(view.SlotType);
        TransferResult result = shift ? ItemTransfer.QuickMove(slot) : ItemTransfer.Click(slot);
        if (result == TransferResult.Rejected)
            view.FlashReject();
    }

    private void HandlePointerEntered(EquipmentSlotView view)
    {
        _hovered = view.SlotType;
        ItemTransfer.SetHovered(SlotRef.FromEquipment(view.SlotType));
    }

    private void HandlePointerExited(EquipmentSlotView view)
    {
        ItemTransfer.ClearHovered(SlotRef.FromEquipment(view.SlotType));
        if (_hovered == view.SlotType)
            _hovered = null;
    }

    void RebuildViews()
    {
        UnbindViewEvents();

        for (int i = 0; i < EquipmentModel.SlotCount; i++)
        {
            if (_views[i] == null)
                _views[i] = Instantiate(_slotPrefab, _content);

            EquipmentSlotType slot = (EquipmentSlotType)i;
            _views[i].Bind(slot, LabelFor(slot), _model);
            _views[i].Clicked += HandleSlotClicked;
            _views[i].PointerEntered += HandlePointerEntered;
            _views[i].PointerExited += HandlePointerExited;
        }
    }

    private void UnbindViewEvents()
    {
        for (int i = 0; i < _views.Length; i++)
        {
            if (_views[i] == null)
                continue;
            _views[i].Clicked -= HandleSlotClicked;
            _views[i].PointerEntered -= HandlePointerEntered;
            _views[i].PointerExited -= HandlePointerExited;
        }
    }

    private static string LabelFor(EquipmentSlotType slot)
    {
        switch (slot)
        {
            case EquipmentSlotType.LoadedScale: return "장전";
            case EquipmentSlotType.Armor: return "갑옷";
            case EquipmentSlotType.Tail: return "꼬리 장식";
            case EquipmentSlotType.Bag: return "가방";
            default: return slot.ToString();
        }
    }
}
