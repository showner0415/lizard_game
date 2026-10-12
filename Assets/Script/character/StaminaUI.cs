using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이벤트 → dirty 플래그 → LateUpdate에서 UI 1회 반영.
/// 페이드는 코루틴 1개만 유지 (Stop/Start 남발 없음, WaitForSeconds 캐싱).
/// </summary>
public class StaminaUI : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PlayerStamina stamina;

    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image fillImage;
    [SerializeField] private Slider slider;

    [Header("Visibility")]
    [SerializeField] private float hideDelay = 2f;
    [SerializeField] private float fadeDuration = 0.75f;

    private Coroutine visibilityRoutine;
    private WaitForSeconds cachedHideDelay;

    private float pendingCurrent;
    private float pendingMax = 1f;
    private bool fillDirty;
    private bool resetHideTimer;
    private bool hasFillImage;
    private bool hasSlider;
    private bool hasCanvasGroup;

    private void Awake()
    {
        hasFillImage = fillImage != null;
        hasSlider = slider != null;
        hasCanvasGroup = canvasGroup != null;

        CacheHideDelay();

        if (hasCanvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnValidate()
    {
        CacheHideDelay();
    }

    void CacheHideDelay()
    {
        cachedHideDelay = new WaitForSeconds(hideDelay);
    }

    private void OnEnable()
    {
        if (stamina == null)
            stamina = FindAnyObjectByType<PlayerStamina>();

        if (stamina == null)
        {
            Debug.LogWarning("StaminaUI: PlayerStamina를 찾지 못함. Inspector에서 연결하세요.");
            return;
        }

        stamina.OnStaminaChanged += HandleStaminaChanged;
        stamina.OnStaminaConsumed += HandleStaminaConsumed;

        pendingCurrent = stamina.Current;
        pendingMax = stamina.Max;
        fillDirty = true;
    }

    private void OnDisable()
    {
        if (stamina != null)
        {
            stamina.OnStaminaChanged -= HandleStaminaChanged;
            stamina.OnStaminaConsumed -= HandleStaminaConsumed;
        }

        if (visibilityRoutine != null)
        {
            StopCoroutine(visibilityRoutine);
            visibilityRoutine = null;
        }

        resetHideTimer = false;
        fillDirty = false;
    }

    private void LateUpdate()
    {
        if (!fillDirty)
            return;

        fillDirty = false;
        ApplyFill(pendingCurrent, pendingMax);
    }

    void HandleStaminaChanged(float current, float max)
    {
        pendingCurrent = current;
        pendingMax = max;
        fillDirty = true;
    }

    void HandleStaminaConsumed()
    {
        if (!hasCanvasGroup)
            return;

        canvasGroup.alpha = 1f;
        resetHideTimer = true;

        // 이미 돌아가는 코루틴이 있으면 StartCoroutine 재할당 없음
        if (visibilityRoutine == null)
            visibilityRoutine = StartCoroutine(HideAfterDelayRoutine());
    }

    void ApplyFill(float current, float max)
    {
        float ratio = max > 0f ? current / max : 0f;
        if (ratio < 0f) ratio = 0f;
        else if (ratio > 1f) ratio = 1f;

        if (hasFillImage)
            fillImage.fillAmount = ratio;

        if (hasSlider)
            slider.value = ratio;
    }

    IEnumerator HideAfterDelayRoutine()
    {
        while (true)
        {
            resetHideTimer = false;

            while (stamina != null && stamina.IsActivelyDraining)
                yield return null;

            if (resetHideTimer)
                continue;

            yield return cachedHideDelay;

            if (resetHideTimer)
                continue;

            float startAlpha = canvasGroup.alpha;
            float invDuration = fadeDuration > 0f ? 1f / fadeDuration : 1f;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                if (resetHideTimer || (stamina != null && stamina.IsActivelyDraining))
                {
                    canvasGroup.alpha = 1f;
                    break;
                }

                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed * invDuration);
                yield return null;
            }

            if (resetHideTimer || (stamina != null && stamina.IsActivelyDraining))
            {
                canvasGroup.alpha = 1f;
                continue;
            }

            canvasGroup.alpha = 0f;
            visibilityRoutine = null;
            yield break;
        }
    }
}
