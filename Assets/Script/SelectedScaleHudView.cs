using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>지금 고른 비늘. 선택이나 인벤토리가 바뀔 때만 다시 그린다.</summary>
public sealed class SelectedScaleHudView : MonoBehaviour
{
    [SerializeField, Tooltip("선택을 읽을 장전. 비우면 PlayerRoot 아래에서 찾는다.")]
    ScaleLoader _loader;

    [SerializeField, Tooltip("선택 비늘 아이콘.")]
    Image _icon;

    [SerializeField, Tooltip("선택 비늘 이름.")]
    TMP_Text _name;

    [SerializeField, Tooltip("인벤토리 수량. 기본 비늘은 무한이다.")]
    TMP_Text _count;

    ScaleLoader _boundLoader;
    InventoryModel _inventory;
    bool _dirty = true;

    void Awake()
    {
        SetRaycast(_icon, false);
        SetRaycast(_name, false);
        SetRaycast(_count, false);
    }

    void OnEnable()
    {
        _dirty = true;
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

    void LateUpdate()
    {
        InventoryModel inventory = null;
        if (InventoryController.Instance != null)
            inventory = InventoryController.Instance.Inventory;

        if (!ReferenceEquals(inventory, _inventory))
        {
            UnbindInventory();
            _inventory = inventory;
            if (_inventory != null)
            {
                _inventory.OnSlotChanged += HandleInventory;
                _inventory.OnCapacityChanged += HandleCapacity;
            }

            _dirty = true;
        }

        if (!_dirty)
            return;

        _dirty = false;
        Refresh();
    }

    void Bind()
    {
        if (_loader == null && PlayerRoot.Instance != null)
            _loader = PlayerRoot.Instance.GetComponentInChildren<ScaleLoader>(true);

        if (_loader == _boundLoader)
            return;

        UnbindLoader();
        _boundLoader = _loader;
        if (_boundLoader != null)
            _boundLoader.SelectionChanged += HandleSelection;

        _dirty = true;
    }

    void Unbind()
    {
        UnbindLoader();
        UnbindInventory();
    }

    void UnbindLoader()
    {
        if (_boundLoader != null)
            _boundLoader.SelectionChanged -= HandleSelection;

        _boundLoader = null;
    }

    void UnbindInventory()
    {
        if (_inventory == null)
            return;

        _inventory.OnSlotChanged -= HandleInventory;
        _inventory.OnCapacityChanged -= HandleCapacity;
        _inventory = null;
    }

    void HandleSelection()
    {
        _dirty = true;
    }

    void HandleInventory(int index, ItemData item, int amount)
    {
        _dirty = true;
    }

    void HandleCapacity(int capacity)
    {
        _dirty = true;
    }

    void Refresh()
    {
        ScaleLoader loader = _boundLoader;
        ScaleData scale = loader != null ? loader.SelectedScale : null;
        if (_icon != null)
        {
            _icon.sprite = scale != null ? scale.Icon : null;
            _icon.enabled = _icon.sprite != null;
        }

        if (_name != null)
            _name.text = scale != null ? scale.DisplayName : string.Empty;

        if (_count == null)
            return;

        if (loader == null || loader.SelectedItem == null)
        {
            _count.text = "∞";
            return;
        }

        int owned = 0;
        if (_inventory != null)
            owned = _inventory.CountOf(loader.SelectedItem);

        _count.SetText("x{0}", owned);
    }

    static void SetRaycast(Graphic graphic, bool enabled)
    {
        if (graphic != null)
            graphic.raycastTarget = enabled;
    }
}
