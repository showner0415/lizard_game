using UnityEngine;

public class MagicProjectile : MonoBehaviour
{
    protected ProjectileMagicData data;
    protected Rigidbody rb;
    protected Vector3 direction;

    

    //????? ???? ????? ?? ??? ?????? ?????? ????? ?????? ?????? ?????? ???? ???
    public void Initialize(ProjectileMagicData magicData, Vector3 dir) 
    {
        data = magicData;
        direction = dir.normalized;

        rb = GetComponent<Rigidbody>();

        Destroy(gameObject, data.lifeTime);
    }
    protected virtual void FixedUpdate()
    {
        Move();
    }

    protected virtual void Move()
    {
        switch (data.moveType)
        {
            case MagicMoveType.Straight:
                rb.linearVelocity = direction * data.speed;
                break;

            case MagicMoveType.Parabola:
                rb.linearVelocity = direction * data.speed + Vector3.up * 5f;
                break;

            case MagicMoveType.Homing:
                // ????? ???
                break;
        }
    }


    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyStats stats = other.GetComponent<EnemyStats>();
            if (stats != null)
            {
                stats.TakeDamage(data.damage);
            }

            Destroy(gameObject);
        }
    }
}
