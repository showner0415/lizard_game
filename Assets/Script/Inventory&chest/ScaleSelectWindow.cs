using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 장전할 비늘을 고르는 창. 스크립트는 항상 켜진 오브젝트에 두고, <see cref="_panel"/>만 켜고 끈다.
/// </summary>
public sealed class ScaleSelectWindow : MonoBehaviour
{
    [SerializeField, Tooltip("켜고 끌 패널. 이 스크립트와 다른 오브젝트여야 한다.")]
    GameObject _panel;

    [SerializeField, Tooltip("항목이 붙는 부모. 열 때마다 비우고 다시 채운다.")]
    Transform _content;

    [SerializeField, Tooltip("항목 프리팹. ScaleSelectEntry가 붙어 있어야 한다.")]
    ScaleSelectEntry _entryPrefab;

    [SerializeField, Tooltip("선택을 바꿀 ScaleLoader. 비우면 PlayerRoot 아래에서 찾는다.")]
    ScaleLoader _scaleLoader;

    [SerializeField, Tooltip("행동 중이면 창을 열지 않는다. 비우면 PlayerRoot 아래에서 찾는다.")]
    PlayerActionGate _actionGate;

    readonly List<ScaleSelectEntry> _entries = new List<ScaleSelectEntry>();
    readonly List<ScaleItemData> _rowItems = new List<ScaleItemData>();
    readonly List<int> _rowCounts = new List<int>();

    /// <summary>패널이 켜져 있으면 true.</summary>
    public bool IsOpen => HasSeparatePanel && _panel.activeInHierarchy;

    bool HasSeparatePanel => _panel != null && _panel != gameObject;

    void Awake()
    {
        WarnIfPanelInvalid();
        ResolveRefs();
        UIManager.Instance?.RegisterScaleSelect(this);
        Hide();
    }

    void Start()
    {
        ResolveRefs();
        UIManager.Instance?.RegisterScaleSelect(this);
    }

    void OnDestroy()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.UnregisterScaleSelect(this);
    }

    /// <summary>PlayerInput의 ScaleSelect(G) 이벤트에 연결한다.</summary>
    public void OnScaleSelect(InputAction.CallbackContext ctx)
    {
        if (!isActiveAndEnabled)
            return;

        if (!ctx.performed)
            return;

        if (IsOpen)
        {
            RequestClose();
            return;
        }

        ResolveRefs();
        if (_actionGate != null && _actionGate.IsBusy)
        {
            Debug.Log("행동 중에는 비늘을 고를 수 없습니다.");
            return;
        }

        if (UIManager.Instance != null && UIManager.Instance.IsAnyWindowOpen)
            return;

        Show();
    }

    /// <summary>목록을 다시 만들고 패널을 보여 준다.</summary>
    public void Show()
    {
        if (!HasSeparatePanel)
            return;

        ResolveRefs();
        if (_scaleLoader == null || _content == null || _entryPrefab == null)
        {
            Debug.LogWarning("[ScaleSelectWindow] ScaleLoader, Content, 항목 프리팹 중 하나가 없습니다.", this);
            return;
        }

        Rebuild();
        _panel.SetActive(true);
    }

    /// <summary>항목을 없애고 패널을 숨긴다.</summary>
    public void Hide()
    {
        ClearEntries();
        if (!HasSeparatePanel)
            return;

        _panel.SetActive(false);
    }

    /// <summary>같은 씬의 닫기 버튼이 호출한다.</summary>
    public void RequestClose()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.CloseAll();
        else
            Hide();
    }

    void Rebuild()
    {
        ClearEntries();
        _rowItems.Clear();
        _rowCounts.Clear();

        ScaleData baseScale = _scaleLoader.DefaultScale;
        if (baseScale != null)
            AddEntry(baseScale, null, 0, false, _scaleLoader.IsSelected(null));
        else
            Debug.LogWarning("[ScaleSelectWindow] 기본 비늘이 비어 있습니다.", _scaleLoader);

        CollectInventoryScales();

        ScaleItemData selected = _scaleLoader.SelectedItem;
        if (selected != null && !_rowItems.Contains(selected))
        {
            _rowItems.Add(selected);
            _rowCounts.Add(0);
        }

        for (int i = 0; i < _rowItems.Count; i++)
        {
            ScaleItemData item = _rowItems[i];
            if (item == null || item.Scale == null)
                continue;

            AddEntry(item.Scale, item, _rowCounts[i], true, _scaleLoader.IsSelected(item));
        }
    }

    void CollectInventoryScales()
    {
        InventoryModel inventory = InventoryController.Instance != null ? InventoryController.Instance.Inventory : null;
        if (inventory == null)
            return;

        for (int i = 0; i < inventory.Capacity; i++)
        {
            ItemStack stack = inventory.Peek(i);
            if (stack == null || !stack.HasItem || stack.item is not ScaleItemData item)
                continue;

            int row = _rowItems.IndexOf(item);
            if (row < 0)
            {
                _rowItems.Add(item);
                _rowCounts.Add(stack.amount);
            }
            else
            {
                _rowCounts[row] += stack.amount;
            }
        }
    }

    void AddEntry(ScaleData scale, ScaleItemData item, int count, bool showCount, bool selected)
    {
        ScaleSelectEntry entry = Instantiate(_entryPrefab, _content);
        entry.gameObject.SetActive(true);
        entry.Bind(scale, item, count, showCount, selected, () => Pick(item));
        _entries.Add(entry);
    }

    void Pick(ScaleItemData item)
    {
        if (_scaleLoader == null)
            return;

        if (!_scaleLoader.TrySelect(item))
        {
            Debug.Log("지금은 비늘을 바꿀 수 없습니다.");
            return;
        }

        RequestClose();
    }

    void ClearEntries()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i] != null)
                Destroy(_entries[i].gameObject);
        }

        _entries.Clear();
    }

    void ResolveRefs()
    {
        if (PlayerRoot.Instance == null)
            return;

        if (_scaleLoader == null)
            _scaleLoader = PlayerRoot.Instance.GetComponentInChildren<ScaleLoader>(true);

        if (_actionGate == null)
            _actionGate = PlayerRoot.Instance.GetComponentInChildren<PlayerActionGate>(true);
    }

    void WarnIfPanelInvalid()
    {
        if (_panel == null)
            Debug.LogWarning("[ScaleSelectWindow] 패널이 비어 있습니다. 창 스크립트는 항상 켜진 오브젝트에 두고, 패널을 따로 연결하세요.", this);
        else if (_panel == gameObject)
            Debug.LogWarning("[ScaleSelectWindow] 패널이 자기 자신입니다. 창 스크립트는 항상 켜진 오브젝트에 두고, 패널을 따로 연결하세요.", this);
    }
}
