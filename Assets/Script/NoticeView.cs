using TMPro;
using UnityEngine;

/// <summary>알림 한 줄을 잠시 보여 주고 페이드한다. 패널은 끄지 않고 알파만 바꾼다.</summary>
public sealed class NoticeView : MonoBehaviour
{
    [SerializeField, Tooltip("알림 문구.")]
    TMP_Text _text;

    [SerializeField, Tooltip("보이기와 페이드에 쓴다. 비우면 이 오브젝트에서 찾거나 만든다.")]
    CanvasGroup _group;

    [SerializeField, Min(0f), Tooltip("완전히 보이는 시간(초, unscaled).")]
    float _holdSeconds = 1.5f;

    [SerializeField, Min(0.01f), Tooltip("사라진 뒤 페이드 시간(초, unscaled).")]
    float _fadeSeconds = 0.35f;

    [SerializeField, Tooltip("정보 알림 색.")]
    Color _infoColor = Color.white;

    [SerializeField, Tooltip("주의 알림 색.")]
    Color _warningColor = new Color(1f, 0.82f, 0.35f, 1f);

    float _shownAt;

    void Awake()
    {
        if (_text != null)
            _text.raycastTarget = false;

        if (_group == null)
            _group = GetComponent<CanvasGroup>();

        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();

        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        enabled = false;
        GameNotice.Posted += Handle;
    }

    void OnDestroy()
    {
        GameNotice.Posted -= Handle;
    }

    void Update()
    {
        float elapsed = Time.unscaledTime - _shownAt;
        if (elapsed <= _holdSeconds)
            return;

        float fade = _fadeSeconds > 0f ? _fadeSeconds : 0.01f;
        float alpha = 1f - (elapsed - _holdSeconds) / fade;
        if (alpha <= 0f)
        {
            if (_group != null)
                _group.alpha = 0f;
            enabled = false;
            return;
        }

        if (_group != null)
            _group.alpha = alpha;
    }

    void Handle(string message, GameNotice.NoticeLevel level)
    {
        if (_text != null && _group != null && _group.alpha > 0f && _text.text == message)
        {
            _shownAt = Time.unscaledTime;
            enabled = true;
            return;
        }

        if (_text != null)
        {
            _text.text = message;
            _text.color = level == GameNotice.NoticeLevel.Info ? _infoColor : _warningColor;
        }

        if (_group != null)
            _group.alpha = 1f;

        _shownAt = Time.unscaledTime;
        enabled = true;
    }
}
