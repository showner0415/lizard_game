using System.Collections.Generic;
using UnityEngine;

/// <summary>가장 가까운 적부터 시작해, 옆 적으로 이어 맞힌다.</summary>
public class ChainChannelEffect : ChannelEffect
{
    const int BufferSize = 64;

    [SerializeField, Min(0), Tooltip("처음 대상을 뺀 연결 수. 2면 처음 적과 그 옆 둘까지.")]
    int _chainCount = 2;

    [SerializeField, Min(0.5f), Tooltip("이전 대상에서 다음 대상을 찾는 거리.")]
    float _linkRange = 6f;

    readonly Collider[] _buffer = new Collider[BufferSize];
    readonly HashSet<IDamageable> _seen = new HashSet<IDamageable>();

    /// <inheritdoc />
    protected override void Tick()
    {
        _seen.Clear();
        Vector3 origin = Caster != null ? Caster.transform.position : transform.position;
        if (!TryFindNearest(origin, Range, out IDamageable first, out Vector3 at))
            return;

        MagicDamage.Hit(first, Damage, Source);
        _seen.Add(first);

        int links = _chainCount < 0 ? 0 : _chainCount;
        for (int i = 0; i < links; i++)
        {
            if (!TryFindNearest(at, _linkRange, out IDamageable next, out Vector3 nextAt))
                return;

            MagicDamage.Hit(next, Damage, Source);
            _seen.Add(next);
            at = nextAt;
        }
    }

    bool TryFindNearest(Vector3 from, float radius, out IDamageable nearest, out Vector3 at)
    {
        nearest = null;
        at = from;
        if (radius <= 0f)
            return false;

        int count = Physics.OverlapSphereNonAlloc(
            from,
            radius,
            _buffer,
            MagicDamage.MaskOrAll(HitMask),
            QueryTriggerInteraction.Collide);

        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider other = _buffer[i];
            if (other == null)
                continue;

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || Source.ScaleFor(target) <= 0f || _seen.Contains(target))
                continue;

            Vector3 pos = ((Component)target).transform.position;
            float dist = (pos - from).sqrMagnitude;
            if (dist >= best)
                continue;

            best = dist;
            nearest = target;
            at = pos;
        }

        return nearest != null;
    }
}
