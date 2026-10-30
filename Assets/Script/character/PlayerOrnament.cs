using UnityEngine;

/// <summary>
/// 꼬리 장식 칸만 조회한다. 모델은 캐시하지 않고 물어볼 때마다 읽는다.
/// 장식이 없으면 기본 투척 마법을 쓴다.
/// </summary>
public sealed class PlayerOrnament : MonoBehaviour
{
    [SerializeField, Tooltip("꼬리 장식이 없을 때 쓰는 마법.")]
    MagicData _basicThrow;

    /// <summary>장착된 꼬리 장식. 없거나 다른 종류면 null.</summary>
    public TailOrnamentItemData CurrentOrnament
    {
        get
        {
            InventoryController controller = InventoryController.Instance;
            if (controller == null || controller.Equipment == null)
                return null;

            return controller.Equipment.Get(EquipmentSlotType.Tail) as TailOrnamentItemData;
        }
    }

    /// <summary>지금 발사에 쓸 마법. 장식 마법이 있으면 그것, 없으면 기본 투척.</summary>
    public MagicData CurrentMagic
    {
        get
        {
            TailOrnamentItemData ornament = CurrentOrnament;
            if (ornament != null && ornament.Magic != null)
                return ornament.Magic;

            return _basicThrow;
        }
    }

    void Awake()
    {
        if (_basicThrow == null)
            Debug.LogWarning("[PlayerOrnament] 기본 투척 마법이 없습니다.", this);
    }
}
