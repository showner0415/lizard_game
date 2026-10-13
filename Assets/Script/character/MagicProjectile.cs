using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 날아가다 맞은 대상에게 효과를 적용하는 발사체.
/// 대상을 찾는 일과 효과를 적용하는 일을 나눠, 나중에는 효과 목록을 같은 자리에 넣는다.
/// </summary>
public class MagicProjectile : MonoBehaviour
{
    [SerializeField, Tooltip("이 레이어에 닿으면 관통과 상관없이 사라진다. 비어 있으면 지형을 통과한다.")]
    LayerMask _stopLayers;

    [SerializeField, Tooltip("폭발이 찾을 레이어. 비어 있으면 모든 레이어.")]
    LayerMask _explosionMask;

    ProjectileMagicData _data;
    Rigidbody _rb;
    Vector3 _direction;
    Vector3 _startPosition;
    int _remainingPierce;
    bool _spent;
    DamageSource _directSource;
    DamageSource _areaSource;
    readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();

    /// <summary>데이터, 진행 방향, 피해 출처를 받고 날리기 시작한다. 직격은 시전자를 무시한다.</summary>
    public void Initialize(ProjectileMagicData magicData, Vector3 dir, DamageSource source)
    {
        _data = magicData;
        _areaSource = source;
        _directSource = source.IgnoringCaster();
        _startPosition = transform.position;
        _direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        _remainingPierce = magicData != null ? magicData.PierceCount : 0;
        _rb = GetComponent<Rigidbody>();

        if (_rb == null)
            Debug.LogWarning("[MagicProjectile] Rigidbody가 없습니다.", this);

        if (_data != null)
            Destroy(gameObject, _data.lifeTime);
    }

    /// <summary>이동한 뒤, 사거리를 넘었으면 사라진다.</summary>
    protected virtual void FixedUpdate()
    {
        if (_spent || _data == null)
            return;

        Move();

        if (_data.MaxRange <= 0f)
            return;

        Vector3 delta = transform.position - _startPosition;
        if (delta.sqrMagnitude > _data.MaxRange * _data.MaxRange)
            Despawn(transform.position, false);
    }

    /// <summary>이동 방식에 따라 속도를 덮어쓴다.</summary>
    protected virtual void Move()
    {
        if (_rb == null || _data == null)
            return;

        switch (_data.moveType)
        {
            case MagicMoveType.Straight:
                _rb.linearVelocity = _direction * _data.speed;
                break;

            // 매 단계 위쪽 속도를 다시 넣으므로 포물선이 아니라 계속 올라간다.
            case MagicMoveType.Parabola:
                _rb.linearVelocity = _direction * _data.speed + Vector3.up * 5f;
                break;

            // 아직 추적하지 않는다. 이 타입이면 제자리에 남는다.
            case MagicMoveType.Homing:
                break;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    /// <summary>맞은 콜라이더에서 대상을 찾고, 있으면 효과를 적용한다.</summary>
    protected void HandleHit(Collider other)
    {
        if (_spent || other == null || _data == null)
            return;

        if (_areaSource.IsCaster(other.transform))
            return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null && _directSource.ScaleFor(target) <= 0f)
            return;

        if (target == null)
        {
            if (IsStopLayer(other.gameObject.layer))
                Despawn(other.ClosestPoint(transform.position), true);
            return;
        }

        if (!_hitTargets.Add(target))
            return;

        ApplyEffects(target);

        if (_remainingPierce == 0)
            Despawn(other.ClosestPoint(transform.position), true);
        else if (_remainingPierce > 0)
            _remainingPierce--;
    }

    /// <summary>맞은 대상에게 효과를 적용한다. 지금은 피해만 준다.</summary>
    protected virtual void ApplyEffects(IDamageable target)
    {
        if (target == null || _data == null)
            return;

        MagicDamage.Hit(target, _data.damage, _directSource);
    }

    /// <summary>한 번만 사라진다. 충돌로 막혔고 폭발 반경이 있으면 그 지점에서 범위 피해를 준다.</summary>
    protected void Despawn(Vector3 point, bool impact)
    {
        if (_spent)
            return;

        _spent = true;
        if (impact && _data != null && _data.ExplosionRadius > 0f)
            MagicDamage.HitArea(point, _data.ExplosionRadius, _data.damage, _explosionMask, _areaSource, _hitTargets);

        Destroy(gameObject);
    }

    bool IsStopLayer(int layer)
    {
        return (_stopLayers.value & (1 << layer)) != 0;
    }
}
