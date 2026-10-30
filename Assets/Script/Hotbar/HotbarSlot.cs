using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 꼬리 장식 퀵슬롯 한 칸. 아이템을 보관하지 않고 컨트롤러의 참조를 그린다.
/// </summary>
public class HotbarSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IItemTooltipSource
{
    public Image backgroundImage;
    public Image iconImage;

    [SerializeField, Tooltip("등록이 거절될 때 칸이 바뀌는 색.")]
    Color _rejectColor = new Color(1f, 0.25f, 0.25f, 1f);

    [SerializeField, Min(0.05f), Tooltip("거절 깜박임이 한 번 왕복하는 시간(초).")]
    float _flashDuration = 0.35f;

    const float DimmedAlpha = 0.35f;

    Coroutine _flashRoutine;
    Color _restColor = Color.white;
    int _index = -1;
    bool _listening;

    /// <summary>이 칸에 등록된 꼬리 장식. 없으면 null.</summary>
    public ItemData TooltipItem
    {
        get
        {
            if (HotbarController.Instance == null || _index < 0)
                return null;

            return HotbarController.Instance.Get(_index);
        }
    }

    /// <summary>컨트롤러가 이 뷰의 칸 번호를 알려 준다.</summary>
    public void Bind(int index)
    {
        _index = index;
        Subscribe();
        Refresh();
    }

    /// <summary>아이콘, 흐림, 장착 테두리를 다시 그린다.</summary>
    public void Refresh()
    {
        HotbarController controller = HotbarController.Instance;
        TailOrnamentItemData ornament = controller != null && _index >= 0 ? controller.Get(_index) : null;
        bool available = ornament != null && controller.IsAvailable(_index);
        bool equipped = ornament != null && controller.IsEquipped(_index);

        if (iconImage != null)
        {
            iconImage.sprite = ornament != null ? ornament.icon : null;
            iconImage.enabled = ornament != null && ornament.icon != null;
            Color color = iconImage.color;
            color.a = ornament != null && !available ? DimmedAlpha : 1f;
            iconImage.color = color;
        }

        if (backgroundImage != null && controller != null)
        {
            backgroundImage.sprite = equipped ? controller.selectedSprite : controller.normalSprite;
        }
    }

    /// <summary>등록이 거절됐을 때 칸을 붉게 깜박인다.</summary>
    public void FlashReject()
    {
        if (!isActiveAndEnabled || backgroundImage == null)
            return;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    void Awake()
    {
        if (backgroundImage != null)
            _restColor = backgroundImage.color;
    }

    void OnEnable()
    {
        if (_index < 0 && HotbarController.Instance != null)
            _index = HotbarController.Instance.IndexOf(this);

        Subscribe();
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
        ItemTooltip.ClearHover(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HotbarController controller = HotbarController.Instance;
        if (controller == null || _index < 0)
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            controller.TryAssign(_index, null);
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        MouseItemUI mouseItem = MouseItemUI.Instance;
        if (mouseItem == null || !mouseItem.HasItem())
            return;

        if (mouseItem.GetItem() is TailOrnamentItemData ornament && controller.TryAssign(_index, ornament))
        {
            ItemTransfer.ReturnHeld();
            return;
        }

        FlashReject();
    }

    /// <inheritdoc />
    public void OnPointerEnter(PointerEventData eventData)
    {
        ItemTooltip.SetHover(this);
    }

    /// <inheritdoc />
    public void OnPointerExit(PointerEventData eventData)
    {
        ItemTooltip.ClearHover(this);
    }

    void Subscribe()
    {
        HotbarController controller = HotbarController.Instance;
        if (controller == null || _listening)
            return;

        controller.SlotChanged += HandleSlotChanged;
        controller.AvailabilityChanged += Refresh;
        _listening = true;
    }

    void Unsubscribe()
    {
        if (!_listening)
            return;

        _listening = false;
        HotbarController controller = HotbarController.Instance;
        if (controller == null)
            return;

        controller.SlotChanged -= HandleSlotChanged;
        controller.AvailabilityChanged -= Refresh;
    }

    void HandleSlotChanged(int index)
    {
        if (index == _index)
            Refresh();
    }

    IEnumerator FlashRoutine()
    {
        float half = _flashDuration * 0.5f;
        backgroundImage.color = _rejectColor;
        yield return new WaitForSeconds(half);
        backgroundImage.color = _restColor;
        yield return new WaitForSeconds(half * 0.5f);
        backgroundImage.color = _rejectColor;
        yield return new WaitForSeconds(half * 0.5f);
        backgroundImage.color = _restColor;
        _flashRoutine = null;
    }
}
