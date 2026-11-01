using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 행동 잠금의 시간 있는 행동을 막대로 보여 준다. 켜고 끄는 주인은 없다.
/// 막대 오브젝트는 끄지 않는다. 안 보일 때는 알파만 0이다.
/// </summary>
public class CastingBarController : MonoBehaviour
{
    [SerializeField, Tooltip("진행도를 읽을 행동 잠금. 비우면 PlayerRoot 아래에서 찾는다.")]
    PlayerActionGate _gate;

    [Header("UI References")]
    public GameObject barContainer;
    public Image fillImage;

    CanvasGroup _group;
    bool _shown;
    bool _warnedMissingGate;

    void Awake()
    {
        if (barContainer == null)
            return;

        barContainer.SetActive(true);
        _group = barContainer.GetComponent<CanvasGroup>();
        if (_group == null)
            _group = barContainer.AddComponent<CanvasGroup>();

        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _shown = false;
    }

    void LateUpdate()
    {
        if (_gate == null)
            ResolveGate();

        bool show = _gate != null && _gate.HasTimedAction;
        if (_group != null && show != _shown)
        {
            _shown = show;
            _group.alpha = show ? 1f : 0f;
        }

        if (!show || fillImage == null || _gate == null)
            return;

        float progress = _gate.Progress;
        if (Mathf.Abs(progress - fillImage.fillAmount) > 0.001f)
            fillImage.fillAmount = progress;
    }

    void ResolveGate()
    {
        if (_gate != null)
            return;

        if (PlayerRoot.Instance == null)
            return;

        _gate = PlayerRoot.Instance.GetComponentInChildren<PlayerActionGate>(true);
        if (_gate != null || _warnedMissingGate)
            return;

        _warnedMissingGate = true;
        Debug.LogWarning("[CastingBarController] PlayerActionGate를 찾지 못했습니다.", this);
    }
}
