using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one inventory cell. No stacking, no Model writes.
/// Does not implement IScrollHandler — wheel bubbles to the parent ScrollRect.
/// </summary>
public sealed class InventorySlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TMPro.TextMeshProUGUI _countText;

    [SerializeField, Tooltip("넣을 수 없을 때 칸이 바뀌는 색.")]
    private Color _rejectColor = new Color(1f, 0.25f, 0.25f, 1f);

    [SerializeField, Min(0.05f), Tooltip("거절 깜박임이 한 번 왕복하는 시간(초).")]
    private float _flashDuration = 0.35f;

    /// <summary>Stable index assigned by the Presenter. -1 until bound.</summary>
    public int SlotIndex { get; private set; } = -1;

    /// <summary>True after <see cref="BindIndex"/>.</summary>
    public bool IsBound { get; private set; }

    Image _frame;
    Color _restColor = Color.white;
    Coroutine _flashRoutine;

    /// <summary>Left-click (and other buttons — Presenter filters).</summary>
    public event Action<InventorySlotView, PointerEventData> Clicked;

    /// <summary>Pointer entered this cell.</summary>
    public event Action<InventorySlotView> PointerEntered;

    /// <summary>Pointer left this cell.</summary>
    public event Action<InventorySlotView> PointerExited;

    private void Awake()
    {
        // The cell itself must receive the click. Child images can sit on a nested Canvas and never hit this view.
        _frame = GetComponent<Image>();
        if (_frame != null)
        {
            _frame.raycastTarget = true;
            _restColor = _frame.color;
        }

        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].gameObject != gameObject)
                canvases[i].enabled = false;
        }

        GraphicRaycaster[] raycasters = GetComponentsInChildren<GraphicRaycaster>(true);
        for (int i = 0; i < raycasters.Length; i++)
        {
            if (raycasters[i].gameObject != gameObject)
                raycasters[i].enabled = false;
        }
    }

    /// <summary>Presenter assigns a stable index when the view is created or reused.</summary>
    public void BindIndex(int index)
    {
        SlotIndex = index;
        IsBound = true;
        name = $"InventorySlot_{index:D3}";
    }

    /// <summary>Paints icon and stack count. Empty slots show no icon.</summary>
    public void Render(ItemData item, int amount)
    {
        bool has = item != null && amount > 0;
        if (_iconImage != null)
        {
            _iconImage.sprite = has ? item.icon : null;
            _iconImage.enabled = has;
        }

        if (_countText != null)
        {
            bool showCount = has && item.Stackable && amount > 1;
            _countText.gameObject.SetActive(showCount);
            if (showCount)
                _countText.text = amount.ToString();
        }
    }

    /// <summary>넣을 수 없는 아이템일 때 칸을 붉게 깜박인다.</summary>
    public void FlashReject()
    {
        if (!isActiveAndEnabled || _frame == null)
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
    }

    /// <inheritdoc />
    public void OnPointerExit(PointerEventData eventData)
    {
        PointerExited?.Invoke(this);
    }

    IEnumerator FlashRoutine()
    {
        float half = _flashDuration * 0.5f;
        _frame.color = _rejectColor;
        yield return new WaitForSeconds(half);
        _frame.color = _restColor;
        yield return new WaitForSeconds(half * 0.5f);
        _frame.color = _rejectColor;
        yield return new WaitForSeconds(half * 0.5f);
        _frame.color = _restColor;
        _flashRoutine = null;
    }
}
