using System;
using UnityEngine;

/// <summary>
/// 자연 회복이 없는 마나 자원.
/// 마나 감소는 <see cref="TrySpendMana"/>로만, 회복은 <see cref="ResetMana"/>(사망/리셋)로만 일어난다.
/// 결제 시점: <see cref="TrySpendMana"/>가 true를 반환한 순간 확정되며 환불 경로는 없다.
/// </summary>
public class PlayerMana : MonoBehaviour, IHomeRestorable
{
    [Header("Mana")]
    [Tooltip("최대 마나. 0보다 커야 한다.")]
    [SerializeField] private float maxMana = 100f;

    private float currentMana;

    /// <summary>현재 마나 (읽기 전용).</summary>
    public float CurrentMana => currentMana;

    /// <summary>최대 마나 (읽기 전용).</summary>
    public float MaxMana => maxMana;

    /// <summary>마나가 바뀔 때 현재 마나 값과 함께 발생한다.</summary>
    public event Action<float> OnManaChanged;

    private void Awake()
    {
        ValidateMaxMana();
        currentMana = maxMana;
        RaiseManaChanged();
    }

    private void OnValidate()
    {
        ValidateMaxMana();
    }

    /// <summary>
    /// 현재 마나로 <paramref name="amount"/>를 지불할 수 있는지 확인만 한다. 마나는 바뀌지 않는다.
    /// </summary>
    public bool CanAfford(float amount)
    {
        return amount >= 0f && currentMana >= amount;
    }

    /// <summary>
    /// 마나를 <paramref name="amount"/>만큼 소모한다. 음수는 거부하고, 부족하면 아무것도 바꾸지 않는다.
    /// true를 반환하면 결제가 확정된다(환불 없음).
    /// </summary>
    public bool TrySpendMana(float amount)
    {
        if (amount < 0f)
        {
            Debug.LogError($"{nameof(TrySpendMana)} received negative amount ({amount}); rejected.", this);
            return false;
        }
        if (currentMana < amount) return false;

        currentMana -= amount;
        if (amount > 0f) RaiseManaChanged(); // 0 비용 시전은 이벤트를 발생시키지 않음
        return true;
    }

    /// <summary>마나를 최대치로 되돌린다. 사망/리스폰 등 리셋 시스템에서 호출한다.</summary>
    public void ResetMana()
    {
        currentMana = maxMana;
        RaiseManaChanged();
    }

    /// <inheritdoc />
    public void RestoreForHome()
    {
        ResetMana();
    }

    private void ValidateMaxMana()
    {
        if (maxMana > 0f && !float.IsInfinity(maxMana))
            return;

        Debug.LogError($"[PlayerMana] maxMana must be > 0 (was {maxMana}); clamped to 1.", this);
        maxMana = 1f;
    }

    /// <summary>구독자 하나가 예외를 던져도 나머지 구독자와 호출한 메서드가 중단되지 않게 개별 호출한다.</summary>
    private void RaiseManaChanged()
    {
        Action<float> handlers = OnManaChanged;
        if (handlers == null)
            return;

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<float>)handler).Invoke(currentMana);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }
    }
}
