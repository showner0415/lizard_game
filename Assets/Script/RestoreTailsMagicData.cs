using UnityEngine;

/// <summary>재생 중인 꼬리를 즉시 되돌리는 마법.</summary>
[CreateAssetMenu(fileName = "RestoreTailsMagic", menuName = "Magic/Restore Tails")]
public class RestoreTailsMagicData : MagicData
{
    [SerializeField, Min(1), Tooltip("한 번에 되돌릴 꼬리 수.")]
    int _restoreCount = 1;

    /// <summary>되돌릴 꼬리 수. 1 이상.</summary>
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
        ITailProvider provider = FindProvider(caster);
        if (provider != null && provider.RegeneratingTailCount > 0)
            return true;

        Debug.Log("[RestoreTailsMagicData] 재생 중인 꼬리가 없어 시전하지 않습니다.");
        return false;
    }

    /// <inheritdoc />
    public override void Cast(GameObject caster, Vector3 targetPos)
    {
        if (caster == null)
            return;

        ITailProvider provider = FindProvider(caster);
        if (provider == null)
        {
            Debug.LogWarning($"[RestoreTailsMagicData] '{name}' 시전자에게 꼬리가 없습니다.", caster);
            return;
        }

        int restored = provider.RestoreRegeneratingTails(RestoreCount);
        if (restored == 0)
            Debug.Log("[RestoreTailsMagicData] 되돌릴 재생 중인 꼬리가 없습니다.");
    }

    static ITailProvider FindProvider(GameObject caster)
    {
        if (caster == null)
            return null;

        SpellCaster spellCaster = caster.GetComponentInParent<SpellCaster>();
        if (spellCaster != null)
            return spellCaster.TailProvider;

        return caster.GetComponentInParent<ITailProvider>();
    }
}
