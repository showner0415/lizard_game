using UnityEngine;

/// <summary>빈 비늘 칸을 앞 번호부터 즉시 자라게(Grown) 하는 마법. 장전은 하지 않는다.</summary>
[CreateAssetMenu(fileName = "RestoreScalesMagic", menuName = "Magic/Restore Scales")]
public class RestoreTailsMagicData : MagicData
{
    [SerializeField, Min(1), Tooltip("한 번에 자라게 할 빈 칸 수.")]
    int _restoreCount = 1;

    /// <summary>자라게 할 빈 칸 수. 1 이상.</summary>
    public int RestoreCount => _restoreCount < 1 ? 1 : _restoreCount;

    /// <inheritdoc />
    public override bool NeedsTargetPoint => false;

    protected override void OnValidate()
    {
        base.OnValidate();
        if (_restoreCount < 1)
        {
            Debug.LogWarning($"[RestoreTailsMagicData] '{name}' restoreCount {_restoreCount}는 1로 맞춥니다.", this);
            _restoreCount = 1;
        }
    }

    /// <inheritdoc />
    public override bool CanCast(GameObject caster)
    {
        IScaleMagazine magazine = FindMagazine(caster);
        if (magazine != null && magazine.EmptyCount > 0)
            return true;

        Debug.Log("[RestoreTailsMagicData] 빈 비늘 칸이 없어 시전하지 않습니다.");
        return false;
    }

    /// <inheritdoc />
    public override void Cast(GameObject caster, Vector3 targetPos)
    {
        if (caster == null)
            return;

        IScaleMagazine magazine = FindMagazine(caster);
        if (magazine == null)
        {
            Debug.LogWarning($"[RestoreTailsMagicData] '{name}' 시전자에게 비늘 탄창이 없습니다.", caster);
            return;
        }

        int restored = magazine.RestoreRegeneratingScales(RestoreCount);
        if (restored == 0)
            Debug.Log("[RestoreTailsMagicData] 자라게 할 빈 비늘 칸이 없습니다.");
    }

    static IScaleMagazine FindMagazine(GameObject caster)
    {
        if (caster == null)
            return null;

        ScaleSpender spender = caster.GetComponentInParent<ScaleSpender>();
        if (spender != null)
            return spender.Magazine;

        return caster.GetComponentInParent<IScaleMagazine>();
    }
}
