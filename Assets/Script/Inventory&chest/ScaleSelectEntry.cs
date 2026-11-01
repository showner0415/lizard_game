using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 비늘 선택 창의 항목 하나. 클릭과 툴팁은 루트 Image의 포인터 이벤트로 받는다.
/// </summary>
public sealed class ScaleSelectEntry : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IItemTooltipSource
{
    [SerializeField, Tooltip("ScaleData.Icon을 그린다.")]
    Image _icon;

    [SerializeField, Tooltip("ScaleData.DisplayName.")]
    TMP_Text _name;

    [SerializeField, Tooltip("파밍 비늘 수량. 기본 비늘 항목에서는 숨긴다.")]
    TMP_Text _count;

    [SerializeField, Tooltip("선택된 항목만 켠다. 비워도 배경색으로 구분한다.")]
    GameObject _selectedMark;

    [SerializeField, Tooltip("마우스가 올라가 있는 동안만 켠다. 없어도 된다.")]
    GameObject _hoverMark;

    [SerializeField, Tooltip("선택 여부에 따라 색을 바꾼다. 비우면 색은 바꾸지 않는다.")]
    Image _background;

    [SerializeField, Tooltip("선택된 항목의 배경색.")]
    Color _selectedColor = new Color(1f, 0.86f, 0.45f, 1f);

    ScaleItemData _item;
    Color _normalColor;
    bool _rememberedColor;
    bool _bound;
    Action _onClick;

    /// <inheritdoc />
    public ItemData TooltipItem => _item;

    void Awake()
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null || !graphic.raycastTarget)
            Debug.LogWarning("[ScaleSelectEntry] 루트에 Raycast Target이 켜진 Image가 없어 클릭을 받을 수 없습니다.", this);
    }

    /// <summary>이 항목이 보여줄 비늘과 클릭 동작을 넣는다. 기본 비늘이면 <paramref name="item"/>은 null.</summary>
    public void Bind(ScaleData scale, ScaleItemData item, int count, bool showCount, bool selected, Action onClick)
    {
        _item = item;
        _bound = true;
        _onClick = onClick;
        SetHoverMark(false);

        if (_icon != null)
        {
            _icon.sprite = scale != null ? scale.Icon : null;
            _icon.enabled = _icon.sprite != null;
        }

        if (_name != null)
            _name.text = scale != null ? scale.DisplayName : string.Empty;

        if (_count != null)
        {
            _count.gameObject.SetActive(showCount);
            if (showCount)
                _count.text = count.ToString();
        }

        if (_selectedMark != null)
            _selectedMark.SetActive(selected);

        if (_background != null)
        {
            if (!_rememberedColor)
            {
                _normalColor = _background.color;
                _rememberedColor = true;
            }

            _background.color = selected ? _selectedColor : _normalColor;
        }
    }

    /// <inheritdoc />
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_bound || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (_onClick != null)
            _onClick.Invoke();
    }

    /// <inheritdoc />
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_bound)
            return;

        SetHoverMark(true);
        ItemTooltip.SetHover(this);
    }

    /// <inheritdoc />
    public void OnPointerExit(PointerEventData eventData)
    {
        SetHoverMark(false);
        ItemTooltip.ClearHover(this);
    }

    void OnDisable()
    {
        SetHoverMark(false);
        ItemTooltip.ClearHover(this);
    }

    void SetHoverMark(bool visible)
    {
        if (_hoverMark != null)
            _hoverMark.SetActive(visible);
    }
}
