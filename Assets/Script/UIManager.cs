using System;
using UnityEngine;

/// <summary>
/// 어떤 창이 열려 있는지를 아는 곳. Tab, 상자, 창고, 비늘 선택, 닫기를 여기서 정한다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    InventoryWindow _inventory;
    ChestWindow _chest;
    StashWindow _stash;
    ScaleSelectWindow _scaleSelect;
    bool _warnedInventory;
    bool _warnedChest;
    bool _warnedStash;
    bool _warnedScaleSelect;

    /// <summary>인벤, 상자, 창고, 비늘 선택 중 하나라도 켜져 있으면 true.</summary>
    public bool IsAnyWindowOpen =>
        (_inventory != null && _inventory.IsOpen) ||
        (_chest != null && _chest.IsOpen) ||
        (_stash != null && _stash.IsOpen) ||
        (_scaleSelect != null && _scaleSelect.IsOpen);

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

    /// <summary>인벤 창을 등록한다. 이미 다른 창이 있으면 유지한다.</summary>
    public void RegisterInventory(InventoryWindow window)
    {
        RegisterWindow(ref _inventory, ref _warnedInventory, window, "인벤");
    }

    /// <summary>인벤 창 등록을 해제한다.</summary>
    public void UnregisterInventory(InventoryWindow window)
    {
        if (_inventory == window)
            _inventory = null;
    }

    /// <summary>상자 창을 등록한다. 이미 다른 창이 있으면 유지한다.</summary>
    public void RegisterChest(ChestWindow window)
    {
        RegisterWindow(ref _chest, ref _warnedChest, window, "상자");
    }

    /// <summary>상자 창 등록을 해제한다.</summary>
    public void UnregisterChest(ChestWindow window)
    {
        if (_chest == window)
            _chest = null;
    }

    /// <summary>창고 창을 등록한다. 이미 다른 창이 있으면 유지한다.</summary>
    public void RegisterStash(StashWindow window)
    {
        RegisterWindow(ref _stash, ref _warnedStash, window, "창고");
    }

    /// <summary>창고 창 등록을 해제한다.</summary>
    public void UnregisterStash(StashWindow window)
    {
        if (_stash == window)
            _stash = null;
    }

    /// <summary>비늘 선택 창을 등록한다. 이미 다른 창이 있으면 유지한다.</summary>
    public void RegisterScaleSelect(ScaleSelectWindow window)
    {
        RegisterWindow(ref _scaleSelect, ref _warnedScaleSelect, window, "비늘 선택");
    }

    /// <summary>비늘 선택 창 등록을 해제한다.</summary>
    public void UnregisterScaleSelect(ScaleSelectWindow window)
    {
        if (_scaleSelect == window)
            _scaleSelect = null;
    }

    /// <summary>열려 있으면 모두 닫고, 아니면 인벤만 연다.</summary>
    public void Toggle()
    {
        if (IsAnyWindowOpen)
            CloseAll();
        else if (_inventory != null)
            _inventory.Show();
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
        if (_inventory != null)
            _inventory.Show();
        if (_chest != null)
            _chest.Show(chest);
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
        if (_inventory != null)
            _inventory.Show();
        if (_stash != null)
            _stash.Show();
    }

    /// <summary>열린 창을 숨기고, 커서를 한 번 되돌린 뒤, 창고가 열려 있었으면 저장한다.</summary>
    public void CloseAll()
    {
        OpenedBy = null;
        bool saveStash = _stash != null && _stash.IsOpen;
        HideWindow(_inventory);
        HideWindow(_chest);
        HideWindow(_stash);
        HideWindow(_scaleSelect);
        ItemTransfer.ReturnHeldOrDrop();
        if (saveStash && SaveManager.Instance != null)
            SaveManager.Instance.SaveGame();
    }

    /// <summary>비어 있거나 같은 창이면 등록한다. 다른 창이 살아 있으면 한 번만 경고하고 유지한다.</summary>
    static void RegisterWindow<T>(ref T current, ref bool warned, T window, string kind) where T : UnityEngine.Object
    {
        if (window == null)
            return;

        if (current == null || current == window)
        {
            current = window;
            return;
        }

        if (warned)
            return;

        warned = true;
        Debug.LogWarning($"[UIManager] {kind} 창은 이미 '{current.name}'에 등록되어 있어 '{window.name}'은 등록하지 않습니다.");
    }

    /// <summary>한 창의 Hide가 실패해도 다음 창은 닫는다.</summary>
    static void HideWindow<T>(T window) where T : MonoBehaviour
    {
        if (window == null)
            return;

        try
        {
            if (window is InventoryWindow inventory)
                inventory.Hide();
            else if (window is ChestWindow chest)
                chest.Hide();
            else if (window is StashWindow stash)
                stash.Hide();
            else if (window is ScaleSelectWindow scaleSelect)
                scaleSelect.Hide();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, window);
        }
    }
}
