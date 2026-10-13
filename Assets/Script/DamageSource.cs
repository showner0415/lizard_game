using UnityEngine;

/// <summary>마법 피해의 출처. 시전자와, 시전자에게 적용할 배율을 가진다.</summary>
public struct DamageSource
{
    /// <summary>시전한 오브젝트. 없으면 모든 대상이 배율 1이다.</summary>
    public GameObject Caster { get; }

    /// <summary>시전자 본인과 그 자식에게 곱할 배율. 0이면 맞지 않는다.</summary>
    public float SelfScale { get; }

    /// <summary>시전자와 자기 피해 배율로 출처를 만든다.</summary>
    public DamageSource(GameObject caster, float selfScale)
    {
        Caster = caster;
        SelfScale = Mathf.Clamp01(selfScale);
    }

    /// <summary>대상이 시전자 쪽이면 자기 피해 배율, 아니면 1. 시전자가 없으면 1.</summary>
    public float ScaleFor(IDamageable target)
    {
        if (Caster == null || target is not Component component || component == null || !IsCaster(component.transform))
            return 1f;

        return SelfScale;
    }

    /// <summary>이 트랜스폼이 시전자 본인이거나 그 자식이면 true. 시전자가 없으면 false.</summary>
    public bool IsCaster(Transform hit)
    {
        if (Caster == null || hit == null)
            return false;

        Transform caster = Caster.transform;
        return hit == caster || hit.IsChildOf(caster);
    }

    /// <summary>직격과 채널링처럼 시전자를 항상 무시하는 복사본.</summary>
    public DamageSource IgnoringCaster()
    {
        return new DamageSource(Caster, 0f);
    }
}
