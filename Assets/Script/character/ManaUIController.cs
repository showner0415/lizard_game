using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마나 바(Fill Image) + 텍스트("현재/최대") 표시.
/// 활성화될 때마다 현재 값을 직접 읽어 즉시 갱신하므로, PlayerMana.Awake와의 실행 순서에 의존하지 않는다.
/// </summary>
public class ManaUIController : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PlayerMana mana;

    [Header("UI")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI manaText;

    // OnEnable에서 구독한 바로 그 인스턴스 (mana 필드가 바뀌어도 정확히 해제하기 위함)
    private PlayerMana subscribedMana;

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    /// <summary>
    /// 표시 대상을 교체한다 (리스폰 등으로 플레이어 인스턴스가 바뀔 때). 이전 대상은 구독 해제되고 새 값으로 즉시 갱신된다.
    /// </summary>
    public void SetTarget(PlayerMana newTarget)
    {
        Unbind();
        mana = newTarget;
        if (isActiveAndEnabled)
            Bind();
    }

    private void Bind()
    {
        if (mana == null)
            mana = FindAnyObjectByType<PlayerMana>();

        if (mana == null)
        {
            Debug.LogWarning("ManaUIController: PlayerMana를 찾지 못함. Inspector에서 연결하세요.", this);
            return;
        }

        subscribedMana = mana;
        subscribedMana.OnManaChanged += HandleManaChanged;

        // 이벤트 타이밍과 무관하게 현재 값을 즉시 반영
        Refresh(subscribedMana.CurrentMana);
    }

    private void Unbind()
    {
        if (subscribedMana != null)
            subscribedMana.OnManaChanged -= HandleManaChanged;

        subscribedMana = null;
    }

    private void HandleManaChanged(float current)
    {
        Refresh(current);
    }

    private void Refresh(float current)
    {
        if (subscribedMana == null)
            return;

        float max = subscribedMana.MaxMana;
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (fillImage != null)
            fillImage.fillAmount = ratio;

        if (manaText != null)
            manaText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
    }
}
