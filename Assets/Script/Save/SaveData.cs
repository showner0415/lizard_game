using System;

/// <summary>
/// JSON에 들어가는 순수 데이터. GameObject / 컴포넌트 / 에셋 참조는 넣지 않는다.
/// 아이템은 ItemData 자체가 아니라 itemNum(ID)만 저장한다.
/// </summary>
[Serializable]
public class SlotSaveData
{
    public int itemNum = -1;
    public int amount;

    public bool IsEmpty => itemNum < 0 || amount <= 0;
}

[Serializable]
public class SaveData
{
    public int version = 1;

    /// <summary>던전 레이드 중이면 true. 사망 시 인벤만 비운다.</summary>
    public bool inRaid;

    public int gold;

    /// <summary>던전에 들고 가는 가방. 탈출 성공 시 유지, 사망 시 소실.</summary>
    public SlotSaveData[] inventory = Array.Empty<SlotSaveData>();

    /// <summary>홈 창고. 사망해도 유지되는 확정 저장분.</summary>
    public SlotSaveData[] stash = Array.Empty<SlotSaveData>();

    /// <summary>핫바 마법서 ID. MagicBookItemData.itemNum, 빈 칸은 -1.</summary>
    public int[] hotbar = Array.Empty<int>();

    /// <summary>장비칸 아이템 ID. EquipmentSlotType 순서, 빈 칸은 -1.</summary>
    public int[] equippedItemNums = Array.Empty<int>();

    /// <summary>장전된 파밍 비늘의 itemNum. 기본 비늘이면 -1. version 2부터 기록한다.</summary>
    public int loadedScaleItemNum = -1;

    /// <summary>장전된 칸 수. 탄창에는 한 종류만 들어 있다.</summary>
    public int loadedScaleCount;
}
