using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private int maxHP = 50;
    //[SerializeField] private int attackPower = 10;

    [Header("UI")]
    [SerializeField] private PlayerHPBarUI hpBarUI;

    [Header("Mana")]
    [Tooltip("사망 시 HP와 함께 리셋할 마나. 비우면 같은 오브젝트에서 찾음")]
    [SerializeField] private PlayerMana mana;

    [Header("Death")]
    [SerializeField] private string homeSceneName = "Home";

    private int currentHP;
    private bool isDead = false;

    private void Awake()
    {
        currentHP = maxHP;

        if (hpBarUI != null)
            hpBarUI.Init(maxHP);

        if (mana == null)
            mana = GetComponent<PlayerMana>();
    }

    /// <summary>마법 피해. 반올림한 뒤 기존 피격과 같은 사망 경로를 탄다.</summary>
    public void TakeDamage(float damage)
    {
        int amount = Mathf.RoundToInt(damage);
        if (amount <= 0)
            return;

        TakeDamage(amount);
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
            return;

        currentHP -= damage;
        currentHP = Mathf.Max(currentHP, 0);

        if (hpBarUI != null)
            hpBarUI.SetHP(currentHP);

        Debug.Log($"플레이어 피격! HP: {currentHP}/{maxHP}");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    /// <summary>HP를 회복한다. 최대치를 넘기지 않는다.</summary>
    public void Heal(int amount)
    {
        if (isDead || amount <= 0)
            return;

        currentHP = Mathf.Min(currentHP + amount, maxHP);

        if (hpBarUI != null)
            hpBarUI.SetHP(currentHP);
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Debug.Log("플레이어 사망 → 던전 인벤 소실 후 Home 이동");

        currentHP = maxHP;
        if (hpBarUI != null)
            hpBarUI.SetHP(currentHP);

        if (mana != null)
            mana.ResetMana();

        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveOnDeath();

        if (PlayerRoot.Instance != null)
            PlayerRoot.Instance.MoveToScene(homeSceneName);

        Revive();
    }

    /// <summary>사망 처리 뒤에 다시 맞을 수 있게 한다. 플레이어는 씬이 바뀌어도 남아 있다.</summary>
    public void Revive()
    {
        isDead = false;
        currentHP = maxHP;
        if (hpBarUI != null)
            hpBarUI.SetHP(currentHP);
    }
}