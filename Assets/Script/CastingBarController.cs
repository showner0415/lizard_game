using UnityEngine;
using UnityEngine.UI;

public class CastingBarController : MonoBehaviour
{
    public static CastingBarController Instance { get; private set; }

    [Header("UI References")]
    public GameObject barContainer; // CastingBar_BG ������Ʈ�� ���� (��ü UI On/Off��)
    public Image fillImage;         // CastingBar_Fill ������Ʈ�� ����

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        // ������ ���� UI �����
        Hide();
    }

    // ĳ���� ���� �� UI ǥ��
    public void Show()
    {
        barContainer.SetActive(true);
        fillImage.fillAmount = 0f;
    }

    // ĳ���� �� ������ ������Ʈ
    public void UpdateFill(float currentHeldTime, float requiredCastingTime)
    {
        if (!barContainer.activeSelf) return;

        // 0.0 ~ 1.0 ������ ������ ��ȯ
        float fillRatio = currentHeldTime / requiredCastingTime;

        // Mathf.Clamp01�� �Ἥ 1.0(100%)�� ���� �ʵ��� ����
        fillImage.fillAmount = Mathf.Clamp01(fillRatio);
    }

    // ĳ���� ����/���� �� UI �����
    public void Hide()
    {
        barContainer.SetActive(false);
    }

    // ĳ���� ���� �� ���� (���Ѵٸ� ���� ���� ���� �߰� ����)
    public void Cancel()
    {
        // ������ ����⸸ ó��
        Hide();
    }
}