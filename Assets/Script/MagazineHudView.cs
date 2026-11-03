using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>탄창에 들어 있는 비늘 수. 칸이 바뀔 때만 다시 그린다.</summary>
public sealed class MagazineHudView : MonoBehaviour
{
    [SerializeField, Tooltip("탄창을 가진 장전. 비우면 PlayerRoot 아래에서 찾는다.")]
    ScaleLoader _loader;

    [SerializeField, Tooltip("지금 탄창 비늘 아이콘.")]
    Image _icon;

    [SerializeField, Tooltip("들고 있는 수.")]
    TMP_Text _count;

    [SerializeField, Tooltip("장전 가능한 자란 칸 수. 0이면 숨긴다.")]
    TMP_Text _grown;

    [SerializeField, Tooltip("비늘이 있을 때 수량 색.")]
    Color _normalColor = Color.white;

    [SerializeField, Tooltip("비어 있을 때 수량 색.")]
    Color _emptyColor = new Color(1f, 1f, 1f, 0.4f);

    ScaleLoader _boundLoader;
    IScaleMagazine _boundMagazine;

    void Awake()
    {
        if (_grown == null)
            Debug.LogWarning("[MagazineHudView] 자란 칸 수 텍스트(_grown)가 연결되지 않았습니다.", this);

        SetRaycast(_icon, false);
        SetRaycast(_count, false);
        SetRaycast(_grown, false);
    }

    void OnEnable()
    {
        Bind();
    }

    void Start()
    {
        Bind();
    }

    void OnDisable()
    {
        Unbind();
    }

    void Bind()
    {
        if (_loader == null && PlayerRoot.Instance != null)
            _loader = PlayerRoot.Instance.GetComponentInChildren<ScaleLoader>(true);

        IScaleMagazine magazine = _loader != null ? _loader.Magazine : null;
        if (_loader == _boundLoader && ReferenceEquals(magazine, _boundMagazine))
        {
            Refresh();
            return;
        }

        Unbind();
        _boundLoader = _loader;
        _boundMagazine = magazine;
        if (_boundMagazine != null)
            _boundMagazine.SlotsChanged += Refresh;

        Refresh();
    }

    void Unbind()
    {
        if (_boundMagazine != null)
            _boundMagazine.SlotsChanged -= Refresh;

        _boundMagazine = null;
        _boundLoader = null;
    }

    void Refresh()
    {
        IScaleMagazine magazine = _boundMagazine;
        int grown = magazine != null ? magazine.GrownCount : 0;
        if (_grown != null)
        {
            if (grown > 0)
            {
                _grown.gameObject.SetActive(true);
                _grown.SetText("+{0}", grown);
            }
            else
            {
                _grown.gameObject.SetActive(false);
            }
        }

        int held = magazine != null ? magazine.HeldCount() : 0;
        if (held > 0 && magazine.CurrentScale != null)
        {
            if (_icon != null)
            {
                _icon.sprite = magazine.CurrentScale.Icon;
                _icon.enabled = true;
            }

            if (_count != null)
            {
                _count.SetText("x{0}", held);
                _count.color = _normalColor;
            }

            return;
        }

        if (_icon != null)
            _icon.enabled = false;

        if (_count != null)
        {
            _count.SetText("x{0}", 0);
            _count.color = _emptyColor;
        }
    }

    static void SetRaycast(Graphic graphic, bool enabled)
    {
        if (graphic != null)
            graphic.raycastTarget = enabled;
    }
}
