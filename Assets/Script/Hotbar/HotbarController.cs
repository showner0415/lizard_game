using UnityEngine;
using UnityEngine.InputSystem;

public class HotbarController : MonoBehaviour
{
    public static HotbarController Instance;

    public Sprite normalSprite;
    public Sprite selectedSprite;

    InventoryModel _inventory;
    MouseItemUI _mouse;
    bool _checksEnabled;
    bool _prunePending;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        SelectSlot(0);
    }

    private void OnEnable()
    {
        BindInventory();
        BindMouse();
        RequestPrune();
    }

    private void Start()
    {
        BindInventory();
        BindMouse();
    }

    private void OnDisable()
    {
        UnbindInventory();
        UnbindMouse();
    }

    private void LateUpdate()
    {
        if (!_checksEnabled)
        {
            _checksEnabled = true;
            _prunePending = true;
        }

        if (!_prunePending)
            return;

        _prunePending = false;
        RemoveBooksNoLongerHeld();
    }

    public HotbarSlot[] slots = new HotbarSlot[5];
    public int currentIndex = 0;
    public int beforeIndex;

    /// <summary>선택된 칸의 마법. 칸이 비었으면 null.</summary>
    public MagicData CurrentMagic =>
        slots[currentIndex].Magic;

    public void OnHotbar1(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SelectSlot(0);
    }

    public void OnHotbar2(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SelectSlot(1);
    }

    public void OnHotbar3(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SelectSlot(2);
    }

    public void OnHotbar4(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SelectSlot(3);
    }

    public void OnHotbar5(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SelectSlot(4);
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= slots.Length)
            return;
        beforeIndex = currentIndex;
        slots[beforeIndex].SetSelected(false);
        currentIndex = index;
        slots[currentIndex].SetSelected(true);
    }

    /// <summary>지정한 칸에 마법서를 넣는다.</summary>
    public void SetBookToSlot(int index, MagicBookItemData book)
    {
        slots[index].SetBook(book);
    }

    public void CaptureState(SaveData data)
    {
        if (data == null || slots == null)
            return;

        data.hotbar = new int[slots.Length];
        for (int i = 0; i < slots.Length; i++)
            data.hotbar[i] = slots[i] != null && slots[i].book != null
                ? slots[i].book.itemNum
                : -1;
    }

    public void RestoreState(SaveData data)
    {
        BindInventory();

        if (data == null || slots == null)
            return;

        if (data.hotbar == null)
            return;

        ItemDatabase db = SaveManager.Instance != null ? SaveManager.Instance.Database : null;
        if (db == null)
        {
            Debug.LogWarning("[HotbarController] ItemDatabase가 없어 핫바를 복원하지 않습니다.");
            return;
        }

        int count = Mathf.Min(slots.Length, data.hotbar.Length);
        for (int i = 0; i < count; i++)
        {
            int savedItemNum = data.hotbar[i];

            if (savedItemNum < 0)
            {
                slots[i].SetBook(null);
                continue;
            }

            MagicBookItemData book = db.Get(savedItemNum) as MagicBookItemData;
            if (book == null)
            {
                Debug.LogWarning($"[HotbarController] 슬롯 {i} 복원 실패: itemNum {savedItemNum}은 마법서가 아닙니다. 칸을 비웁니다.");
                slots[i].SetBook(null);
                continue;
            }

            slots[i].SetBook(book);
        }

        RequestPrune();
    }

    void BindInventory()
    {
        InventoryModel model = InventoryController.Instance != null ? InventoryController.Instance.Inventory : null;
        if (model == _inventory)
            return;

        UnbindInventory();
        _inventory = model;
        if (_inventory != null)
            _inventory.OnSlotChanged += HandleInventorySlotChanged;
    }

    void UnbindInventory()
    {
        if (_inventory == null)
            return;

        _inventory.OnSlotChanged -= HandleInventorySlotChanged;
        _inventory = null;
    }

    void BindMouse()
    {
        MouseItemUI mouse = MouseItemUI.Instance;
        if (mouse == _mouse)
            return;

        UnbindMouse();
        _mouse = mouse;
        if (_mouse != null)
            _mouse.OnHeldItemCleared += RequestPrune;
    }

    void UnbindMouse()
    {
        if (_mouse == null)
            return;

        _mouse.OnHeldItemCleared -= RequestPrune;
        _mouse = null;
    }

    void HandleInventorySlotChanged(int index, ItemData item, int amount)
    {
        RequestPrune();
    }

    void RequestPrune()
    {
        _prunePending = true;
    }

    /// <summary>
    /// 인벤과 커서에 없는 마법서를 핫바에서 뺀다.
    /// 프레임 안의 중간 상태를 보지 않도록 LateUpdate에서만 호출한다.
    /// </summary>
    void RemoveBooksNoLongerHeld()
    {
        if (!_checksEnabled || slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            HotbarSlot slot = slots[i];
            if (slot == null || slot.book == null)
                continue;

            if (!IsBookStillHeld(slot.book.itemNum))
                slot.SetBook(null);
        }
    }

    static bool IsBookStillHeld(int itemNum)
    {
        InventoryModel inventory = InventoryController.Instance != null ? InventoryController.Instance.Inventory : null;
        if (inventory != null)
        {
            for (int i = 0; i < inventory.Capacity; i++)
            {
                ItemStack stack = inventory.Peek(i);
                if (stack != null && stack.HasItem && stack.item.itemNum == itemNum)
                    return true;
            }
        }

        MouseItemUI mouse = MouseItemUI.Instance;
        return mouse != null && mouse.HasItem() && mouse.GetItem().itemNum == itemNum;
    }
}
