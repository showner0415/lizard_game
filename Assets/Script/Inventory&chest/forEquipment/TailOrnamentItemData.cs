using UnityEngine;

/// <summary>꼬리 장식. 끼우면 그 마법으로 발사한다.</summary>
[CreateAssetMenu(fileName = "TailOrnament", menuName = "Item/Tail Ornament")]
public sealed class TailOrnamentItemData : EquipmentItemData
{
    [SerializeField] MagicData _magic;

    /// <summary>이 장식을 끼웠을 때 쓰는 마법.</summary>
    public MagicData Magic => _magic;

    /// <inheritdoc />
    protected override void OnValidate()
    {
        base.OnValidate();
        slotType = EquipmentSlotType.Tail;
        maxStack = 1;
        if (_magic == null)
        {
            Debug.LogWarning($"[TailOrnamentItemData] '{name}'에 마법이 없습니다. 끼우면 기본 던지기가 나갑니다.", this);
            return;
        }

        if (_magic.Mode != CastMode.Channel && _magic.ScaleCost < 1)
            Debug.LogWarning($"[TailOrnamentItemData] '{name}'의 마법 '{_magic.name}'은 비늘을 1개 이상 써야 합니다.", this);
    }
}
