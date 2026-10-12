using UnityEngine;

/// <summary>투사체 이동 방식.</summary>
public enum MagicMoveType
{
    /// <summary>직선.</summary>
    Straight,

    /// <summary>위로 뜨는 포물선.</summary>
    Parabola,

    /// <summary>유도. 회전 속도는 <see cref="ProjectileMagicData.homingTurnSpeed"/>.</summary>
    Homing
}

/// <summary>프리팹을 날려 피해를 주는 마법.</summary>
[CreateAssetMenu(fileName = "ProjectileMagic", menuName = "Magic/Projectile")]
public class ProjectileMagicData : MagicData
{
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

    private const float MinPlanarDirectionSqr = 0.0001f;

    /// <inheritdoc />
    public override void Cast(GameObject caster, Vector3 targetPos)
    {
        if (caster == null)
            return;

        Transform origin = caster.transform;
        PlayerAttack attack = caster.GetComponent<PlayerAttack>();
        if (attack != null && attack.firePoint != null)
            origin = attack.firePoint;

        Vector3 dir = targetPos - origin.position;
        dir.y = 0f;
        if (dir.sqrMagnitude <= MinPlanarDirectionSqr)
            dir = origin.forward;
        dir.Normalize();

        GameObject obj = Instantiate(
            projectilePrefab,
            origin.position,
            Quaternion.LookRotation(dir));

        obj.GetComponent<MagicProjectile>().Initialize(this, dir);
    }
}
