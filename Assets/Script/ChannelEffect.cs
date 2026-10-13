using UnityEngine;

/// <summary>
/// 누르는 동안 시전자에 붙어 마우스 방향을 본다.
/// 누구를 맞히는지는 하위 클래스가 정한다.
/// </summary>
public abstract class ChannelEffect : MonoBehaviour
{
    const float MinTick = 0.05f;
    const float MinRange = 0.5f;

    [SerializeField, Min(MinTick), Tooltip("피해를 주는 간격(초).")]
    float _tickInterval = 0.25f;

    [SerializeField, Min(0f), Tooltip("틱마다 주는 피해.")]
    float _damage = 4f;

    [SerializeField, Min(MinRange), Tooltip("대상을 찾는 거리.")]
    float _range = 4f;

    [SerializeField, Tooltip("대상을 찾을 레이어. 비어 있으면 모든 레이어.")]
    LayerMask _hitMask;

    DamageSource _source;
    GameObject _caster;
    MouseRayCaster _aim;
    float _nextTick;
    bool _running;

    /// <summary>틱마다 주는 피해.</summary>
    protected float Damage => _damage;

    /// <summary>대상을 찾는 거리.</summary>
    protected float Range => _range < MinRange ? MinRange : _range;

    /// <summary>대상 레이어. 비어 있으면 모든 레이어.</summary>
    protected LayerMask HitMask => _hitMask;

    /// <summary>이 효과를 시전한 사람.</summary>
    protected GameObject Caster => _caster;

    /// <summary>피해 출처. 채널링은 시전자를 무시하는 출처를 받는다.</summary>
    protected DamageSource Source => _source;

    /// <summary>시전자에게 붙이고, 바로 한 번 맞힌 뒤 간격마다 다시 맞힌다.</summary>
    public void Begin(DamageSource source, MouseRayCaster aim)
    {
        _source = source;
        _caster = source.Caster;
        _aim = aim;
        _running = true;
        _nextTick = Time.time;

        if (_caster != null)
        {
            transform.SetParent(_caster.transform, false);
            transform.localPosition = Vector3.zero;
        }
    }

    /// <summary>효과를 끝낸다.</summary>
    public void Stop()
    {
        if (!_running)
            return;

        _running = false;
        Destroy(gameObject);
    }

    void Update()
    {
        if (!_running)
            return;

        if (_caster == null)
        {
            Stop();
            return;
        }

        AimAtMouse();
        if (Time.time < _nextTick)
            return;

        _nextTick = Time.time + Mathf.Max(_tickInterval, MinTick);
        Tick();
    }

    /// <summary>이번 틱에 누구를 맞힐지 정한다.</summary>
    protected abstract void Tick();

    void AimAtMouse()
    {
        if (_aim == null || !_aim.TryGetMouseWorldPosition(out Vector3 point))
            return;

        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }
}
