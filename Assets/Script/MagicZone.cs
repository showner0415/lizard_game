using System.Collections;
using UnityEngine;

/// <summary>장판이 피해를 주는 시점.</summary>
public enum ZoneTriggerMode
{
    /// <summary>안에 있는 적에게 일정 간격으로 피해를 준다.</summary>
    Tick = 0,

    /// <summary>처음 밟은 적에게 한 번 주고 사라진다.</summary>
    OnEnter = 1
}

/// <summary>바닥에 놓인 마법 구역. 독안개처럼 지속되거나, 함정처럼 한 번 발동한다.</summary>
public class MagicZone : MonoBehaviour
{
    const float MinRadius = 0.25f;
    const float MinDuration = 0.05f;
    const float MinTick = 0.05f;

    [SerializeField, Min(MinRadius), Tooltip("구역 반경.")]
    float _radius = 2f;

    [SerializeField, Min(MinDuration), Tooltip("이 시간이 지나면 사라진다.")]
    float _duration = 4f;

    [SerializeField, Min(0f), Tooltip("한 번에 주는 피해.")]
    float _damage = 5f;

    [SerializeField, Tooltip("Tick은 간격마다, OnEnter는 처음 들어온 대상 한 번.")]
    ZoneTriggerMode _triggerMode = ZoneTriggerMode.Tick;

    [SerializeField, Min(MinTick), Tooltip("Tick 모드에서 피해 간격(초).")]
    float _tickInterval = 0.5f;

    [SerializeField, Tooltip("대상을 찾을 레이어. 비어 있으면 모든 레이어.")]
    LayerMask _hitMask;

    DamageSource _source;
    bool _spent;

    /// <summary>이 장판의 피해 출처를 정한다. 자기 피해 배율은 출처가 가진다.</summary>
    public void Ignite(DamageSource source)
    {
        _source = source;
    }

    void Awake()
    {
        if (float.IsNaN(_radius) || float.IsInfinity(_radius) || _radius < MinRadius)
        {
            Debug.LogWarning($"[MagicZone] radius {_radius}는 {MinRadius}로 맞춥니다.", this);
            _radius = MinRadius;
        }

        if (float.IsNaN(_duration) || float.IsInfinity(_duration) || _duration < MinDuration)
        {
            Debug.LogWarning($"[MagicZone] duration {_duration}는 {MinDuration}로 맞춥니다.", this);
            _duration = MinDuration;
        }

        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = gameObject.AddComponent<SphereCollider>();

        sphere.isTrigger = true;
        sphere.radius = _radius;
    }

    void OnEnable()
    {
        StartCoroutine(Run());
    }

    void OnTriggerEnter(Collider other)
    {
        if (_spent || _triggerMode != ZoneTriggerMode.OnEnter || other == null)
            return;

        if (MagicDamage.Hit(other, _damage, _source))
            Despawn();
    }

    IEnumerator Run()
    {
        float end = Time.time + _duration;
        if (_triggerMode == ZoneTriggerMode.OnEnter)
        {
            while (Time.time < end && !_spent)
                yield return null;

            Despawn();
            yield break;
        }

        while (Time.time < end && !_spent)
        {
            Tick();
            yield return new WaitForSeconds(_tickInterval);
        }

        Despawn();
    }

    void Tick()
    {
        MagicDamage.HitArea(transform.position, _radius, _damage, _hitMask, _source);
    }

    void Despawn()
    {
        if (_spent)
            return;

        _spent = true;
        Destroy(gameObject);
    }
}
