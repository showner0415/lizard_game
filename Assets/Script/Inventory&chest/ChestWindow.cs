using UnityEngine;

/// <summary>
/// 던전 상자 패널을 켜고 끈다. 모델 연결은 숨기고 보일 때 직접 한다.
/// </summary>
public class ChestWindow : MonoBehaviour
{
    [SerializeField, Tooltip("켜고 끌 상자 패널. 이 스크립트와 다른 오브젝트여야 한다.")]
    private GameObject _panel;

    [SerializeField, Tooltip("상자 모델을 그릴 프레젠터. 비어 있으면 패널 아래에서 찾는다.")]
    private ContainerPresenter _presenter;

    SearchableObject _chest;

    /// <summary>패널이 켜져 있으면 true.</summary>
    public bool IsOpen => HasSeparatePanel && _panel.activeInHierarchy;

    bool HasSeparatePanel => _panel != null && _panel != gameObject;

    private void Awake()
    {
        WarnIfPanelInvalid();
        ResolvePresenter();
        UIManager.Instance?.RegisterChest(this);
    }

    private void Start()
    {
        ResolvePresenter();
        UIManager.Instance?.RegisterChest(this);
    }

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.UnregisterChest(this);
    }

    /// <summary>이 상자를 패널에 연결하고 보여 준다.</summary>
    public void Show(SearchableObject chest)
    {
        if (chest == null)
        {
            Debug.LogWarning("[ChestWindow] 열 상자가 없습니다.", this);
            return;
        }

        if (!HasSeparatePanel)
            return;

        ResolvePresenter();
        _chest = chest;
        _panel.SetActive(true);

        if (_presenter == null)
        {
            Debug.LogWarning("[ChestWindow] ContainerPresenter가 없습니다.", this);
            return;
        }

        _presenter.BindModel(chest.Model);
    }

    /// <summary>연결을 끊고 패널을 숨긴다.</summary>
    public void Hide()
    {
        _presenter?.Unbind();
        _chest = null;
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
            Debug.LogWarning("[ChestWindow] 패널이 비어 있습니다. 창 스크립트는 항상 켜진 오브젝트에 두고, 패널을 따로 연결하세요.", this);
        else if (_panel == gameObject)
            Debug.LogWarning("[ChestWindow] 패널이 자기 자신입니다. 창 스크립트는 항상 켜진 오브젝트에 두고, 패널을 따로 연결하세요.", this);
    }
}
