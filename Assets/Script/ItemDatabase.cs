using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// itemNum → ItemData 사전.
/// JSON에는 ID만 저장하고, 불러올 때 여기서 원본 에셋을 찾는다.
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Item/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemData> items = new();

    private Dictionary<int, ItemData> lookup;

    public void Init()
    {
        lookup = new Dictionary<int, ItemData>();
        WarnDuplicateItemNums();

        if (items == null)
            return;

        foreach (ItemData item in items)
        {
            if (item == null)
            {
                Debug.LogWarning("[ItemDatabase] 비어 있는 항목이 있어 건너뜁니다. 지운 에셋일 수 있습니다.", this);
                continue;
            }

            if (lookup.ContainsKey(item.itemNum))
                continue;

            lookup.Add(item.itemNum, item);
        }
    }

    void OnValidate()
    {
        WarnDuplicateItemNums();
    }

    /// <summary>같은 itemNum이 두 개 이상이면 경고한다.</summary>
    public void WarnDuplicateItemNums()
    {
        if (items == null)
            return;

        var seen = new Dictionary<int, ItemData>();
        foreach (ItemData item in items)
        {
            if (item == null)
                continue;

            if (seen.TryGetValue(item.itemNum, out ItemData first))
            {
                Debug.LogWarning(
                    $"ItemDatabase: itemNum {item.itemNum}이 중복됩니다. '{first.name}'과 '{item.name}'.",
                    this);
                continue;
            }

            seen.Add(item.itemNum, item);
        }
    }

    public ItemData Get(int itemNum)
    {
        if (lookup == null)
            Init();

        if (itemNum < 0)
            return null;

        lookup.TryGetValue(itemNum, out ItemData item);
        return item;
    }

    public Sprite GetIcon(int itemNum)
    {
        ItemData item = Get(itemNum);
        return item != null ? item.icon : null;
    }
}
