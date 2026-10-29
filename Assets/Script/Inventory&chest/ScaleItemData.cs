using UnityEngine;

/// <summary>인벤토리에 드는 파밍 비늘. 가리키는 <see cref="ScaleData"/>를 탄창에 장전한다.</summary>
[CreateAssetMenu(fileName = "ScaleItem", menuName = "Item/Scale")]
public class ScaleItemData : ItemData
{
    [Tooltip("이 아이템이 장전하는 비늘. 비우면 안 된다.")]
    [SerializeField] private ScaleData scale;

    /// <summary>장전되는 비늘. 없을 수 있으니 호출하는 쪽에서 확인한다.</summary>
    public ScaleData Scale => scale;

    /// <inheritdoc />
    public override ItemCategory Category => ItemCategory.Scale;

    /// <inheritdoc />
    protected override void OnValidate()
    {
        base.OnValidate();
        if (scale == null)
            Debug.LogWarning($"[ScaleItemData] '{name}'에 ScaleData가 없습니다.", this);
    }
}
