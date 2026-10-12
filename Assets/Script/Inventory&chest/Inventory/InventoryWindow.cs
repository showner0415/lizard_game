using UnityEngine;

/// <summary>
/// 인벤 패널을 켜고 끈다. 그리기는 패널 안의 Presenter가 한다.
/// </summary>
public class InventoryWindow : MonoBehaviour
{
    [SerializeField, Tooltip("켜고 끌 인벤 패널. 이 스크립트와 다른 오브젝트여야 한다.")]
    private GameObject _panel;

    /// <summary>패널이 켜져 있으면 true.</summary>
    public bool IsOpen => HasSeparatePanel && _panel.activeInHierarchy;

    bool HasSeparatePanel => _panel != null && _panel != gameObject;

    private void Awake()
    {
        WarnIfPanelInvalid();
        UIManager.Instance?.RegisterInventory(this);
    }

    private void Start()
    {
        UIManager.Instance?.RegisterInventory(this);
    }

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.UnregisterInventory(this);
    }

    /// <summary>인벤 패널을 보여 준다.</summary>
    public void Show()
    {
        if (!HasSeparatePanel)
            return;

        _panel.SetActive(true);
    }

    /// <summary>인벤 패널을 숨긴다. 프레젠터는 패널이 꺼지며 구독을 끊는다.</summary>
    public void Hide()
    {
        if (!HasSeparatePanel)
            return;

        _panel.SetActive(false);
    }

    /// <summary>같은 씬의 닫기 버튼이 호출한다.</summary>
    public void RequestClose()
    {
        UIManager.Instance?.CloseAll();
    }

    void WarnIfPanelInvalid()
    {
        if (_panel == null)
            Debug.LogWarning("[InventoryWindow] 패널이 비어 있습니다. 창 스크립트는 항상 켜진 오브젝트에 두고, 패널을 따로 연결하세요.", this);
        else if (_panel == gameObject)
            Debug.LogWarning("[InventoryWindow] 패널이 자기 자신입니다. 창 스크립트는 항상 켜진 오브젝트에 두고, 패널을 따로 연결하세요.", this);
    }
}
