// PROPOSED — not applied. Target: Assets/Script/Inventory&chest/WorldItemDrop.cs (new file)
using UnityEngine;

/// <summary>
/// Shared "drop an item in front of the player" step, previously duplicated inside ItemSlotUI.DropItemToWorld.
/// Behavior is unchanged: returns false only when ItemDropManager is missing; otherwise the caller clears its slot
/// (ItemDropManager.DropItem itself may still refuse, e.g. no worldPrefab — pre-existing behavior, see conflict log).
/// </summary>
public static class WorldItemDrop
{
    /// <summary>Spawns the item via ItemDropManager. Returns false if it could not be attempted.</summary>
    public static bool TryDrop(ItemData item, int amount, string logTag)
    {
        if (item == null || amount <= 0)
        {
            Debug.Log($"[{logTag}] Q키 입력 - 빈 슬롯이므로 드롭하지 않음");
            return false;
        }

        if (ItemDropManager.Instance == null)
        {
            Debug.LogError($"[{logTag}] ❌ ItemDropManager.Instance가 null입니다.");
            return false;
        }

        Transform playerTransform = null;
        if (PlayerRoot.Instance != null)
            playerTransform = PlayerRoot.Instance.transform.Find("Player");

        Debug.Log($"[{logTag}] Q키 드롭 - '{item.name}' x{amount}");
        ItemDropManager.Instance.DropItem(item, amount, playerTransform);
        return true;
    }
}
