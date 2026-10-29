using UnityEngine;

/// <summary>
/// 인벤토리, 드롭, 세이브가 공통으로 쓰는 아이템 데이터.
/// 종류별 필드는 <see cref="EquipmentItemData"/>, <see cref="ConsumableItemData"/>, <see cref="MagicBookItemData"/>, <see cref="ScaleItemData"/>에 둔다.
/// </summary>
public abstract class ItemData : ScriptableObject
{
    /// <summary>세이브/로드용 ID.</summary>
    public int itemNum;

    /// <summary>슬롯에 그릴 아이콘.</summary>
    public Sprite icon;

    /// <summary>표시용 희귀도.</summary>
    public ItemRarity rarity;

    /// <summary>한 칸에 쌓을 수 있는 최대 수량. 1이면 겹치지 않는다.</summary>
    [Min(1)] public int maxStack = 1;

    [Tooltip("툴팁에 크게 표시할 이름. 비우면 에셋 파일 이름을 쓴다.")]
    [SerializeField] string displayName;

    [TextArea(2, 6)]
    [Tooltip("툴팁에 이름 아래 표시할 설명. 비우면 이름만 나온다.")]
    [SerializeField] string description;

    /// <summary>툴팁에 크게 표시할 이름. 비어 있으면 에셋 파일 이름.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    /// <summary>툴팁 설명. 없으면 빈 문자열.</summary>
    public string Description => description ?? string.Empty;

    /// <summary>최대 수량이 2 이상이면 스택할 수 있다.</summary>
    public bool Stackable => maxStack > 1;

    [Header("World Drop")]
    /// <summary>월드에 떨어뜨릴 때 생성할 프리팹.</summary>
    public GameObject worldPrefab;

    /// <summary>UI 필터용 종류. 하위 클래스가 고정해서 돌려준다.</summary>
    public abstract ItemCategory Category { get; }

    /// <summary>에디터에서 잘못된 값을 바로잡는다.</summary>
    protected virtual void OnValidate()
    {
        if (maxStack >= 1)
            return;

        Debug.LogWarning($"[ItemData] '{name}'의 maxStack이 1보다 작아 1로 맞춥니다.", this);
        maxStack = 1;
    }
}
