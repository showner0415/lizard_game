using UnityEngine;

/// <summary>
/// 런타임 인벤/창고 한 칸. UI가 아니라 데이터만 들고 있는다.
/// 디스크에는 이 객체가 아니라 itemNum + amount만 저장한다.
/// </summary>
public class ItemStack
{
    public ItemData item;
    public int amount;

    public bool HasItem => item != null && amount > 0;

    public void Clear()
    {
        item = null;
        amount = 0;
    }

    public void Set(ItemData newItem, int newAmount)
    {
        if (newItem == null || newAmount <= 0)
        {
            Clear();
            return;
        }

        item = newItem;
        amount = newAmount;
    }

    public SlotSaveData ToSave()
    {
        if (!HasItem)
            return new SlotSaveData { itemNum = -1, amount = 0 };

        return new SlotSaveData
        {
            itemNum = item.itemNum,
            amount = amount
        };
    }

    /// <summary>
    /// 런타임 인벤/창고 한 칸. UI가 아니라 데이터만 들고 있는다.
    /// 디스크에는 이 객체가 아니라 itemNum + amount만 저장한다.
    /// </summary>
    public void FromSave(SlotSaveData save, ItemDatabase database)
    {
        // ✅ 예외 처리 1: null 체크
        if (save == null || save.IsEmpty)
        {
            Clear();
            return;
        }

        // ✅ 예외 처리 2: 데이터베이스 검증
        if (database == null)
        {
            Debug.LogError($"[ItemStack] ❌ ItemDatabase가 null입니다. itemNum={save.itemNum} 복원 실패.");
            Clear();
            return;
        }

        // ✅ 예외 처리 3: 잘못된 itemNum (데이터베이스에 없음)
        ItemData found = database.Get(save.itemNum);
        if (found == null)
        {
            Debug.LogWarning($"[ItemStack] ⚠️ itemNum={save.itemNum}에 해당하는 아이템을 데이터베이스에서 찾을 수 없습니다. " +
                $"세이브 파일이 손상되었거나 아이템이 게임에서 제거되었을 수 있습니다. 슬롯을 비웁니다.");
            Clear();
            return;
        }

        // ✅ 정상 복원
        Set(found, save.amount);
    }
}
