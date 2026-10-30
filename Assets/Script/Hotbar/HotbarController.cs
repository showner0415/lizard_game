using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 꼬리 장식 퀵슬롯. 아이템을 보관하지 않고 참조만 가진다.
/// 숫자 키는 그 장식을 인벤에서 빼 꼬리 칸에 끼운다.
/// </summary>
public class HotbarController : MonoBehaviour
{
    public static HotbarController Instance;

    public Sprite normalSprite;
    public Sprite selectedSprite;

    public HotbarSlot[] slots = new HotbarSlot[5];

    TailOrnamentItemData[] _slots = new TailOrnamentItemData[5];
    InventoryModel _inventory;
    EquipmentModel _equipment;

    /// <summary>퀵슬롯 참조가 바뀌면 칸 번호와 함께 발생한다.</summary>
    public event Action<int> SlotChanged;

    /// <summary>인벤이나 꼬리 칸이 바뀌어 장착 가능 여부가 달라졌을 때 발생한다.</summary>
    public event Action AvailabilityChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        int count = slots != null && slots.Length > 0 ? slots.Length : 5;
        _slots = new TailOrnamentItemData[count];
    }

    void OnEnable()
    {
        BindInventory();
        BindEquipment();
        BindViews();
    }

    void Start()
    {
        BindInventory();
        BindEquipment();
        BindViews();
    }

    void OnDisable()
    {
        UnbindInventory();
        UnbindEquipment();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>그 칸의 꼬리 장식. 없거나 범위 밖이면 null.</summary>
    public TailOrnamentItemData Get(int index)
    {
        if (!IsValid(index))
            return null;

        return _slots[index];
    }

    /// <summary>칸에 장식 참조를 넣는다. null이면 비운다. 다른 칸에 같은 장식이 있으면 그 칸은 비운다.</summary>
    public bool TryAssign(int index, TailOrnamentItemData item)
    {
        if (!IsValid(index))
            return false;

        if (item != null)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i == index || _slots[i] != item)
                    continue;

                _slots[i] = null;
                SlotChanged?.Invoke(i);
            }
        }

        if (_slots[index] == item)
            return true;

        _slots[index] = item;
        SlotChanged?.Invoke(index);
        return true;
    }

    /// <summary>숫자 키. 그 장식을 인벤에서 빼 꼬리 칸에 끼운다. 이미 끼워져 있으면 true.</summary>
    public bool TryEquipSlot(int index)
    {
        if (!isActiveAndEnabled || !IsValid(index))
            return false;

        TailOrnamentItemData item = _slots[index];
        if (item == null || IsBlocked())
            return false;

        if (IsEquipped(index))
            return true;

        InventoryController controller = InventoryController.Instance;
        if (controller == null || controller.Inventory == null || controller.Equipment == null)
            return false;

        if (!controller.Inventory.TryRemove(item, 1))
            return false;

        if (!controller.Equipment.TryEquip(EquipmentSlotType.Tail, item, out ItemData previous))
        {
            controller.AddToInventory(item, 1);
            return false;
        }

        if (previous != null)
            ReturnPrevious(controller, previous);

        return true;
    }

    /// <summary>인벤에 있거나 꼬리 칸에 끼워져 있으면 true. 빈 칸은 false.</summary>
    public bool IsAvailable(int index)
    {
        TailOrnamentItemData item = Get(index);
        if (item == null)
            return false;

        if (IsEquipped(index))
            return true;

        InventoryModel inventory = Inventory();
        return inventory != null && inventory.CountOf(item) > 0;
    }

    /// <summary>이 칸의 장식이 지금 꼬리 칸에 있으면 true.</summary>
    public bool IsEquipped(int index)
    {
        TailOrnamentItemData item = Get(index);
        if (item == null)
            return false;

        InventoryController controller = InventoryController.Instance;
        if (controller == null || controller.Equipment == null)
            return false;

        return controller.Equipment.Get(EquipmentSlotType.Tail) == item;
    }

    /// <summary>퀵슬롯의 장식 번호를 저장한다. 빈 칸은 -1.</summary>
    public void CaptureState(SaveData data)
    {
        if (data == null)
            return;

        data.hotbar = new int[_slots.Length];
        for (int i = 0; i < _slots.Length; i++)
            data.hotbar[i] = _slots[i] != null ? _slots[i].itemNum : -1;
    }

    /// <summary>저장에서 꼬리 장식만 되돌린다. 없거나 다른 종류면 그 칸은 빈다.</summary>
    public void RestoreState(SaveData data)
    {
        BindInventory();
        BindEquipment();

        if (data == null || data.hotbar == null)
            return;

        ItemDatabase database = SaveManager.Instance != null ? SaveManager.Instance.Database : null;
        if (database == null)
        {
            Debug.LogWarning("[HotbarController] ItemDatabase가 없어 퀵슬롯을 복원하지 않습니다.");
            return;
        }

        int count = Mathf.Min(_slots.Length, data.hotbar.Length);
        for (int i = 0; i < count; i++)
        {
            int savedItemNum = data.hotbar[i];
            TailOrnamentItemData ornament = null;
            if (savedItemNum >= 0)
                ornament = database.Get(savedItemNum) as TailOrnamentItemData;

            if (savedItemNum >= 0 && ornament == null)
                Debug.LogWarning($"[HotbarController] 슬롯 {i} 복원 실패: itemNum {savedItemNum}은 꼬리 장식이 아닙니다. 칸을 비웁니다.");

            _slots[i] = ornament;
            SlotChanged?.Invoke(i);
        }

        AvailabilityChanged?.Invoke();
    }

    /// <summary>인스펙터에 연결된 칸 뷰의 번호. 없으면 -1.</summary>
    public int IndexOf(HotbarSlot slot)
    {
        if (slots == null || slot == null)
            return -1;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == slot)
                return i;
        }

        return -1;
    }

    public void OnHotbar1(InputAction.CallbackContext context) => EquipFromKey(context, 0);

    public void OnHotbar2(InputAction.CallbackContext context) => EquipFromKey(context, 1);

    public void OnHotbar3(InputAction.CallbackContext context) => EquipFromKey(context, 2);

    public void OnHotbar4(InputAction.CallbackContext context) => EquipFromKey(context, 3);

    public void OnHotbar5(InputAction.CallbackContext context) => EquipFromKey(context, 4);

    void EquipFromKey(InputAction.CallbackContext context, int index)
    {
        if (!isActiveAndEnabled || !context.performed)
            return;

        TryEquipSlot(index);
    }

    void ReturnPrevious(InventoryController controller, ItemData previous)
    {
        int remain = controller.AddToInventory(previous, 1);
        if (remain <= 0)
            return;

        bool dropped = false;
        if (ItemDropManager.Instance != null)
            dropped = ItemDropManager.Instance.DropItemStack(previous, remain, transform);

        if (!dropped)
            Debug.LogWarning($"[HotbarController] '{previous.name}' {remain}개를 인벤토리에도 바닥에도 두지 못했습니다.", this);
    }

    bool IsBlocked()
    {
        PlayerActionGate gate = FindGate();
        if (gate != null && gate.IsBusy)
            return true;

        PlayerStats stats = FindStats();
        if (stats != null && stats.IsDead)
            return true;

        return UIManager.Instance != null && UIManager.Instance.IsAnyWindowOpen;
    }

    static PlayerActionGate FindGate()
    {
        if (PlayerRoot.Instance == null)
            return null;

        return PlayerRoot.Instance.GetComponentInChildren<PlayerActionGate>(true);
    }

    static PlayerStats FindStats()
    {
        if (PlayerRoot.Instance == null)
            return null;

        return PlayerRoot.Instance.GetComponentInChildren<PlayerStats>(true);
    }

    static InventoryModel Inventory()
    {
        InventoryController controller = InventoryController.Instance;
        return controller != null ? controller.Inventory : null;
    }

    bool IsValid(int index)
    {
        return index >= 0 && index < _slots.Length;
    }

    void BindViews()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].Bind(i);
        }
    }

    void BindInventory()
    {
        InventoryModel model = Inventory();
        if (model == _inventory)
            return;

        UnbindInventory();
        _inventory = model;
        if (_inventory != null)
            _inventory.OnSlotChanged += HandleInventoryChanged;
    }

    void UnbindInventory()
    {
        if (_inventory == null)
            return;

        _inventory.OnSlotChanged -= HandleInventoryChanged;
        _inventory = null;
    }

    void BindEquipment()
    {
        EquipmentModel model = null;
        if (InventoryController.Instance != null)
            model = InventoryController.Instance.Equipment;

        if (model == _equipment)
            return;

        UnbindEquipment();
        _equipment = model;
        if (_equipment != null)
            _equipment.OnEquipmentChanged += HandleEquipmentChanged;
    }

    void UnbindEquipment()
    {
        if (_equipment == null)
            return;

        _equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        _equipment = null;
    }

    void HandleInventoryChanged(int index, ItemData item, int amount)
    {
        AvailabilityChanged?.Invoke();
    }

    void HandleEquipmentChanged(EquipmentSlotType slot, ItemData item)
    {
        AvailabilityChanged?.Invoke();
    }
}
