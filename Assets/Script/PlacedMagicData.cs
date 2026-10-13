using UnityEngine;

/// <summary>마우스 지점에 구역 프리팹을 놓는 마법.</summary>
[CreateAssetMenu(fileName = "PlacedMagic", menuName = "Magic/Placed")]
public class PlacedMagicData : MagicData
{
    const float MinDistance = 0.5f;

    [SerializeField, Tooltip("놓을 구역 프리팹. MagicZone이 붙어 있어야 한다.")]
    GameObject _zonePrefab;

    [SerializeField, Min(MinDistance), Tooltip("시전자로부터 놓을 수 있는 최대 거리.")]
    float _maxPlaceDistance = 8f;

    /// <summary>놓을 수 있는 최대 거리.</summary>
    public float MaxPlaceDistance => _maxPlaceDistance < MinDistance ? MinDistance : _maxPlaceDistance;

    protected override void OnValidate()
    {
        base.OnValidate();
        if (float.IsNaN(_maxPlaceDistance) || float.IsInfinity(_maxPlaceDistance) || _maxPlaceDistance < MinDistance)
        {
            Debug.LogWarning($"[PlacedMagicData] '{name}' maxPlaceDistance {_maxPlaceDistance}는 {MinDistance}로 맞춥니다.", this);
            _maxPlaceDistance = MinDistance;
        }
    }

    /// <inheritdoc />
    public override void Cast(GameObject caster, Vector3 targetPos)
    {
        if (caster == null)
            return;

        if (_zonePrefab == null)
        {
            Debug.LogWarning($"[PlacedMagicData] '{name}'에 구역 프리팹이 없습니다.", this);
            return;
        }

        Vector3 point = ClampToRange(caster.transform.position, targetPos);
        GameObject obj = Instantiate(_zonePrefab, point, Quaternion.identity);
        MagicZone zone = obj.GetComponent<MagicZone>();
        if (zone == null)
        {
            Debug.LogWarning($"[PlacedMagicData] '{name}' 프리팹에 MagicZone이 없어 제거합니다.", _zonePrefab);
            Destroy(obj);
            return;
        }

        zone.Ignite(CreateSource(caster));
    }

    Vector3 ClampToRange(Vector3 origin, Vector3 target)
    {
        Vector3 flat = target - origin;
        flat.y = 0f;
        float max = MaxPlaceDistance;
        if (flat.sqrMagnitude <= max * max)
            return target;

        Vector3 clamped = origin + flat.normalized * max;
        clamped.y = target.y;
        return clamped;
    }
}
