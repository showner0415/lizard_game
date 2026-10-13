using UnityEngine;

/// <summary>클릭 한 번의 결과.</summary>
public enum TransferResult
{
    /// <summary>바꿀 것이 없었다.</summary>
    None = 0,

    /// <summary>아이템이 움직였다.</summary>
    Moved = 1,

    /// <summary>움직이려 했으나 규칙이 거절했다.</summary>
    Rejected = 2
}

/// <summary>
/// 아이템이 어디서 어디로 가는지 정한다. 칸 데이터와 커서는 각자 들고, 이 클래스는 규칙만 있다.
/// </summary>
public static class ItemTransfer
{
    /// <summary>마우스가 올라간 칸. 없으면 <see cref="SlotRef.None"/>.</summary>
    public static SlotRef Hovered { get; private set; }

    /// <summary>이 칸 위로 마우스가 들어왔다.</summary>
    public static void SetHovered(SlotRef slot)
    {
        Hovered = slot;
    }

    /// <summary>마우스가 이 칸에서 나갔다. 다른 칸이 이미 올라와 있으면 그대로 둔다.</summary>
    public static void ClearHovered(SlotRef slot)
    {
        if (slot.Kind == SlotRefKind.None)
            return;

        if (SameSlot(Hovered, slot))
            Hovered = SlotRef.None;
    }

    /// <summary>커서에 아이템이 있으면 그것을, 없으면 마우스가 올라간 칸을 바닥에 둔다.</summary>
    public static void DropAtPointer()
    {
        MouseItemUI mouse = MouseItemUI.Instance;
        if (mouse != null && mouse.HasItem())
        {
            DropHeld();
            return;
        }

        DropToWorld(Hovered);
    }

    /// <summary>집기, 내려놓기, 합치기, 교체. 장비칸이면 장착하거나 해제한다.</summary>
    public static TransferResult Click(SlotRef slot)
    {
        MouseItemUI mouse = MouseItemUI.Instance;
        if (mouse == null)
            return TransferResult.None;

        if (slot.Kind == SlotRefKind.Equipment)
            return ClickEquipment(slot.EquipmentSlot, mouse);

        if (slot.Kind != SlotRefKind.Container || slot.Container == null)
            return TransferResult.None;

        return ClickContainer(slot, mouse);
    }

    /// <summary>Shift+클릭. 열린 창, 장착, 인벤 해제 중 그 칸에 맞는 쪽으로 옮긴다.</summary>
    public static TransferResult QuickMove(SlotRef slot)
    {
        if (slot.Kind == SlotRefKind.Equipment)
            return QuickUnequip(slot.EquipmentSlot);

        if (slot.Kind != SlotRefKind.Container || slot.Container == null)
            return TransferResult.None;

        if (!slot.Container.HasItem(slot.ContainerIndex))
            return TransferResult.None;

        if (slot.IsPlayerInventory)
        {
            InventoryModel open = FindOpenContainer();
            if (open != null && open != slot.Container)
                return MoveTo(slot.Container, slot.ContainerIndex, open);

            return TryShiftEquip(slot.Container, slot.ContainerIndex);
        }

        InventoryModel player = PlayerInventory();
        if (player == null)
            return TransferResult.None;

        return MoveTo(slot.Container, slot.ContainerIndex, player);
    }

    /// <summary>그 칸의 아이템을 바닥에 둔다. 바닥에 생겼을 때만 칸에서 뺀다.</summary>
    public static void DropToWorld(SlotRef slot)
    {
        if (slot.Kind == SlotRefKind.Equipment)
        {
            DropEquipment(slot.EquipmentSlot);
            return;
        }

        if (slot.Kind != SlotRefKind.Container || slot.Container == null)
            return;

        InventoryModel model = slot.Container;
        int index = slot.ContainerIndex;
        if (!model.HasItem(index))
            return;

        ItemStack stack = model.Peek(index);
        if (Drop(stack.item, stack.amount))
            model.ClearSlot(index);
    }

    /// <summary>커서의 아이템을 바닥에 둔다. 바닥에 생겼을 때만 커서를 비운다.</summary>
    public static void DropHeld()
    {
        MouseItemUI mouse = MouseItemUI.Instance;
        if (mouse == null || !mouse.HasItem())
            return;

        if (!Drop(mouse.GetItem(), mouse.GetAmount()))
            return;

        mouse.Clear();
    }

    /// <summary>커서를 원래 칸, 그다음 플레이어 인벤 순으로 되돌린다. 실패하면 커서에 남긴다.</summary>
    public static void ReturnHeld()
    {
        MouseItemUI mouse = MouseItemUI.Instance;
        if (mouse == null || !mouse.HasItem())
            return;

        if (TryReturnToOrigin(mouse))
            return;

        InventoryModel inventory = PlayerInventory();
        if (inventory == null)
        {
            Debug.LogWarning("[ItemTransfer] 플레이어 인벤이 없어 커서 아이템을 되돌리지 못했습니다.");
            return;
        }

        int remain = inventory.AddItem(mouse.GetItem(), mouse.GetAmount());
        if (remain <= 0)
        {
            mouse.Clear();
            return;
        }

        mouse.SetItem(mouse.GetItem(), remain);
        Debug.LogWarning($"[ItemTransfer] 인벤이 가득 차 '{mouse.GetItem().name} x{remain}'이 커서에 남습니다.");
    }

    /// <summary>
    /// 커서 아이템을 바닥에 둔다. 바닥에 못 둬도 커서는 비운다.
    /// 사망처럼 인벤으로 되돌리면 안 되는 경우에 쓴다.
    /// </summary>
    public static void DiscardHeld()
    {
        MouseItemUI mouse = MouseItemUI.Instance;
        if (mouse == null || !mouse.HasItem())
            return;

        DropHeld();
        if (mouse.HasItem())
            mouse.Clear();
    }

    /// <summary>커서를 되돌리고, 남은 것은 바닥에 둔다.</summary>
    public static void ReturnHeldOrDrop()
    {
        ReturnHeld();
        DropHeld();
    }

    /// <summary>커서 아이템을 플레이어 인벤에 넣는다. 다 들어갔으면 커서를 비우고 true.</summary>
    public static bool TryStoreHeldInPlayerInventory()
    {
        MouseItemUI mouse = MouseItemUI.Instance;
        if (mouse == null || !mouse.HasItem())
            return false;

        InventoryModel inventory = PlayerInventory();
        if (inventory == null)
            return false;

        ItemData item = mouse.GetItem();
        int amount = mouse.GetAmount();
        SlotRef origin = mouse.Origin;
        if (origin.IsPlayerInventory && !origin.Container.HasItem(origin.ContainerIndex))
        {
            origin.Container.SetSlot(origin.ContainerIndex, item, amount);
            mouse.Clear();
            return true;
        }

        int remain = inventory.AddItem(item, amount);
        if (remain > 0)
        {
            mouse.SetItem(item, remain);
            return false;
        }

        mouse.Clear();
        return true;
    }

    static TransferResult ClickContainer(SlotRef slot, MouseItemUI mouse)
    {
        InventoryModel model = slot.Container;
        int index = slot.ContainerIndex;

        if (!mouse.HasItem())
        {
            if (!model.HasItem(index))
                return TransferResult.None;

            ItemStack stack = model.Peek(index);
            mouse.SetOrigin(slot);
            mouse.SetItem(stack.item, stack.amount);
            model.ClearSlot(index);
            return TransferResult.Moved;
        }

        ItemData mouseItem = mouse.GetItem();
        int mouseAmount = mouse.GetAmount();
        ItemStack dest = model.Peek(index);

        if (dest == null || !dest.HasItem)
        {
            model.SetSlot(index, mouseItem, mouseAmount);
            mouse.Clear();
            return TransferResult.Moved;
        }

        if (dest.item == mouseItem && mouseItem.Stackable && dest.amount < mouseItem.maxStack)
        {
            int space = mouseItem.maxStack - dest.amount;
            int added = Mathf.Min(space, mouseAmount);
            model.SetSlot(index, dest.item, dest.amount + added);
            int remain = mouseAmount - added;
            if (remain <= 0)
                mouse.Clear();
            else
                mouse.SetItem(mouseItem, remain);

            return TransferResult.Moved;
        }

        ItemData swapped = dest.item;
        int swappedAmount = dest.amount;
        mouse.SetItem(swapped, swappedAmount);
        mouse.SetOrigin(slot);
        model.SetSlot(index, mouseItem, mouseAmount);
        return TransferResult.Moved;
    }

    static TransferResult ClickEquipment(EquipmentSlotType slot, MouseItemUI mouse)
    {
        InventoryController controller = InventoryController.Instance;
        if (controller == null || controller.Equipment == null)
            return TransferResult.None;

        if (!mouse.HasItem())
        {
            if (!controller.Equipment.HasItem(slot))
                return TransferResult.None;

            if (!controller.TryUnequip(slot, out ItemData taken) || taken == null)
                return TransferResult.Rejected;

            mouse.SetOrigin(SlotRef.FromEquipment(slot));
            mouse.SetItem(taken, 1);
            return TransferResult.Moved;
        }

        ItemData held = mouse.GetItem();
        if (!controller.Equipment.CanEquip(slot, held))
            return TransferResult.Rejected;

        if (!controller.TryEquipItem(held, out ItemData previous))
            return TransferResult.Rejected;

        if (previous == null)
        {
            mouse.Clear();
            return TransferResult.Moved;
        }

        mouse.SetItem(previous, 1);
        mouse.SetOrigin(SlotRef.FromEquipment(slot));
        return TransferResult.Moved;
    }

    static TransferResult QuickUnequip(EquipmentSlotType slot)
    {
        InventoryController controller = InventoryController.Instance;
        if (controller == null || controller.Equipment == null || !controller.Equipment.HasItem(slot))
            return TransferResult.None;

        return controller.TryUnequipToInventory(slot) ? TransferResult.Moved : TransferResult.Rejected;
    }

    static TransferResult TryShiftEquip(InventoryModel model, int index)
    {
        ItemStack stack = model.Peek(index);
        if (stack == null || stack.item is not EquipmentItemData)
            return TransferResult.None;

        InventoryController controller = InventoryController.Instance;
        if (controller == null)
            return TransferResult.None;

        ItemData item = stack.item;
        int amount = stack.amount;
        model.ClearSlot(index);

        if (!controller.TryEquipItem(item, out ItemData previous))
        {
            PutBack(model, index, item, amount);
            return TransferResult.Rejected;
        }

        if (amount > 1)
            PutBack(model, index, item, amount - 1);

        if (previous == null)
            return TransferResult.Moved;

        InventoryModel player = controller.Inventory;
        if (player != null && player.AddItem(previous, 1) <= 0)
            return TransferResult.Moved;

        if (!controller.TryEquipItem(previous, out ItemData kicked))
        {
            Debug.LogWarning("[ItemTransfer] 이전 장비를 인벤에 넣지 못해 바닥에 떨어뜨립니다.");
            Drop(previous, 1);
            return TransferResult.Moved;
        }

        PutBack(player != null ? player : model, index, kicked != null ? kicked : item, 1);
        return TransferResult.Rejected;
    }

    static TransferResult MoveTo(InventoryModel source, int index, InventoryModel dest)
    {
        if (source == null || dest == null || source == dest || !source.HasItem(index))
            return TransferResult.None;

        ItemStack stack = source.Peek(index);
        int remain = dest.AddItem(stack.item, stack.amount);
        if (remain >= stack.amount)
            return TransferResult.None;

        if (remain <= 0)
            source.ClearSlot(index);
        else
            source.SetSlot(index, stack.item, remain);

        return TransferResult.Moved;
    }

    static bool TryReturnToOrigin(MouseItemUI mouse)
    {
        SlotRef origin = mouse.Origin;
        if (origin.Kind == SlotRefKind.Equipment)
        {
            InventoryController controller = InventoryController.Instance;
            if (controller == null || controller.Equipment == null)
                return false;

            if (!controller.Equipment.HasItem(origin.EquipmentSlot) &&
                controller.TryEquipItem(mouse.GetItem(), out _))
            {
                mouse.Clear();
                return true;
            }

            return false;
        }

        if (origin.Kind == SlotRefKind.Container && origin.Container != null &&
            !origin.Container.HasItem(origin.ContainerIndex))
        {
            origin.Container.SetSlot(origin.ContainerIndex, mouse.GetItem(), mouse.GetAmount());
            mouse.Clear();
            return true;
        }

        return false;
    }

    static void PutBack(InventoryModel model, int index, ItemData item, int amount)
    {
        if (model == null || item == null)
            return;

        if (index >= 0 && index < model.Capacity && !model.HasItem(index))
        {
            model.SetSlot(index, item, amount);
            return;
        }

        int remain = model.AddItem(item, amount);
        if (remain > 0)
            Drop(item, remain);
    }

    static bool Drop(ItemData item, int amount)
    {
        if (ItemDropManager.Instance == null)
        {
            Debug.LogError("[ItemTransfer] ItemDropManager가 없습니다.");
            return false;
        }

        return ItemDropManager.Instance.DropItem(item, amount, FindPlayer());
    }

    static bool DropEquipment(EquipmentSlotType slot)
    {
        InventoryController controller = InventoryController.Instance;
        if (controller == null || !controller.TryUnequip(slot, out ItemData item) || item == null)
            return false;

        if (Drop(item, 1))
            return true;

        controller.TryEquipItem(item, out _);
        return false;
    }

    static InventoryModel FindOpenContainer()
    {
        InventoryModel open = ContainerPresenter.OpenModel;
        InventoryModel player = PlayerInventory();
        if (open == null || open == player)
            return null;

        return open;
    }

    static InventoryModel PlayerInventory()
    {
        return InventoryController.Instance != null ? InventoryController.Instance.Inventory : null;
    }

    static bool SameSlot(SlotRef a, SlotRef b)
    {
        if (a.Kind != b.Kind)
            return false;

        if (a.Kind == SlotRefKind.Equipment)
            return a.EquipmentSlot == b.EquipmentSlot;

        if (a.Kind == SlotRefKind.Container)
            return a.Container == b.Container && a.ContainerIndex == b.ContainerIndex;

        return false;
    }

    static Transform FindPlayer()
    {
        if (PlayerRoot.Instance == null)
            return null;

        return PlayerRoot.Instance.transform.Find("Player");
    }
}
