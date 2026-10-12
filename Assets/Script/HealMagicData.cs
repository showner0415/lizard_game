using UnityEngine;

/// <summary>시전자의 HP를 회복하는 마법.</summary>
[CreateAssetMenu(fileName = "HealMagic", menuName = "Magic/Heal")]
public class HealMagicData : MagicData
{
    /// <summary>회복량.</summary>
    [Min(0)] public float healAmount;

    /// <summary>시전자 위치에 만들 효과 프리팹. 비우면 효과 없음.</summary>
    public GameObject effectPrefab;

    /// <inheritdoc />
    public override void Cast(GameObject caster, Vector3 targetPos)
    {
        if (caster == null)
            return;

        PlayerStats stats = caster.GetComponent<PlayerStats>();
        if (stats != null)
            stats.Heal(Mathf.RoundToInt(healAmount));

        if (effectPrefab != null)
            Instantiate(effectPrefab, caster.transform.position, Quaternion.identity);
    }
}
