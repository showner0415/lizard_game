using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마법이 대상에게 피해를 주는 곳. 발사체, 장판, 채널링이 모두 여기를 부른다.
/// </summary>
public static class MagicDamage
{
    const int BufferSize = 64;

    static readonly Collider[] OverlapBuffer = new Collider[BufferSize];
    static readonly HashSet<IDamageable> Seen = new HashSet<IDamageable>();

    /// <summary>콜라이더의 부모에서 피해 대상을 찾아 피해를 준다. 배율이 0 이하이거나 대상이 없으면 false.</summary>
    public static bool Hit(Collider other, float damage, DamageSource source)
    {
        if (other == null)
            return false;

        return Hit(other.GetComponentInParent<IDamageable>(), damage, source);
    }

    /// <summary>출처 배율을 곱해 피해를 준다. 배율이 0 이하이거나 대상이 없으면 false.</summary>
    public static bool Hit(IDamageable target, float damage, DamageSource source)
    {
        if (!IsAlive(target))
            return false;

        float scale = source.ScaleFor(target);
        if (scale <= 0f)
            return false;

        target.TakeDamage(damage * scale);
        return true;
    }

    /// <summary>
    /// 범위 안의 대상에게 한 번씩 피해를 준다. 같은 대상의 콜라이더가 여러 개여도 한 번이다.
    /// 마스크가 비어 있으면 모든 레이어를 본다. <paramref name="skip"/>에 있는 대상은 넘긴다.
    /// 시전자 배율은 <paramref name="source"/>가 정한다.
    /// </summary>
    public static int HitArea(Vector3 center, float radius, float damage, LayerMask mask, DamageSource source, HashSet<IDamageable> skip = null)
    {
        if (radius <= 0f)
            return 0;

        int count = Physics.OverlapSphereNonAlloc(
            center,
            radius,
            OverlapBuffer,
            MaskOrAll(mask),
            QueryTriggerInteraction.Collide);

        Seen.Clear();
        int hit = 0;
        for (int i = 0; i < count; i++)
        {
            Collider other = OverlapBuffer[i];
            if (other == null)
                continue;

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (!IsAlive(target))
                continue;

            if (skip != null && skip.Contains(target))
                continue;

            if (!Seen.Add(target))
                continue;

            if (Hit(target, damage, source))
                hit++;
        }

        return hit;
    }

    /// <summary>비어 있는 마스크는 모든 레이어로 본다.</summary>
    public static int MaskOrAll(LayerMask mask)
    {
        return mask.value == 0 ? Physics.AllLayers : mask.value;
    }

    static bool IsAlive(IDamageable target)
    {
        if (target == null)
            return false;

        return target is not Object unityObject || unityObject != null;
    }
}
