using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>장비칸 하나. 아이콘과 종류 글씨만 그린다. 모델 쓰기는 Presenter가 한다.</summary>
public sealed class EquipmentSlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IItemTooltipSource
{
    [SerializeField] private Image _frameImage;
    [SerializeField] private Image _iconImage;
    [SerializeField] private TMPro.TextMeshProUGUI _label;
    [SerializeField] private Color _rejectColor = new Color(1f, 0.25f, 0.25f, 1f);

    [SerializeField, Min(0.05f)] private float _flashDuration = 0.35f;

    /// <summary>이 칸의 장비 종류.</summary>
    public EquipmentSlotType SlotType { get; private set; }

    /// <summary>Presenter가 종류를 붙인 뒤 true.</summary>
    public bool IsBound { get; private set; }

    /// <summary>좌클릭.</summary>
    public event Action<EquipmentSlotView, PointerEventData> Clicked;

    /// <summary>포인터가 들어옴.</summary>
    public event Action<EquipmentSlotView> PointerEntered;

    /// <summary>포인터가 나감.</summary>
    public event Action<EquipmentSlotView> PointerExited;

    private Color _restColor = Color.white;
    private Coroutine _flashRoutine;
    EquipmentModel _model;

    /// <summary>이 칸이 지금 장비 모델에서 읽고 있는 아이템. 비었으면 null.</summary>
    public ItemData TooltipItem => _model != null && IsBound ? _model.Get(SlotType) : null;

    private void Awake()
    {
        if (_frameImage != null)
            _restColor = _frameImage.color;
    }

    /// <summary>종류와 위에 붙는 글씨를 정한다.</summary>
    public void Bind(EquipmentSlotType slotType, string label)
    {
        Bind(slotType, label, _model);
    }

    /// <summary>종류 글씨와, 아이템을 물어볼 장비 모델을 붙인다.</summary>
    public void Bind(EquipmentSlotType slotType, string label, EquipmentModel model)
    {
        SlotType = slotType;
        _model = model;
        IsBound = true;
        name = $"EquipmentSlot_{slotType}";
        if (_label != null)
            _label.text = label;
    }

    /// <summary>아이콘만 그린다. 장비는 수량을 표시하지 않는다.</summary>
    public void Render(ItemData item)
    {
        bool has = item != null;
        if (_iconImage == null)
            return;

        _iconImage.sprite = has ? item.icon : null;
        _iconImage.enabled = has;
    }

    /// <summary>넣을 수 없는 아이템을 놓았을 때 칸을 붉게 깜박인다.</summary>
    public void FlashReject()
    {
        if (!isActiveAndEnabled || _frameImage == null)
            return;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    /// <inheritdoc />
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsBound)
            return;
        Clicked?.Invoke(this, eventData);
    }

    /// <inheritdoc />
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsBound)
            return;
        PointerEntered?.Invoke(this);
        ItemTooltip.SetHover(this);
    }

    /// <inheritdoc />
    public void OnPointerExit(PointerEventData eventData)
    {
        PointerExited?.Invoke(this);
        ItemTooltip.ClearHover(this);
    }

    void OnDisable()
    {
        ItemTooltip.ClearHover(this);
    }

    private IEnumerator FlashRoutine()
    {
        float half = _flashDuration * 0.5f;
        _frameImage.color = _rejectColor;
        yield return new WaitForSeconds(half);
        _frameImage.color = _restColor;
        yield return new WaitForSeconds(half * 0.5f);
        _frameImage.color = _rejectColor;
        yield return new WaitForSeconds(half * 0.5f);
        _frameImage.color = _restColor;
        _flashRoutine = null;
    }
}
