// PROPOSED — not applied. Target: Assets/Script/Inventory&chest/IItemSlot.cs (new file)

/// <summary>
/// 패널 종류와 무관한 "아이템 한 칸" 계약.
/// MouseItemUI(원래 슬롯 기억), HotbarSlot(마법책 반환), Q키 드롭이 이 인터페이스만 알면 되도록
/// 상자/창고 슬롯(ItemSlotUI)과 인벤토리 슬롯(InventorySlotHandle → 모델)을 같은 타입으로 다룬다.
/// </summary>
public interface IItemSlot
{
    /// <summary>아이템이 들어 있는지.</summary>
    bool HasItem();

    /// <summary>들어 있는 아이템 (없으면 null).</summary>
    ItemData GetItem();

    /// <summary>들어 있는 수량.</summary>
    int GetAmount();

    /// <summary>칸 내용을 덮어쓴다. item이 null이거나 amount가 0 이하면 비운다.</summary>
    void SetItem(ItemData item, int amount = 1);

    /// <summary>칸을 비운다.</summary>
    void Clear();

    /// <summary>칸의 아이템을 플레이어 앞 월드에 떨어뜨리고 칸을 비운다.</summary>
    void DropItemToWorld();
}

/// <summary>IItemSlot 보조 메서드.</summary>
public static class ItemSlotExtensions
{
    /// <summary>
    /// 인터페이스 null 검사는 Unity의 "파괴된 오브젝트" 판정을 우회하므로,
    /// MonoBehaviour 구현체는 Unity null 검사로 생존 여부를 확인한다.
    /// </summary>
    public static bool IsAlive(this IItemSlot slot)
    {
        if (slot is UnityEngine.Object unityObject)
            return unityObject != null;

        return slot != null;
    }
}

/// <summary>
/// 현재 마우스 커서 아래의 아이템 칸 (Q키 드롭 2순위용). 기존 ItemSlotUI.CurrentHoveredSlot을 대체한다.
/// </summary>
public static class ItemSlotHover
{
    /// <summary>현재 호버 중인 칸 (없으면 null).</summary>
    public static IItemSlot Current { get; private set; }

    /// <summary>포인터가 칸에 들어왔을 때 호출.</summary>
    public static void Enter(IItemSlot slot) => Current = slot;

    /// <summary>포인터가 칸에서 나갔을 때 호출. 다른 칸이 이미 등록됐으면 건드리지 않는다.</summary>
    public static void Exit(IItemSlot slot)
    {
        if (ReferenceEquals(Current, slot))
            Current = null;
    }

    /// <summary>호버 상태를 강제로 비운다 (패널이 닫혀 Exit 이벤트가 오지 않을 때).</summary>
    public static void Clear() => Current = null;
}
