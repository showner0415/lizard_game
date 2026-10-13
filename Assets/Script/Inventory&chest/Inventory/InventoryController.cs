using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 인벤/창고의 "메모리 상태".
/// UI는 씬마다 새로 생기고, 이 컨트롤러만 데이터를 들고 씬을 넘어간다.
/// </summary>
public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance;

    [SerializeField] private int defaultInventorySlots = 50;
    [SerializeField] private int defaultStashSlots = 50;

    /// <summary>플레이어 인벤 데이터.</summary>
    public InventoryModel Inventory { get; private set; }

    /// <summary>거점 창고. 레이드에 들고 가지 않으며, 사망해도 남는다.</summary>
    public InventoryModel Stash { get; private set; }

    /// <summary>장비칸 데이터. 일반 인벤과 분리한다.</summary>
    public EquipmentModel Equipment { get; private set; }

    private bool inRaid;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Inventory = new InventoryModel(defaultInventorySlots);
        Inventory.OnWarning += HandleInventoryWarning;
        Stash = new InventoryModel(defaultStashSlots);
        Stash.OnWarning += HandleInventoryWarning;
        Equipment = new EquipmentModel();
    }

    private void HandleInventoryWarning(string message)
    {
        Debug.LogWarning("[InventoryModel] " + message, this);
    }

    /// <summary>가능한 만큼 플레이어 인벤에 넣고, 들어가지 못한 수량을 반환한다.</summary>
    public int AddToInventory(ItemData item, int amount = 1)
    {
        if (Inventory == null || item == null || amount <= 0)
            return amount;

        return Inventory.AddItem(item, amount);
    }

    /// <summary>PlayerInput의 Inventory 액션. 창을 여닫는 판단은 UIManager가 한다.</summary>
    public void OnInventory(InputAction.CallbackContext context)
    {
        Debug.Log("[Tab] 입력 받음");
        if (!context.performed)
            return;

        UIManager.Instance?.Toggle();
    }

    //====================================================
    //              데이터 배열
    //====================================================

    public int InventorySlotCount => Inventory != null ? Inventory.Capacity : 0;
    public int StashSlotCount => Stash != null ? Stash.Capacity : 0;

    public ItemStack GetInventorySlot(int index)
    {
        return Inventory != null ? Inventory.Peek(index) : null;
    }

    public void SetInventorySlot(int index, ItemData item, int amount)
    {
        if (Inventory == null)
            return;
        Inventory.SetSlot(index, item, amount);
    }

    public void ClearInventorySlot(int index)
    {
        SetInventorySlot(index, null, 0);
    }

    /// <summary>지금 가방이 더해주는 칸 수.</summary>
    public int BagExtraSlots => Equipment != null && Equipment.Get(EquipmentSlotType.Bag) is BagItemData bag
        ? Mathf.Max(0, bag.extraSlots)
        : 0;

    /// <summary>기본 칸 + 가방 추가 칸으로 맞춘다. 뒤쪽 칸에 아이템이 있으면 실패한다.</summary>
    public bool ApplyBagCapacity()
    {
        if (Inventory == null)
            return false;
        return Inventory.TrySetCapacity(defaultInventorySlots + BagExtraSlots);
    }

    /// <summary>장비칸에 넣는다. 가방이면 칸 수를 먼저 맞춘다.</summary>
    public bool TryEquipItem(ItemData item, out ItemData previous)
    {
        previous = null;
        if (Equipment == null || item is not EquipmentItemData eq)
            return false;

        if (eq.slotType == EquipmentSlotType.Bag)
            return TryEquipBag(eq, out previous);

        return Equipment.TryEquip(eq.slotType, eq, out previous);
    }

    /// <summary>장비칸에서 뺀다. 가방이면 칸 수를 줄인 뒤에만 뺀다.</summary>
    public bool TryUnequip(EquipmentSlotType slot, out ItemData removed)
    {
        removed = null;
        if (Equipment == null)
            return false;

        if (slot == EquipmentSlotType.Bag)
            return TryUnequipBag(out removed);

        removed = Equipment.Unequip(slot);
        return true;
    }

    /// <summary>장착된 장비를 인벤 빈 칸으로 옮긴다. 자리가 없으면 그대로 둔다.</summary>
    public bool TryUnequipToInventory(EquipmentSlotType slot)
    {
        if (Equipment == null || Inventory == null || !Equipment.HasItem(slot))
            return false;

        ItemData equipped = Equipment.Get(slot);
        if (slot == EquipmentSlotType.Bag)
        {
            if (!Inventory.TrySetCapacity(defaultInventorySlots))
                return false;
            if (!Inventory.TryAddItem(equipped, 1))
            {
                ApplyBagCapacity();
                return false;
            }

            Equipment.Unequip(slot);
            return true;
        }

        if (!Inventory.TryAddItem(equipped, 1))
            return false;

        Equipment.Unequip(slot);
        return true;
    }

    bool TryEquipBag(EquipmentItemData item, out ItemData previous)
    {
        previous = Equipment.Get(EquipmentSlotType.Bag);
        if (!Equipment.CanEquip(EquipmentSlotType.Bag, item))
            return false;

        int extra = item is BagItemData bag ? Mathf.Max(0, bag.extraSlots) : 0;
        if (!Inventory.TrySetCapacity(defaultInventorySlots + extra))
            return false;

        if (Equipment.TryEquip(EquipmentSlotType.Bag, item, out previous))
            return true;

        ApplyBagCapacity();
        return false;
    }

    bool TryUnequipBag(out ItemData removed)
    {
        removed = Equipment.Get(EquipmentSlotType.Bag);
        if (removed == null)
            return true;

        if (!Inventory.TrySetCapacity(defaultInventorySlots))
            return false;

        removed = Equipment.Unequip(EquipmentSlotType.Bag);
        return true;
    }

    //====================================================
    //              SaveData
    //====================================================

    public void CaptureState(SaveData data)
    {
        if (data == null)
            return;

        data.inRaid = inRaid;
        data.inventory = Inventory != null ? Inventory.ToSaveArray() : System.Array.Empty<SlotSaveData>();
        data.stash = Stash != null ? Stash.ToSaveArray() : System.Array.Empty<SlotSaveData>();
        data.equippedItemNums = Equipment != null ? Equipment.ToSaveIds() : System.Array.Empty<int>();
    }

    public void RestoreState(SaveData data)
    {
        if (data == null)
            return;

        inRaid = data.inRaid;

        if (Inventory == null)
            Inventory = new InventoryModel(defaultInventorySlots);
        if (Stash == null)
            Stash = new InventoryModel(defaultStashSlots);

        ItemDatabase db = SaveManager.Instance != null ? SaveManager.Instance.Database : null;

        if (Equipment == null)
            Equipment = new EquipmentModel();
        Equipment.LoadFromSave(data.equippedItemNums, db);
        ApplyBagCapacity();
        Inventory.LoadIntoCurrentCapacity(data.inventory, db);
        Stash.LoadFromSave(data.stash, db);
    }

    //====================================================
    //              추출 장르 이벤트
    //====================================================

    public void BeginRaid()
    {
        inRaid = true;
    }

    public void EndRaidExtract()
    {
        inRaid = false;
    }

    /// <summary>사망. 커서에 든 아이템을 버리고, 던전 인벤과 장비를 비운다. 창고는 남긴다.</summary>
    public void LoseRaidInventory()
    {
        ItemTransfer.DiscardHeld();
        Inventory?.ClearAll();

        if (Equipment != null)
        {
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
                Equipment.Unequip((EquipmentSlotType)i);
        }

        ApplyBagCapacity();
        inRaid = false;
    }
}
