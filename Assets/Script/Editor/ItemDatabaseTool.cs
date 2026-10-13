using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프로젝트의 ItemData가 ItemDatabase에 들어 있는지 검사하고, 빠진 것을 넣는다.
/// </summary>
public static class ItemDatabaseTool
{
    const string CheckMenu = "Tools/Magiduck/Check Item Database";
    const string AddMenu = "Tools/Magiduck/Add Missing Items To Database";

    static bool _addScheduled;

    [MenuItem(CheckMenu)]
    public static void Check()
    {
        ItemDatabase database = FindDatabase();
        if (database == null)
            return;

        int missing = CountMissing(database, logEach: true);
        if (missing == 0)
            Debug.Log($"ItemDatabase: 프로젝트의 ItemData가 모두 등록되어 있습니다.", database);
        else
            Debug.LogWarning($"ItemDatabase: 등록 안 된 ItemData가 {missing}개입니다. {AddMenu} 로 넣을 수 있습니다.", database);
    }

    [MenuItem(AddMenu)]
    public static void AddMissing()
    {
        ItemDatabase database = FindDatabase();
        if (database == null)
            return;

        int added = AddMissingItems(database);
        if (added == 0)
            Debug.Log("ItemDatabase: 추가할 ItemData가 없습니다.", database);
        else
            Debug.Log($"ItemDatabase: ItemData {added}개를 목록에 넣었습니다.", database);
    }

    /// <summary>
    /// 가져온 ItemData가 데이터베이스에 없으면 임포트가 끝난 뒤에 넣고 경고한다.
    /// 임포트 콜백 안에서 목록을 고치면 저장이 임포트와 겹친다.
    /// </summary>
    public static void AddImported(string[] importedPaths)
    {
        if (_addScheduled || !ContainsItemData(importedPaths))
            return;

        _addScheduled = true;
        EditorApplication.delayCall += AddImportedWhenReady;
    }

    static void AddImportedWhenReady()
    {
        _addScheduled = false;

        ItemDatabase database = FindDatabase(logIfMissing: false);
        if (database == null)
            return;

        AddMissingItems(database);
    }

    static bool ContainsItemData(string[] importedPaths)
    {
        if (importedPaths == null)
            return false;

        for (int i = 0; i < importedPaths.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<ItemData>(importedPaths[i]) != null)
                return true;
        }

        return false;
    }

    static int CountMissing(ItemDatabase database, bool logEach)
    {
        if (database.items == null)
            database.items = new List<ItemData>();

        int missing = 0;
        foreach (ItemData item in FindAllItems())
        {
            if (database.items.Contains(item))
                continue;

            missing++;
            if (logEach)
            {
                Debug.LogWarning(
                    $"ItemDatabase에 없습니다: '{item.name}' (itemNum {item.itemNum}). 저장 후 다시 켜면 사라집니다.",
                    item);
            }
        }

        return missing;
    }

    static int AddMissingItems(ItemDatabase database)
    {
        if (database.items == null)
            database.items = new List<ItemData>();

        int added = 0;
        foreach (ItemData item in FindAllItems())
        {
            if (database.items.Contains(item))
                continue;

            database.items.Add(item);
            added++;
            Debug.LogWarning(
                $"ItemDatabase: '{item.name}' (itemNum {item.itemNum})이 목록에 없어 추가했습니다.",
                database);
        }

        if (added > 0)
        {
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssetIfDirty(database);
        }

        return added;
    }

    static List<ItemData> FindAllItems()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemData");
        var items = new List<ItemData>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null)
                items.Add(item);
        }

        return items;
    }

    static ItemDatabase FindDatabase(bool logIfMissing = true)
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids.Length == 0)
        {
            if (logIfMissing)
                Debug.LogError("ItemDatabase 에셋이 없습니다.");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
    }
}

/// <summary>ItemData를 가져올 때 데이터베이스 누락을 바로 넣는다.</summary>
public sealed class ItemDatabasePostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        ItemDatabaseTool.AddImported(importedAssets);
    }
}
