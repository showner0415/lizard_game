using UnityEngine;

public abstract class EnemyAttackBase : MonoBehaviour
{
    public float attackCooldown = 2f;
    protected float lastAttackTime;

    public void TryAttack(Transform target)
    {
        if (Time.time - lastAttackTime < attackCooldown)
            return;

        lastAttackTime = Time.time;
        Attack(target);
    }

    protected abstract void Attack(Transform target);
}
