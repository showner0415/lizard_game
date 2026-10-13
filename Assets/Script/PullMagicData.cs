using UnityEngine;

/// <summary>마우스 지점 반경 안의 파리를 플레이어에게 붙잡는다.</summary>
[CreateAssetMenu(fileName = "PullMagic", menuName = "Magic/Pull")]
public class PullMagicData : MagicData
{
    const float MinDistance = 0.5f;
    const int FlyLayer = 11;
    const int BufferSize = 32;

    static readonly Collider[] OverlapBuffer = new Collider[BufferSize];

    [SerializeField, Min(0.5f), Tooltip("파리를 붙잡는 반경.")]
    float _radius = 3f;

    [SerializeField, Min(0f), Tooltip("플레이어에게 날아오는 속도.")]
    float _pullSpeed = 8f;

    [SerializeField, Min(MinDistance), Tooltip("시전자로부터 올가미를 놓을 수 있는 최대 거리.")]
    float _maxPlaceDistance = 8f;

    [SerializeField, Tooltip("파리 레이어. 비어 있으면 아무것도 붙잡지 않는다.")]
    LayerMask _hitMask = 1 << FlyLayer;

    protected override void OnValidate()
    {
        base.OnValidate();
        if (float.IsNaN(_radius) || float.IsInfinity(_radius) || _radius < 0.5f)
        {
            Debug.LogWarning($"[PullMagicData] '{name}' radius {_radius}는 0.5로 맞춥니다.", this);
            _radius = 0.5f;
        }

        if (float.IsNaN(_pullSpeed) || float.IsInfinity(_pullSpeed) || _pullSpeed < 0f)
        {
            Debug.LogWarning($"[PullMagicData] '{name}' pullSpeed {_pullSpeed}는 0으로 맞춥니다.", this);
            _pullSpeed = 0f;
        }

        if (float.IsNaN(_maxPlaceDistance) || float.IsInfinity(_maxPlaceDistance) || _maxPlaceDistance < MinDistance)
        {
            Debug.LogWarning($"[PullMagicData] '{name}' maxPlaceDistance {_maxPlaceDistance}는 {MinDistance}로 맞춥니다.", this);
            _maxPlaceDistance = MinDistance;
        }
    }

    /// <inheritdoc />
    public override void Cast(GameObject caster, Vector3 targetPos)
    {
        if (caster == null)
            return;

        if (_hitMask.value == 0)
        {
            Debug.LogWarning($"[PullMagicData] '{name}'에 파리 레이어가 없습니다.", this);
            return;
        }

        Vector3 point = ClampToRange(caster.transform.position, targetPos);
        int count = Physics.OverlapSphereNonAlloc(
            point,
            _radius < 0.5f ? 0.5f : _radius,
            OverlapBuffer,
            _hitMask,
            QueryTriggerInteraction.Collide);

        int caught = 0;
        for (int i = 0; i < count; i++)
        {
            Collider other = OverlapBuffer[i];
            if (other == null)
                continue;

            IPullable pullable = other.GetComponentInParent<IPullable>();
            if (pullable == null)
                continue;

            pullable.BeginPull(caster.transform, _pullSpeed);
            caught++;
        }

        if (caught == 0)
            Debug.Log($"[PullMagicData] '{name}' 범위 안에 붙잡을 파리가 없습니다.");
    }

    Vector3 ClampToRange(Vector3 origin, Vector3 target)
    {
        Vector3 flat = target - origin;
        flat.y = 0f;
        float max = _maxPlaceDistance < MinDistance ? MinDistance : _maxPlaceDistance;
        if (flat.sqrMagnitude <= max * max)
            return target;

        Vector3 clamped = origin + flat.normalized * max;
        clamped.y = target.y;
        return clamped;
    }
}
