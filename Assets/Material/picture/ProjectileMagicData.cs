using UnityEngine;

/// <summary>투사체 이동 방식.</summary>
public enum MagicMoveType
{
    /// <summary>직선.</summary>
    Straight,

    /// <summary>위로 뜨는 포물선.</summary>
    Parabola,

    /// <summary>유도. 회전 속도는 <see cref="ProjectileMagicData.HomingTurnSpeed"/>.</summary>
    Homing
}

/// <summary>프리팹을 날려 피해를 주는 마법. 몇 발을 어느 각도로 쏠지만 정한다.</summary>
[CreateAssetMenu(fileName = "ProjectileMagic", menuName = "Magic/Projectile")]
public class ProjectileMagicData : MagicData
{
    const float MinPlanarDirectionSqr = 0.0001f;

    /// <summary>발사할 투사체 프리팹.</summary>
    public GameObject projectilePrefab;

    /// <summary>적중 시 피해.</summary>
    public float damage = 10f;

    /// <summary>이동 속도.</summary>
    public float speed = 10f;

    /// <summary>자동으로 사라지기까지 시간(초).</summary>
    public float lifeTime = 5f;

    /// <summary>이동 방식.</summary>
    public MagicMoveType moveType;

    [Header("Homing")]
    /// <summary>Homing일 때만 사용하는 회전 속도.</summary>
    public float homingTurnSpeed;

    [Header("Pattern")]
    [SerializeField, Min(1), Tooltip("한 번에 쏘는 발 수. 1이면 한 발.")]
    int projectileCount = 1;

    [SerializeField, Min(0f), Tooltip("부채꼴 전체 각도. 0이면 한 방향. 3발에 30이면 -15, 0, +15.")]
    float spreadAngle;

    [SerializeField, Min(0f), Tooltip("최대 비행 거리. 0이면 lifeTime만 사용한다.")]
    float maxRange;

    [SerializeField, Tooltip("뚫고 지나갈 수 있는 적 수. 0이면 첫 적에서 사라지고, -1이면 무제한.")]
    int pierceCount;

    [SerializeField, Min(0f), Tooltip("0보다 크면 막힌 지점에서 이 반경으로 범위 피해를 준다. 0이면 폭발 없음.")]
    float explosionRadius;

    /// <summary>한 번에 쏘는 발 수. 1 이상.</summary>
    public int ProjectileCount => projectileCount < 1 ? 1 : projectileCount;

    /// <summary>부채꼴 전체 각도. 0 이상.</summary>
    public float SpreadAngle => spreadAngle < 0f ? 0f : spreadAngle;

    /// <summary>최대 비행 거리. 0이면 사거리 제한 없음.</summary>
    public float MaxRange => maxRange < 0f ? 0f : maxRange;

    /// <summary>뚫고 지나갈 수 있는 적 수. -1이면 무제한.</summary>
    public int PierceCount => pierceCount < -1 ? -1 : pierceCount;

    /// <summary>막힌 지점의 폭발 반경. 0이면 폭발하지 않는다.</summary>
    public float ExplosionRadius => explosionRadius < 0f ? 0f : explosionRadius;

    /// <summary>Homing일 때 회전 속도.</summary>
    public float HomingTurnSpeed => homingTurnSpeed;

    protected override void OnValidate()
    {
        base.OnValidate();
        if (projectileCount < 1)
        {
            Debug.LogWarning($"[ProjectileMagicData] '{name}' projectileCount {projectileCount}는 1로 맞춥니다.", this);
            projectileCount = 1;
        }

        if (float.IsNaN(spreadAngle) || float.IsInfinity(spreadAngle) || spreadAngle < 0f)
        {
            Debug.LogWarning($"[ProjectileMagicData] '{name}' spreadAngle {spreadAngle}는 0으로 맞춥니다.", this);
            spreadAngle = 0f;
        }

        if (float.IsNaN(maxRange) || float.IsInfinity(maxRange) || maxRange < 0f)
        {
            Debug.LogWarning($"[ProjectileMagicData] '{name}' maxRange {maxRange}는 0으로 맞춥니다.", this);
            maxRange = 0f;
        }

        if (pierceCount < -1)
        {
            Debug.LogWarning($"[ProjectileMagicData] '{name}' pierceCount {pierceCount}는 -1로 맞춥니다.", this);
            pierceCount = -1;
        }

        if (float.IsNaN(explosionRadius) || float.IsInfinity(explosionRadius) || explosionRadius < 0f)
        {
            Debug.LogWarning($"[ProjectileMagicData] '{name}' explosionRadius {explosionRadius}는 0으로 맞춥니다.", this);
            explosionRadius = 0f;
        }
    }

    /// <inheritdoc />
    public override void Cast(GameObject caster, Vector3 targetPos)
    {
        if (caster == null)
            return;

        if (projectilePrefab == null)
        {
            Debug.LogWarning($"[ProjectileMagicData] '{name}'에 발사체 프리팹이 없습니다.", this);
            return;
        }

        Transform origin = caster.transform;
        PlayerAttack attack = caster.GetComponent<PlayerAttack>();
        if (attack != null && attack.firePoint != null)
            origin = attack.firePoint;

        Vector3 dir = targetPos - origin.position;
        dir.y = 0f;
        if (dir.sqrMagnitude <= MinPlanarDirectionSqr)
            dir = FlatForward(origin);
        dir.Normalize();

        int count = ProjectileCount;
        float arc = SpreadAngle;
        for (int i = 0; i < count; i++)
        {
            float angle = 0f;
            if (count > 1)
                angle = Mathf.Lerp(-arc * 0.5f, arc * 0.5f, i / (float)(count - 1));

            Vector3 shotDir = Quaternion.AngleAxis(angle, Vector3.up) * dir;
            Spawn(origin.position, shotDir, CreateSource(caster));
        }
    }

    static Vector3 FlatForward(Transform origin)
    {
        Vector3 forward = origin.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= MinPlanarDirectionSqr)
            return Vector3.forward;
        return forward.normalized;
    }

    void Spawn(Vector3 position, Vector3 shotDir, DamageSource source)
    {
        GameObject obj = Instantiate(projectilePrefab, position, Quaternion.LookRotation(shotDir));
        MagicProjectile projectile = obj.GetComponent<MagicProjectile>();
        if (projectile == null)
        {
            Debug.LogWarning($"[ProjectileMagicData] '{name}' 프리팹에 MagicProjectile이 없어 제거합니다.", projectilePrefab);
            Destroy(obj);
            return;
        }

        projectile.Initialize(this, shotDir, source);
    }
}
