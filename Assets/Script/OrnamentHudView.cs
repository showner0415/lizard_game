using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>지금 발사할 장식과 비용. 장식, 마나, 탄창, 씬이 바뀔 때만 다시 그린다.</summary>
public sealed class OrnamentHudView : MonoBehaviour
{
    [SerializeField, Tooltip("꼬리 장식. 비우면 PlayerRoot 아래에서 찾는다.")]
    PlayerOrnament _ornament;

    [SerializeField, Tooltip("마나. 비우면 PlayerRoot 아래에서 찾는다.")]
    PlayerMana _mana;

    [SerializeField, Tooltip("탄창을 빌릴 장전. 비우면 PlayerRoot 아래에서 찾는다.")]
    ScaleLoader _loader;

    [SerializeField, Tooltip("장식 아이콘. 기본 던지기면 아래 스프라이트.")]
    Image _icon;

    [SerializeField, Tooltip("장식 이름.")]
    TMP_Text _name;

    [SerializeField, Tooltip("마나와 비늘 비용.")]
    TMP_Text _cost;

    [SerializeField, Tooltip("장식이 없을 때 아이콘.")]
    Sprite _basicThrowIcon;

    [SerializeField, Tooltip("장식이 없을 때 이름.")]
    string _basicThrowName = "기본 던지기";

    [SerializeField, Tooltip("비용을 낼 수 있을 때 색.")]
    Color _okColor = Color.white;

    [SerializeField, Tooltip("마나 또는 비늘이 부족할 때 색.")]
    Color _lackColor = new Color(1f, 0.45f, 0.45f, 1f);

    PlayerOrnament _boundOrnament;
    PlayerMana _boundMana;
    IScaleMagazine _boundMagazine;
    bool _sceneHooked;

    void Awake()
    {
        SetRaycast(_icon, false);
        SetRaycast(_name, false);
        SetRaycast(_cost, false);
    }

    void OnEnable()
    {
        if (!_sceneHooked)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _sceneHooked = true;
        }

        Bind();
    }

    void Start()
    {
        Bind();
    }

    void OnDisable()
    {
        if (_sceneHooked)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _sceneHooked = false;
        }

        Unbind();
    }

    void Bind()
    {
        if (PlayerRoot.Instance != null)
        {
            if (_ornament == null)
                _ornament = PlayerRoot.Instance.GetComponentInChildren<PlayerOrnament>(true);
            if (_mana == null)
                _mana = PlayerRoot.Instance.GetComponentInChildren<PlayerMana>(true);
            if (_loader == null)
                _loader = PlayerRoot.Instance.GetComponentInChildren<ScaleLoader>(true);
        }

        if (_ornament != _boundOrnament)
        {
            if (_boundOrnament != null)
                _boundOrnament.OrnamentChanged -= Refresh;

            _boundOrnament = _ornament;
            if (_boundOrnament != null)
                _boundOrnament.OrnamentChanged += Refresh;
        }

        if (_mana != _boundMana)
        {
            if (_boundMana != null)
                _boundMana.OnManaChanged -= HandleMana;

            _boundMana = _mana;
            if (_boundMana != null)
                _boundMana.OnManaChanged += HandleMana;
        }

        RebindMagazine();
        Refresh();
    }

    void Unbind()
    {
        if (_boundOrnament != null)
            _boundOrnament.OrnamentChanged -= Refresh;
        _boundOrnament = null;

        if (_boundMana != null)
            _boundMana.OnManaChanged -= HandleMana;
        _boundMana = null;

        if (_boundMagazine != null)
            _boundMagazine.SlotsChanged -= Refresh;
        _boundMagazine = null;
    }

    void RebindMagazine()
    {
        IScaleMagazine magazine = _loader != null ? _loader.Magazine : null;
        if (ReferenceEquals(magazine, _boundMagazine))
            return;

        if (_boundMagazine != null)
            _boundMagazine.SlotsChanged -= Refresh;

        _boundMagazine = magazine;
        if (_boundMagazine != null)
            _boundMagazine.SlotsChanged += Refresh;
    }

    void HandleMana(float mana)
    {
        Refresh();
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindMagazine();
        Refresh();
    }

    void Refresh()
    {
        RebindMagazine();
        TailOrnamentItemData ornament = _boundOrnament != null ? _boundOrnament.CurrentOrnament : null;
        MagicData magic = _boundOrnament != null ? _boundOrnament.CurrentMagic : null;

        if (_icon != null)
        {
            if (ornament != null && ornament.icon != null)
                _icon.sprite = ornament.icon;
            else
                _icon.sprite = _basicThrowIcon;

            _icon.enabled = _icon.sprite != null;
        }

        if (_name != null)
            _name.text = ornament != null ? ornament.DisplayName : _basicThrowName;

        int manaCost = magic != null ? magic.ManaCost : 0;
        int scales = magic != null ? magic.RequiredScalesToStart : 0;
        bool free = PlayerCostRules.IsFree;
        if (_cost != null)
        {
            if (free)
                _cost.text = "무료";
            else if (scales > 0)
                _cost.SetText("MP {0} · 비늘 {1}", manaCost, scales);
            else
                _cost.SetText("MP {0}", manaCost);
        }

        bool lacksMana = !free && _boundMana != null && magic != null && !_boundMana.CanAfford(manaCost);
        int held = _boundMagazine != null ? _boundMagazine.HeldCount() : 0;
        bool lacksScales = !free && scales > 0 && held < scales;
        Color color = lacksMana || lacksScales ? _lackColor : _okColor;
        if (_name != null)
            _name.color = color;
        if (_cost != null)
            _cost.color = color;
    }

    static void SetRaycast(Graphic graphic, bool enabled)
    {
        if (graphic != null)
            graphic.raycastTarget = enabled;
    }
}
