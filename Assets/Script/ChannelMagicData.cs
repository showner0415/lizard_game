using UnityEngine;

/// <summary>누르는 동안 유지되는 마법. 시전은 <see cref="BeginChannel"/>로 시작하고, 뗄 때 효과를 멈춘다.</summary>
[CreateAssetMenu(fileName = "ChannelMagic", menuName = "Magic/Channel")]
public class ChannelMagicData : MagicData
{
    [SerializeField, Tooltip("시전자에 붙일 효과 프리팹. ChannelEffect가 붙어 있어야 한다.")]
    GameObject _effectPrefab;

    /// <summary>효과를 만들어 시전자에게 붙인다. 프리팹에 효과가 없으면 null.</summary>
    public ChannelEffect BeginChannel(GameObject caster, MouseRayCaster aim)
    {
        if (caster == null)
            return null;

        if (_effectPrefab == null)
        {
            Debug.LogWarning($"[ChannelMagicData] '{name}'에 효과 프리팹이 없습니다.", this);
            return null;
        }

        GameObject obj = Instantiate(_effectPrefab, caster.transform);
        ChannelEffect effect = obj.GetComponent<ChannelEffect>();
        if (effect == null)
        {
            Debug.LogWarning($"[ChannelMagicData] '{name}' 프리팹에 ChannelEffect가 없어 제거합니다.", _effectPrefab);
            Destroy(obj);
            return null;
        }

        effect.Begin(CreateSource(caster).IgnoringCaster(), aim);
        return effect;
    }

    /// <inheritdoc />
    public override void Cast(GameObject caster, Vector3 targetPos)
    {
        Debug.LogWarning($"[ChannelMagicData] '{name}'은 Cast가 아니라 BeginChannel로 시작합니다.", this);
    }
}
