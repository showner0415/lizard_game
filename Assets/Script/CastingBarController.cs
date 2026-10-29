using UnityEngine;
using UnityEngine.UI;

public class CastingBarController : MonoBehaviour
{
    public static CastingBarController Instance { get; private set; }

    object _owner;

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

        _owner = null;
        if (barContainer != null)
            barContainer.SetActive(false);
    }

    /// <summary>바를 켠다. 겹침은 행동 잠금이 막으므로 마지막 호출이 주인이 된다.</summary>
    public void Show(object owner)
    {
        if (owner == null || barContainer == null || fillImage == null)
            return;

        _owner = owner;
        barContainer.SetActive(true);
        fillImage.fillAmount = 0f;
    }

    /// <summary>이 주인의 바만 채운다.</summary>
    public void UpdateFill(object owner, float currentHeldTime, float requiredCastingTime)
    {
        if (!IsOwner(owner) || barContainer == null || !barContainer.activeSelf || fillImage == null)
            return;

        float fillRatio = requiredCastingTime > 0f ? currentHeldTime / requiredCastingTime : 1f;
        fillImage.fillAmount = Mathf.Clamp01(fillRatio);
    }

    /// <summary>이 주인이 켠 바만 끈다. 다른 주인의 바는 그대로 둔다.</summary>
    public void Hide(object owner)
    {
        if (!IsOwner(owner))
            return;

        _owner = null;
        if (barContainer != null)
            barContainer.SetActive(false);
    }

    /// <summary>파괴된 Unity 오브젝트는 주인이 없는 것으로 본다.</summary>
    bool IsOwner(object owner)
    {
        if (_owner is Object unityOwner && unityOwner == null)
            _owner = null;

        return owner != null && ReferenceEquals(_owner, owner);
    }

    /// <summary>이 주인이 켠 바를 취소한다.</summary>
    public void Cancel(object owner)
    {
        Hide(owner);
    }
}