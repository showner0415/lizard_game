using UnityEngine;

/// <summary>
/// 어떤 창이 열려 있는지를 아는 곳. Tab, 상자, 창고, 닫기를 여기서 정한다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    InventoryWindow _inventory;
    ChestWindow _chest;
    StashWindow _stash;

    /// <summary>인벤, 상자, 창고 중 하나라도 켜져 있으면 true.</summary>
    public bool IsAnyWindowOpen =>
        (_inventory != null && _inventory.IsOpen) ||
        (_chest != null && _chest.IsOpen) ||
        (_stash != null && _stash.IsOpen);

    /// <summary>상자 창이 켜져 있으면 true.</summary>
    public bool IsChestOpen => _chest != null && _chest.IsOpen;

    /// <summary>창고 창이 켜져 있으면 true.</summary>
    public bool IsStashOpen => _stash != null && _stash.IsOpen;

    /// <summary>이 창을 연 상호작용 대상. Tab으로 연 인벤이면 null.</summary>
    public IInteractable OpenedBy { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>인벤 창을 등록한다.</summary>
    public void RegisterInventory(InventoryWindow window) => _inventory = window;

    /// <summary>인벤 창 등록을 해제한다.</summary>
    public void UnregisterInventory(InventoryWindow window)
    {
        if (_inventory == window)
            _inventory = null;
    }

    /// <summary>상자 창을 등록한다.</summary>
    public void RegisterChest(ChestWindow window) => _chest = window;

    /// <summary>상자 창 등록을 해제한다.</summary>
    public void UnregisterChest(ChestWindow window)
    {
        if (_chest == window)
            _chest = null;
    }

    /// <summary>창고 창을 등록한다.</summary>
    public void RegisterStash(StashWindow window) => _stash = window;

    /// <summary>창고 창 등록을 해제한다.</summary>
    public void UnregisterStash(StashWindow window)
    {
        if (_stash == window)
            _stash = null;
    }

    /// <summary>열려 있으면 모두 닫고, 아니면 인벤만 연다.</summary>
    public void Toggle()
    {
        Debug.Log($"[Tab] Toggle - 등록된 인벤: {_inventory != null}, 열림: {IsAnyWindowOpen}");
        if (IsAnyWindowOpen)
            CloseAll();
        else
            _inventory?.Show();
    }

    /// <summary>상자가 열려 있으면 모두 닫고, 아니면 인벤과 그 상자를 연다.</summary>
    public void OpenChest(SearchableObject chest)
    {
        if (chest == null)
            return;

        if (IsChestOpen)
        {
            CloseAll();
            return;
        }

        OpenedBy = chest;
        _inventory?.Show();
        _chest?.Show(chest);
    }

    /// <summary>창고가 열려 있으면 모두 닫고, 아니면 인벤과 창고를 연다.</summary>
    public void OpenStash(IInteractable stash)
    {
        if (stash == null)
            return;

        if (IsStashOpen)
        {
            CloseAll();
            return;
        }

        OpenedBy = stash;
        _inventory?.Show();
        _stash?.Show();
    }

    /// <summary>열린 창을 숨기고, 커서를 한 번 되돌린 뒤, 창고가 열려 있었으면 저장한다.</summary>
    public void CloseAll()
    {
        OpenedBy = null;
        bool saveStash = _stash != null && _stash.IsOpen;
        _inventory?.Hide();
        _chest?.Hide();
        _stash?.Hide();
        ItemTransfer.ReturnHeldOrDrop();
        if (saveStash && SaveManager.Instance != null)
            SaveManager.Instance.SaveGame();
    }
}
