using System.Collections.Generic;
using UnityEngine;

/// <summary>시전자 앞의 부채꼴 안에 있는 대상에게 틱마다 피해를 준다.</summary>
public class ConeChannelEffect : ChannelEffect
{
    const int BufferSize = 64;

    [SerializeField, Range(1f, 180f), Tooltip("부채꼴 전체 각도.")]
    float _angle = 50f;

    readonly Collider[] _buffer = new Collider[BufferSize];
    readonly HashSet<IDamageable> _seen = new HashSet<IDamageable>();

    /// <inheritdoc />
    protected override void Tick()
    {
        Vector3 origin = Caster != null ? Caster.transform.position : transform.position;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        float half = Mathf.Clamp(_angle, 1f, 180f) * 0.5f;
        _seen.Clear();
        int count = Physics.OverlapSphereNonAlloc(
            origin,
            Range,
            _buffer,
            MagicDamage.MaskOrAll(HitMask),
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider other = _buffer[i];
            if (other == null)
                continue;

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || Source.ScaleFor(target) <= 0f)
                continue;

            Vector3 to = ((Component)target).transform.position - origin;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f || Vector3.Angle(forward, to) > half)
                continue;

            if (!_seen.Add(target))
                continue;

            MagicDamage.Hit(target, Damage, Source);
        }
    }
}
