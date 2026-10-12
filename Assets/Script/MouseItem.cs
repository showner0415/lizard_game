using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MouseItemUI : MonoBehaviour
{
    public static MouseItemUI Instance;

    [SerializeField] private Image iconImage;

    private RectTransform rect;
    private ItemData currentItem;
    private int amount;

    /// <summary>집은 칸. 인벤, 장비, 상자 중 하나만 기억한다.</summary>
    private SlotRef _origin;

    /// <summary>커서의 아이템이 비워진 뒤. 핫바 같은 구독자가 소지를 다시 확인한다.</summary>
    public event Action OnHeldItemCleared;

    /// <summary>커서에 아이템이 생기거나 사라질 때. 인자는 지금 들고 있는지.</summary>
    public event Action<bool> HeldItemChanged;

    readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
    PointerEventData _pointerData;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        rect = GetComponent<RectTransform>();
        this.transform.SetAsLastSibling();
        Clear();
    }

    private void Update()
    {
        if (currentItem != null)
        {
            if (Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            rect.position = mousePos;

            // ✅ 요구사항 1: UI 밖 좌클릭으로 아이템 드롭
            HandleDropOnClickOutsideUI();
        }

        // ✅ 요구사항 2: Q키로 아이템 드롭
        HandleDropWithQKey();
    }

    /// <summary>
    /// UI 밖을 클릭했을 때 아이템 드롭
    /// </summary>
    private void HandleDropOnClickOutsideUI()
    {
        if (Mouse.current == null)
            return;

        // 좌클릭 체크
        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // UI 위에 있는지 체크 (모든 입력 장치 지원)
        if (IsPointerOverUI())
            return;

        // UI 밖을 클릭했으므로 아이템 드롭
        Debug.Log($"[MouseItemUI] UI 밖 클릭 감지 - '{currentItem.name}' 드롭");
        ItemTransfer.DropHeld();
    }

    /// <summary>
    /// Q키로 아이템 드롭
    /// </summary>
    private void HandleDropWithQKey()
    {
        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.qKey.wasPressedThisFrame)
            return;

        ItemTransfer.DropAtPointer();
    }

    /// <summary>
    /// 마우스 포인터가 UI 위에 있는지 확인 (모든 입력 장치 지원)
    /// </summary>
    private bool IsPointerOverUI()
    {
        // EventSystem이 없으면 false
        if (EventSystem.current == null)
            return false;

        // 마우스 입력 체크
        if (Mouse.current != null)
            return RaycastUI(Mouse.current.position.ReadValue());

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return RaycastUI(Touchscreen.current.primaryTouch.position.ReadValue());

        return false;
    }

    bool RaycastUI(Vector2 screenPosition)
    {
        if (_pointerData == null)
            _pointerData = new PointerEventData(EventSystem.current);

        _pointerData.position = screenPosition;
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(_pointerData, _raycastResults);
        return _raycastResults.Count > 0;
    }

    public bool HasItem()
    {
        return currentItem != null;
    }

    /// <summary>집은 칸.</summary>
    public SlotRef Origin => _origin;

    /// <summary>집은 위치를 하나만 기억한다.</summary>
    public void SetOrigin(SlotRef origin) => _origin = origin;

    public ItemData GetItem() => currentItem;
    public int GetAmount() => amount;

    public void SetItem(ItemData item, int count)
    {
        currentItem = item;
        amount = count;

        if (item != null)
        {
            iconImage.sprite = item.icon;
            iconImage.enabled = true;
            HeldItemChanged?.Invoke(true);
        }
        else
        {
            Clear();
        }
    }

    public void Clear()
    {
        currentItem = null;
        amount = 0;
        _origin = SlotRef.None;
        iconImage.sprite = null;
        iconImage.enabled = false;
        OnHeldItemCleared?.Invoke();
        HeldItemChanged?.Invoke(false);
    }

}