using UnityEngine;

/// <summary>핫바에 등록하면 <see cref="magic"/>을 시전할 수 있는 마법서.</summary>
[CreateAssetMenu(fileName = "MagicBook", menuName = "Item/MagicBook")]
public class MagicBookItemData : ItemData
{
    /// <summary>이 마법서가 가르치는 마법.</summary>
    public MagicData magic;

    /// <inheritdoc />
    public override ItemCategory Category => ItemCategory.MagicBook;

    /// <summary>연결된 마법이 흑마법이면 true.</summary>
    public bool IsDark => magic != null && magic.isDarkMagic;
}
