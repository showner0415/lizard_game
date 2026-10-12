using UnityEngine;

public class EnemyProjectileAttack : EnemyAttackBase
{
    public GameObject projectilePrefab;
    public Transform firePoint;
    public int attackPower = 10;

    protected override void Attack(Transform target)
    {
        Vector3 dir = target.position - firePoint.position;
        dir.y = 0f;
        dir.Normalize();

        GameObject proj = Instantiate(
            projectilePrefab,
            firePoint.position,
            Quaternion.LookRotation(dir)
        );

        EnemyProjectile projectile = proj.GetComponent<EnemyProjectile>();
        if (projectile != null)
        {
            projectile.Init(dir, attackPower);
        }
    }
}
