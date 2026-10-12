using UnityEngine;

public class EnemyStats : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    float currentHealth;
    public GameObject mobChest;
    public float AdjustY = 1;

    bool isDead;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        
        if (isDead)
            return;

        currentHealth -= damage;
        Debug.Log($"몹 피격! HP: {currentHealth}/{maxHealth}");

        GetComponent<EnemyController>().SetAggro(true);

        if (currentHealth <= 0f)
        {
            Vector3 mobPos = transform.position;
            mobPos.y -= AdjustY;

            Instantiate(mobChest, mobPos, transform.rotation);

            
            Die();
            
        }
    }

    void Die()
    {
        isDead = true;

        // 이동 / 공격 중지
        GetComponent<Rigidbody>()?.Sleep();
        EnemyController controller = GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        EnemyAttackBase attack = GetComponent<EnemyAttackBase>();
        if (attack != null)
        {
            attack.enabled = false;
        }

        // 임시: 바로 삭제
        Destroy(gameObject);
    }
}
