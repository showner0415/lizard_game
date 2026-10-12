using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float detectRange = 10f;
    public float attackRange = 7f;
    public float moveSpeed = 3f;

    protected Transform player;
    protected Rigidbody rb;
    protected EnemyAttackBase attack;

    protected bool isAggro;

    protected virtual void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        rb = GetComponent<Rigidbody>();
        attack = GetComponent<EnemyAttackBase>();
    }

    protected virtual void FixedUpdate()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (!isAggro && distance <= detectRange)//인식 사거리안에 플레이어 있으면 어그로

        {
            Debug.Log("플레이어 발견");
            isAggro = true;
        }

        if (!isAggro)
            return;

        if (distance > attackRange)//어그로 상태일때, 공격 사거리 밖이면 이동
        {
            MoveToPlayer();
        }
        else
        {
            rb.linearVelocity = Vector3.zero;//공격 사거리 안이면 멈추고 공격
            attack.TryAttack(player);
        }
    }

    protected void MoveToPlayer()
    {
        Vector3 dir = (player.position - transform.position);
        dir.y = 0f;
        dir.Normalize();

        rb.linearVelocity = dir * moveSpeed;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    public void SetAggro(bool aggro)
    {
        isAggro = aggro;
    }
}
