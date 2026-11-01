using UnityEngine;
using UnityEngine.Serialization;

/// <summary>발사로 지금 선택된 비늘을 장전하는 꼬리 장식 마법. 비늘 칸은 쓰지 않고, 마나는 마법 비용만 한 번 낸다.</summary>
[CreateAssetMenu(fileName = "LoadScalesMagic", menuName = "Magic/Load Scales")]
public class LoadScalesMagicData : MagicData
{
    [FormerlySerializedAs("_restoreCount")]
    [SerializeField, Min(1), Tooltip("한 번에 장전할 칸 수.")]
    int _loadCount = 1;

    /// <summary>한 번에 장전할 칸 수. 1 이상.</summary>
    public int LoadCount => _loadCount < 1 ? 1 : _loadCount;

    /// <inheritdoc />
    public override bool RequiresScale => false;

    /// <inheritdoc />
    public override bool NeedsTargetPoint => false;

    protected override void OnValidate()
    {
        base.OnValidate();
        if (_loadCount < 1)
        {
            Debug.LogWarning($"[LoadScalesMagicData] '{name}' loadCount {_loadCount}는 1로 맞춥니다.", this);
            _loadCount = 1;
        }
    }

    /// <inheritdoc />
    public override bool CanCast(GameObject caster, out string failReason)
    {
        IInstantScaleLoader loader = FindLoader(caster);
        if (loader == null)
        {
            failReason = NoticeText.CannotUseNow;
            return false;
        }

        return loader.CanLoadNow(LoadCount, out failReason);
    }

    /// <inheritdoc />
    public override void Cast(GameObject caster, Vector3 targetPos)
    {
        if (caster == null)
            return;

        IInstantScaleLoader loader = FindLoader(caster);
        if (loader == null || loader.LoadNow(LoadCount) == 0)
            Debug.LogWarning("[LoadScalesMagicData] 장전할 수 있다고 확인한 직후 장전에 실패했습니다.", this);
    }

    static IInstantScaleLoader FindLoader(GameObject caster)
    {
        if (caster == null)
            return null;

        IInstantScaleLoader loader = caster.GetComponentInParent<IInstantScaleLoader>();
        if (loader != null)
            return loader;

        return caster.GetComponentInChildren<IInstantScaleLoader>(true);
    }
}
