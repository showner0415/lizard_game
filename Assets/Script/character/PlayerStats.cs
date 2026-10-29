using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour, IDamageable, IHomeRestorable
{
    [Header("Stats")]
    [SerializeField] private int maxHP = 50;
    //[SerializeField] private int attackPower = 10;

    [Header("UI")]
    [SerializeField] private PlayerHPBarUI hpBarUI;

    [Header("Death")]
    [SerializeField] private string homeSceneName = "Home";

    private int currentHP;
    private bool isDead = false;

    /// <summary>체력이 바뀔 때 현재 체력과 함께 발생한다.</summary>
    public event Action<int> OnHealthChanged;

    /// <summary>이번 삶에서 이미 죽었으면 true. 거점 회복 때 풀린다.</summary>
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHP = maxHP;

        if (hpBarUI != null)
            hpBarUI.Init(maxHP);
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

        // 저장보다 먼저 비운다. 그렇지 않으면 장전 칸 수가 세이브에 남는다.
        ApplyDeathPenalties();

        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveOnDeath();

        if (PlayerRoot.Instance != null)
            PlayerRoot.Instance.MoveToScene(homeSceneName);
    }

    void ApplyDeathPenalties()
    {
        Transform root = PlayerRoot.Instance != null ? PlayerRoot.Instance.transform : transform;
        IDeathPenalty[] penalties = root.GetComponentsInChildren<IDeathPenalty>(true);
        for (int i = 0; i < penalties.Length; i++)
        {
            IDeathPenalty penalty = penalties[i];
            if (penalty is UnityEngine.Object unityObject && unityObject == null)
                continue;

            penalty.ApplyDeathPenalty();
        }
    }

    /// <inheritdoc />
    public void RestoreForHome()
    {
        isDead = false;
        currentHP = maxHP;
        if (hpBarUI != null)
            hpBarUI.SetHP(currentHP);
        OnHealthChanged?.Invoke(currentHP);
    }
}