using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 올라간 슬롯만 기억하고, 무엇을 보여줄지는 매 프레임 그 슬롯에 물어본다.
/// </summary>
public sealed class ItemTooltip : MonoBehaviour
{
    public static ItemTooltip Instance { get; private set; }

    [SerializeField, Tooltip("켰다 끄는 패널. 비어 있으면 이 오브젝트 아래에 만든다.")]
    RectTransform _panel;

    [SerializeField] TMP_Text _nameText;
    [SerializeField] TMP_Text _descriptionText;
    [SerializeField] LayoutElement _layoutElement;

    [SerializeField, Min(1f), Tooltip("툴팁 가로의 하한.")]
    float _minWidth = 120f;

    [SerializeField, Min(1f), Tooltip("툴팁 가로의 상한. 넘으면 설명이 줄바꿈된다.")]
    float _maxWidth = 320f;

    [SerializeField, Tooltip("왼쪽 위 기준일 때의 화면 픽셀 오프셋. 화면 밖으로 나가 뒤집히면 부호가 바뀐다.")]
    Vector2 _cursorOffset;

    IItemTooltipSource _hover;
    ItemData _shown;
    Canvas _canvas;
    Vector2 _screenSize;

    /// <summary>이 슬롯 위로 마우스가 들어왔다.</summary>
    public static void SetHover(IItemTooltipSource source)
    {
        if (Instance == null || source == null)
            return;

        Instance._hover = source;
    }

    /// <summary>이 슬롯에서 마우스가 나갔거나 슬롯이 꺼졌다. 기억 중인 슬롯일 때만 지운다.</summary>
    public static void ClearHover(IItemTooltipSource source)
    {
        if (Instance == null || !ReferenceEquals(Instance._hover, source))
            return;

        Instance._hover = null;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureVisuals();
        Hide();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void LateUpdate()
    {
        if (_panel == null)
            return;

        if (MouseItemUI.Instance != null && MouseItemUI.Instance.HasItem())
        {
            Hide();
            return;
        }

        ItemData item = _hover != null ? _hover.TooltipItem : null;
        if (item == null)
        {
            Hide();
            return;
        }

        if (!ReferenceEquals(item, _shown))
            Refresh(item);

        FollowMouse();
    }

    void Refresh(ItemData item)
    {
        _shown = item;
        _panel.gameObject.SetActive(true);

        string itemName = item.DisplayName;
        _nameText.text = itemName;

        string description = item.Description;
        bool hasDescription = !string.IsNullOrWhiteSpace(description);
        _descriptionText.gameObject.SetActive(hasDescription);
        if (hasDescription)
            _descriptionText.text = description;

        float nameWidth = _nameText.GetPreferredValues(itemName).x;
        float descriptionWidth = hasDescription ? _descriptionText.GetPreferredValues(description).x : 0f;
        float width = Mathf.Max(nameWidth, descriptionWidth) + HorizontalPadding();
        float minWidth = _minWidth > 0f ? _minWidth : 1f;
        float maxWidth = _maxWidth >= minWidth ? _maxWidth : minWidth;
        _layoutElement.preferredWidth = Mathf.Clamp(width, minWidth, maxWidth);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
        CacheScreenSize();
    }

    void CacheScreenSize()
    {
        if (_canvas == null)
            _canvas = _panel.GetComponentInParent<Canvas>();

        float scale = _canvas != null ? _canvas.scaleFactor : 1f;
        if (scale <= 0f)
            scale = 1f;

        _screenSize = _panel.rect.size * scale;
    }

    void FollowMouse()
    {
        if (Mouse.current == null)
            return;

        if (_canvas == null)
            _canvas = _panel.GetComponentInParent<Canvas>();

        Vector2 screen = Mouse.current.position.ReadValue();
        float pivotX = screen.x + _screenSize.x > Screen.width ? 1f : 0f;
        float pivotY = screen.y - _screenSize.y < 0f ? 0f : 1f;
        _panel.pivot = new Vector2(pivotX, pivotY);

        Vector2 offset = _cursorOffset;
        if (pivotX > 0f)
            offset.x = -offset.x;
        if (pivotY < 1f)
            offset.y = -offset.y;
        screen += offset;

        if (_canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            _panel.position = screen;
            return;
        }

        Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : null;
        RectTransform parent = _panel.parent as RectTransform;
        if (parent == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, camera, out Vector2 local))
            _panel.anchoredPosition = local;
    }

    void Hide()
    {
        _shown = null;
        if (_panel != null && _panel.gameObject.activeSelf)
            _panel.gameObject.SetActive(false);
    }

    float HorizontalPadding()
    {
        VerticalLayoutGroup layout = _panel.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return 0f;

        return layout.padding.left + layout.padding.right;
    }

    void EnsureVisuals()
    {
        if (_panel != null && _nameText != null && _descriptionText != null && _layoutElement != null)
        {
            _canvas = _panel.GetComponentInParent<Canvas>();
            return;
        }

        Canvas canvas = CreateCanvas();
        _canvas = canvas;

        var root = new GameObject("ItemTooltip", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);

        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement), typeof(CanvasGroup));
        panelObject.transform.SetParent(root.transform, false);
        _panel = panelObject.GetComponent<RectTransform>();
        _panel.pivot = new Vector2(0f, 1f);
        _panel.anchorMin = new Vector2(0.5f, 0.5f);
        _panel.anchorMax = new Vector2(0.5f, 0.5f);

        Image background = panelObject.GetComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        background.raycastTarget = false;

        CanvasGroup group = panelObject.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _layoutElement = panelObject.GetComponent<LayoutElement>();

        _nameText = CreateText(panelObject.transform, "NameText", 22f, FontStyles.Bold);
        _descriptionText = CreateText(panelObject.transform, "DescriptionText", 16f, FontStyles.Normal);
    }

    Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("TooltipCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        CanvasScaler main = FindMainScaler();
        if (main != null)
        {
            scaler.uiScaleMode = main.uiScaleMode;
            scaler.referenceResolution = main.referenceResolution;
            scaler.screenMatchMode = main.screenMatchMode;
            scaler.matchWidthOrHeight = main.matchWidthOrHeight;
            scaler.referencePixelsPerUnit = main.referencePixelsPerUnit;
        }

        return canvas;
    }

    CanvasScaler FindMainScaler()
    {
        CanvasScaler[] scalers = GetComponentsInChildren<CanvasScaler>(true);
        for (int i = 0; i < scalers.Length; i++)
        {
            if (scalers[i].GetComponent<Canvas>() != null && scalers[i].gameObject.name != "TooltipCanvas")
                return scalers[i];
        }

        return null;
    }

    static TMP_Text CreateText(Transform parent, string objectName, float fontSize, FontStyles style)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;
        return text;
    }
}
