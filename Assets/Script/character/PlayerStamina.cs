using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float dashCost = 30f;
    [SerializeField] private float regenPerSecond = 5f;
    [SerializeField] private float sprintDrainPerSecond = 4f;

    [Header("UI Notify Throttle")]
    [Tooltip("정규화 비율이 이 값 이상 변하면 UI 이벤트 발송")]
    [SerializeField] private float notifyRatioThreshold = 0.01f;

    private PlayerMovement movement;
    private float currentStamina;
    private bool wasSprintDraining;

    private int lastNotifiedInt = int.MinValue;
    private float lastNotifiedRatio = -1f;
    private float invMaxStamina = 1f;

    public float Current => currentStamina;
    public float Max => maxStamina;
    public float Normalized => currentStamina * invMaxStamina;
    public float DashCost => dashCost;
    public bool CanDash => currentStamina >= dashCost;

    public bool IsActivelyDraining =>
        movement != null && movement.IsUsingStamina;

    public event System.Action<float, float> OnStaminaChanged;
    public event System.Action OnStaminaConsumed;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        currentStamina = maxStamina;
        invMaxStamina = maxStamina > 0f ? 1f / maxStamina : 0f;
    }

    private void Start()
    {
        NotifyChanged(force: true);
    }

    private void Update()
    {
        // 캐시된 프로퍼티만 읽음 — GetComponent 없음
        if (movement != null)
        {
            if (movement.IsDashing)
            {
                wasSprintDraining = false;
                return;
            }

            if (movement.IsSprinting)
            {
                DrainSprint();
                return;
            }
        }

        wasSprintDraining = false;

        // 이미 풀이면 회복 연산·이벤트 전부 스킵
        if (currentStamina >= maxStamina)
            return;

        currentStamina += regenPerSecond * Time.deltaTime;
        if (currentStamina > maxStamina)
            currentStamina = maxStamina;

        NotifyChanged(force: false);
    }

    void DrainSprint()
    {
        if (currentStamina <= 0f)
        {
            wasSprintDraining = false;
            return;
        }

        currentStamina -= sprintDrainPerSecond * Time.deltaTime;
        if (currentStamina < 0f)
            currentStamina = 0f;

        NotifyChanged(force: currentStamina <= 0f);

        if (!wasSprintDraining)
        {
            wasSprintDraining = true;
            OnStaminaConsumed?.Invoke();
        }
    }

    public bool TryConsumeDash()
    {
        if (currentStamina < dashCost)
            return false;

        currentStamina -= dashCost;
        if (currentStamina < 0f)
            currentStamina = 0f;

        wasSprintDraining = false;
        NotifyChanged(force: true);
        OnStaminaConsumed?.Invoke();
        return true;
    }

    /// <summary>
    /// 정수 칸이 바뀌거나, 비율이 threshold 이상 변했거나, force일 때만 UI 이벤트.
    /// </summary>
    void NotifyChanged(bool force)
    {
        int displayed = (int)currentStamina;
        float ratio = currentStamina * invMaxStamina;

        if (!force
            && displayed == lastNotifiedInt
            && Mathf.Abs(ratio - lastNotifiedRatio) < notifyRatioThreshold)
            return;

        lastNotifiedInt = displayed;
        lastNotifiedRatio = ratio;
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }
}
