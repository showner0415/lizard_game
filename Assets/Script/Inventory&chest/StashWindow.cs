using UnityEngine;

/// <summary>
/// 거점 창고 패널을 켜고 끈다. 모델은 <see cref="InventoryController.Stash"/>다.
/// </summary>
public class StashWindow : MonoBehaviour
{
    [SerializeField, Tooltip("켜고 끌 창고 패널. 이 스크립트와 다른 오브젝트여야 한다.")]
    private GameObject _panel;

    [SerializeField, Tooltip("창고 모델을 그릴 프레젠터. 비어 있으면 패널 아래에서 찾는다.")]
    private ContainerPresenter _presenter;

    /// <summary>패널이 켜져 있으면 true.</summary>
    public bool IsOpen => HasSeparatePanel && _panel.activeInHierarchy;

    bool HasSeparatePanel => _panel != null && _panel != gameObject;

    private void Awake()
    {
        WarnIfPanelInvalid();
        ResolvePresenter();
        UIManager.Instance?.RegisterStash(this);
    }

    private void Start()
    {
        ResolvePresenter();
        UIManager.Instance?.RegisterStash(this);
    }

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.UnregisterStash(this);
    }

    /// <summary>창고 모델을 연결하고 패널을 보여 준다.</summary>
    public void Show()
    {
        if (!HasSeparatePanel)
            return;

        ResolvePresenter();
        InventoryModel stash = InventoryController.Instance != null ? InventoryController.Instance.Stash : null;
        if (_presenter == null || stash == null)
        {
            Debug.LogWarning("[StashWindow] ContainerPresenter 또는 창고 모델이 없습니다.", this);
            return;
        }

        _panel.SetActive(true);
        _presenter.BindModel(stash);
    }

    /// <summary>연결을 끊고 패널을 숨긴다.</summary>
    public void Hide()
    {
        _presenter?.Unbind();
        if (!HasSeparatePanel)
            return;

        _panel.SetActive(false);
    }

    /// <summary>같은 씬의 닫기 버튼이 호출한다.</summary>
    public void RequestClose()
    {
        UIManager.Instance?.CloseAll();
    }

    void ResolvePresenter()
    {
        if (_presenter != null || !HasSeparatePanel)
            return;

        _presenter = _panel.GetComponent<ContainerPresenter>();
        if (_presenter == null)
            _presenter = _panel.GetComponentInChildren<ContainerPresenter>(true);
    }

    void WarnIfPanelInvalid()
    {
        if (_panel == null)
            Debug.LogWarning("[StashWindow] 패널이 비어 있습니다. 창 스크립트는 항상 켜진 오브젝트에 두고, 패널을 따로 연결하세요.", this);
        else if (_panel == gameObject)
            Debug.LogWarning("[StashWindow] 패널이 자기 자신입니다. 창 스크립트는 항상 켜진 오브젝트에 두고, 패널을 따로 연결하세요.", this);
    }
}
