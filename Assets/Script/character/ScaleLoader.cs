using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 전용 비늘 장전. R 입력, 장전 시간, 마나 지불, 파밍 비늘의 인벤토리 차감을 맡고
/// 탄창에는 "자란 칸에 이 비늘을 넣어"라고만 요청한다.
/// <para>
/// 마나는 장전이 끝나는 순간에만 지불하고 환불하지 않는다. 끝나기 전에 취소되면 비용이 없다.
/// 탄창(<see cref="IScaleMagazine"/>)은 플레이어, 인벤토리, 마나를 모르므로 이런 규칙은 전부 여기에 둔다.
/// 선택은 비늘 데이터가 아니라 아이템으로 구분한다. 같은 데이터라도 아이템이 다르면 장전된 칸을 비우고 바꾼다.
/// 직전 선택이 없으면 기본 비늘이라 해제해도 버리고, 선택이 있으면 그 아이템으로 되돌린다. 넘치면 바닥에 떨어진다.
/// 장전 중이거나 예약된 칸이 있으면 선택을 바꾸지 않는다.
/// </para>
/// </summary>
public sealed class ScaleLoader : MonoBehaviour, IHomeRestorable, IDeathPenalty, IInstantScaleLoader, IGateInterruptible
{
    [Header("Magazine")]
    [Tooltip("탄창을 가진 ScaleSpender. 비우면 이 오브젝트와 부모에서 찾는다.")]
    [SerializeField] private ScaleSpender _scaleSpender;

    [Header("Player")]
    [Tooltip("장전 비용을 낼 마나. 비우면 같은 오브젝트에서 찾고, 없으면 무료로 장전한다.")]
    [SerializeField] private PlayerMana _playerMana;

    [Tooltip("차지, 채널링, 장전이 겹치지 않게 잠근다. 비우면 같은 오브젝트와 부모에서 찾는다.")]
    [SerializeField] private PlayerActionGate _actionGate;

    [Tooltip("죽었으면 장전을 시작하지 않는다. 비우면 같은 오브젝트와 부모에서 찾는다.")]
    [SerializeField] private PlayerStats _playerStats;

    [Header("Load")]
    [Tooltip("장전할 기본 비늘(tier 0). 아이템이 아니며 인벤토리 없이 장전된다.")]
    [SerializeField] private ScaleData _defaultScale;

    [Tooltip("장전 1회에 채우는 칸 수. 장신구/업그레이드가 올린다.")]
    [SerializeField, Min(1)] private int _loadPerAction = 1;

    [Header("Debug")]
    [Tooltip("컨텍스트 메뉴 Select Debug Item이 고르는 파밍 비늘. 플레이 모드에서만 동작한다.")]
    [SerializeField] private ScaleItemData _debugSelectItem;

    private Coroutine _loadRoutine;
    private bool _isLoading;
    private ScaleData _selectedScale;
    private ScaleItemData _selectedItem;
    private bool _selectionRestored;
    private bool _warnedMissingSpender;

    /// <summary>지금 장전 중이면 true.</summary>
    public bool IsLoading => _isLoading;

    /// <summary>장전 1회에 채우는 칸 수. 1 이상.</summary>
    public int LoadPerAction => _loadPerAction < 1 ? 1 : _loadPerAction;

    /// <summary>R로 장전할 비늘. 처음에는 기본 비늘이다.</summary>
    public ScaleData SelectedScale => _selectedScale;

    /// <summary>선택한 파밍 비늘 아이템. 기본 비늘이면 null.</summary>
    public ScaleItemData SelectedItem => _selectedItem;

    /// <summary>선택한 비늘 아이템이 바뀔 때 발생한다.</summary>
    public event System.Action SelectionChanged;

    /// <summary>목록 첫 항목으로 쓰는 기본 비늘. 지금 선택과 다를 수 있다.</summary>
    public ScaleData DefaultScale => _defaultScale;

    /// <summary>장전에 쓰는 탄창. ScaleSpender가 찾은 것을 그대로 쓴다.</summary>
    public IScaleMagazine Magazine
    {
        get
        {
            if (_scaleSpender == null)
                _scaleSpender = GetComponentInParent<ScaleSpender>();

            if (_scaleSpender == null)
            {
                if (!_warnedMissingSpender)
                {
                    _warnedMissingSpender = true;
                    Debug.LogWarning("[ScaleLoader] ScaleSpender가 없어 탄창을 찾지 못했습니다.", this);
                }

                return null;
            }

            return _scaleSpender.Magazine;
        }
    }

    private void Awake()
    {
        if (!_selectionRestored)
            SetSelection(null, false);

        if (_scaleSpender == null)
            _scaleSpender = GetComponentInParent<ScaleSpender>();

        if (_playerMana == null)
            _playerMana = GetComponent<PlayerMana>();

        if (_playerMana == null)
            Debug.LogWarning("[ScaleLoader] PlayerMana를 찾지 못함 → 마나 비용 없이 장전됩니다.", this);

        if (_actionGate == null)
            _actionGate = GetComponent<PlayerActionGate>();

        if (_actionGate == null)
            _actionGate = GetComponentInParent<PlayerActionGate>();

        if (_actionGate == null)
            Debug.LogWarning("[ScaleLoader] PlayerActionGate를 찾지 못함 → 장전이 다른 행동과 겹칠 수 있습니다.", this);

        if (_playerStats == null)
            _playerStats = GetComponent<PlayerStats>();

        if (_playerStats == null)
            _playerStats = GetComponentInParent<PlayerStats>();

        if (_defaultScale == null)
            Debug.LogWarning("[ScaleLoader] 기본 비늘 ScaleData가 비어 있어 기본 비늘을 장전할 수 없습니다.", this);

        if (_loadPerAction < 1)
        {
            Debug.LogWarning($"[ScaleLoader] _loadPerAction {_loadPerAction}는 1로 맞춥니다.", this);
            _loadPerAction = 1;
        }
    }

    private void OnDisable()
    {
        CancelLoad();
    }

    /// <inheritdoc />
    public void InterruptByGate()
    {
        CancelLoad();
    }

    private void OnValidate()
    {
        if (_loadPerAction < 1)
            _loadPerAction = 1;
    }

    /// <summary>PlayerInput의 Reload(R) 이벤트에 연결한다.</summary>
    public void OnReload(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
            return;

        TryBeginLoad();
    }

    /// <summary>
    /// 장전할 비늘을 고른다. <paramref name="item"/>이 null이면 기본 비늘이다.
    /// 장전 중이거나, 행동이 진행 중이거나, 예약된 칸이 있으면 false.
    /// 같은 아이템이면(둘 다 null인 기본 비늘 포함) 탄창을 건드리지 않고 true.
    /// 비늘 데이터가 같아도 아이템이 다르면 장전된 칸을 비우고, 이전 파밍 비늘을 되돌린 뒤 선택을 바꾼다.
    /// </summary>
    public bool TrySelect(ScaleItemData item)
    {
        if (_isLoading)
            return false;

        if (_actionGate != null && _actionGate.IsBusy)
            return false;

        if (item != null && item.Scale == null)
        {
            Debug.LogWarning($"[ScaleLoader] '{item.name}'에 ScaleData가 없어 선택할 수 없습니다.", item);
            return false;
        }

        ScaleData nextScale = item == null ? _defaultScale : item.Scale;
        if (nextScale == null)
        {
            Debug.LogWarning("[ScaleLoader] 선택할 비늘이 없습니다.", this);
            return false;
        }

        if (item == _selectedItem)
            return true;

        IScaleMagazine magazine = Magazine;
        if (magazine == null)
            return false;

        if (magazine.ReservedCount > 0)
            return false;

        ScaleItemData previousItem = _selectedItem;
        int unloaded = magazine.UnloadAll(out ScaleData unloadedScale);
        ReturnUnloaded(previousItem, unloadedScale, unloaded);

        SetSelection(item, false);
        return true;
    }

    /// <summary>선택한 아이템을 바꾼다. 기본 비늘은 null. 값이 같으면 알리지 않고, 복원은 항상 알린다.</summary>
    void SetSelection(ScaleItemData item, bool forceRaise)
    {
        ScaleData scale = item != null ? item.Scale : _defaultScale;
        bool changed = item != _selectedItem || scale != _selectedScale;
        _selectedItem = item;
        _selectedScale = scale;
        if (!changed && !forceRaise)
            return;

        Action handlers = SelectionChanged;
        if (handlers == null)
            return;

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler).Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    /// <summary>그 아이템이 지금 선택된 파밍 비늘이면 true. null이면 기본 비늘이 선택된 상태인지.</summary>
    public bool IsSelected(ScaleItemData item)
    {
        if (item == null)
            return _selectedItem == null;

        return _selectedItem == item;
    }

    /// <summary>조건이 맞으면 장전을 시작한다. 이 시점에는 마나와 인벤토리를 확인만 하고 빼지 않는다.</summary>
    public bool TryBeginLoad()
    {
        // PlayerInput은 꺼진 컴포넌트의 이벤트도 호출한다.
        if (!isActiveAndEnabled)
            return false;

        if (_playerStats != null && _playerStats.IsDead)
            return false;

        if (UIManager.Instance != null && UIManager.Instance.IsAnyWindowOpen)
            return false;

        if (_isLoading)
            return false;

        ScaleData scale = SelectedScale;
        if (scale == null)
        {
            Debug.LogWarning("[ScaleLoader] 장전할 비늘이 정해지지 않았습니다.", this);
            return false;
        }

        IScaleMagazine magazine = Magazine;
        if (magazine == null)
            return false;

        if (magazine.CurrentScale != null && magazine.CurrentScale != scale)
        {
            Debug.LogWarning($"[ScaleLoader] 탄창의 비늘 '{magazine.CurrentScale.name}'과 선택 '{scale.name}'이 다릅니다.", this);
            return false;
        }

        if (magazine.GrownCount < 1)
        {
            GameNotice.Show(NoticeText.NoGrownSlot);
            return false;
        }

        if (IsFarmed && CountSelectedItem() < 1)
        {
            GameNotice.Show(NoticeText.SelectedScaleMissing);
            return false;
        }

        int count = PlannedCount(magazine, scale, includeMana: false);
        if (count < 1)
            return false;

        int cost = scale.LoadManaCost * count;
        if (!PlayerCostRules.IsFree && _playerMana != null && !_playerMana.CanAfford(cost))
        {
            GameNotice.Show(NoticeText.NotEnoughMana);
            return false;
        }

        if (!TryEnterLoading(scale.LoadTime))
            return false;

        _isLoading = true;
        _loadRoutine = StartCoroutine(LoadRoutine(scale, _selectedItem, Time.time));
        return true;
    }

    /// <summary>장전 중이면 비용 없이 취소한다. 장전 중이 아니면 아무것도 하지 않는다.</summary>
    public void CancelLoad()
    {
        if (!_isLoading)
            return;

        EndLoadState();
        ExitLoading();
    }

    /// <inheritdoc />
    public void RestoreForHome()
    {
        CancelLoad();
    }

    /// <inheritdoc />
    public void ApplyDeathPenalty()
    {
        CancelLoad();
    }

    /// <summary>선택한 비늘 아이템 번호와 장전된 칸 수를 <paramref name="save"/>에 적는다.</summary>
    public void CaptureState(SaveData save)
    {
        if (save == null)
            return;

        save.loadedScaleItemNum = _selectedItem != null ? _selectedItem.itemNum : -1;
        IScaleMagazine magazine = Magazine;
        int loaded = magazine != null ? magazine.LoadedCount + magazine.ReservedCount : 0;
        save.loadedScaleCount = loaded < 0 ? 0 : loaded;
    }

    /// <summary>
    /// 선택을 되돌리고, 저장돼 있던 칸 수만큼 <see cref="IScaleMagazine.LoadGrown"/>을 부른다.
    /// 인벤토리에서는 빼지 않는다. 그 수량은 저장 전에 이미 빠져 있다.
    /// </summary>
    public void RestoreState(SaveData save, ItemDatabase database)
    {
        if (save == null)
            return;

        _selectionRestored = true;
        ScaleItemData item = null;
        int count = save.loadedScaleCount < 0 ? 0 : save.loadedScaleCount;

        if (save.loadedScaleItemNum >= 0)
        {
            ItemData found = database != null ? database.Get(save.loadedScaleItemNum) : null;
            if (found is ScaleItemData scaleItem && scaleItem.Scale != null)
                item = scaleItem;
            else
            {
                Debug.LogWarning($"[ScaleLoader] itemNum {save.loadedScaleItemNum} 비늘을 찾지 못했습니다. 장전 칸은 복원하지 않습니다.", this);
                count = 0;
            }
        }

        SetSelection(item, true);
        if (count < 1 || _selectedScale == null)
            return;

        IScaleMagazine magazine = Magazine;
        if (magazine == null)
        {
            Debug.LogWarning("[ScaleLoader] 탄창이 없어 장전 칸을 복원하지 못했습니다.", this);
            return;
        }

        int loaded = magazine.LoadGrown(_selectedScale, count);
        if (loaded < count)
        {
            if (item != null)
            {
                ReturnToInventoryOrDrop(item, count - loaded);
                Debug.LogWarning($"[ScaleLoader] 저장은 {count}칸인데 {loaded}칸만 복원돼 나머지 {count - loaded}개를 인벤토리로 돌려줍니다.", this);
            }
            else
            {
                Debug.LogWarning($"[ScaleLoader] 저장은 {count}칸인데 {loaded}칸만 복원됐습니다.", this);
            }
        }
    }

    /// <summary>플레이 모드에서 기본 비늘을 선택한다.</summary>
    [ContextMenu("Select Base Scale")]
    void SelectBaseScale()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ScaleLoader] 플레이 모드에서만 선택을 바꿉니다.");
            return;
        }

        TrySelect(null);
    }

    /// <summary>플레이 모드에서 <see cref="_debugSelectItem"/>을 선택한다.</summary>
    [ContextMenu("Select Debug Item")]
    void SelectDebugItem()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ScaleLoader] 플레이 모드에서만 선택을 바꿉니다.");
            return;
        }

        if (_debugSelectItem == null)
        {
            Debug.LogWarning("[ScaleLoader] Debug Select Item이 비어 있습니다.", this);
            return;
        }

        TrySelect(_debugSelectItem);
    }

    private IEnumerator LoadRoutine(ScaleData scale, ScaleItemData item, float startTime)
    {
        float loadTime = scale.LoadTime;
        float completeAtTime = startTime + loadTime;

        // 0초 장전도 StartCoroutine 밖에서 끝나게 최소 한 프레임은 기다린다.
        do
        {
            yield return null;
        }
        while (Time.time < completeAtTime);

        _loadRoutine = null;
        CompleteLoad(scale, item);
    }

    /// <summary>
    /// 끝나는 순간 칸 수와 마나를 다시 계산한다. 마나를 낸 뒤 <see cref="LoadCore"/>가 비늘을 넣고 칸을 채운다.
    /// 마나는 환불하지 않는다.
    /// </summary>
    private void CompleteLoad(ScaleData scale, ScaleItemData item)
    {
        // 칸을 채우기 전에는 잠금을 유지한다. 바를 숨기는 동안 선택이 바뀌면 뺀 비늘과 장전 대상이 어긋난다.
        try
        {
            if (_playerStats != null && _playerStats.IsDead)
                return;

            if (_selectedScale != scale || _selectedItem != item)
            {
                Debug.LogWarning("[ScaleLoader] 장전 중에 선택이 바뀌어 장전을 취소합니다.", this);
                return;
            }

            IScaleMagazine magazine = Magazine;
            if (magazine == null)
                return;

            int count = PlannedCount(magazine, scale, includeMana: true);
            if (count < 1)
            {
                string reason = magazine.GrownCount < 1 ? NoticeText.NoGrownSlot
                    : IsFarmed && CountSelectedItem() < 1 ? NoticeText.SelectedScaleMissing
                    : NoticeText.NotEnoughMana;
                GameNotice.Show(reason);
                return;
            }

            int cost = scale.LoadManaCost * count;
            if (!PlayerCostRules.IsFree && _playerMana != null && !_playerMana.TrySpendMana(cost))
            {
                GameNotice.Show(NoticeText.NotEnoughMana);
                return;
            }

            int loaded = LoadCore(scale, item, count);
            if (loaded < 0)
            {
                GameNotice.Show(NoticeText.SelectedScaleMissing);
                return;
            }

            if (loaded < count)
                Debug.LogWarning($"[ScaleLoader] {count}칸 비용을 냈지만 {loaded}칸만 장전됐습니다.", this);
        }
        finally
        {
            EndLoadState();
            ExitLoading();
        }
    }

    /// <inheritdoc />
    public bool CanLoadNow(int maxCount, out string reason)
    {
        if (_playerStats != null && _playerStats.IsDead)
        {
            reason = NoticeText.CannotUseNow;
            return false;
        }

        if (maxCount < 1 || _selectedScale == null)
        {
            reason = NoticeText.CannotUseNow;
            return false;
        }

        IScaleMagazine magazine = Magazine;
        if (magazine == null)
        {
            reason = NoticeText.CannotUseNow;
            return false;
        }

        if (magazine.CurrentScale != null && magazine.CurrentScale != _selectedScale)
        {
            reason = NoticeText.CannotChangeScale;
            return false;
        }

        if (magazine.GrownCount < 1)
        {
            reason = NoticeText.NoGrownSlot;
            return false;
        }

        if (IsFarmed && CountSelectedItem() < 1)
        {
            reason = NoticeText.SelectedScaleMissing;
            return false;
        }

        reason = null;
        return true;
    }

    /// <inheritdoc />
    public int LoadNow(int maxCount)
    {
        if (_isLoading)
            return 0;

        string reason;
        if (!CanLoadNow(maxCount, out reason))
            return 0;

        IScaleMagazine magazine = Magazine;
        if (magazine == null)
            return 0;

        int count = Mathf.Min(maxCount, magazine.GrownCount);
        if (IsFarmed)
            count = Mathf.Min(count, CountSelectedItem());

        if (count < 1)
            return 0;

        int loaded = LoadCore(_selectedScale, _selectedItem, count);
        return loaded < 0 ? 0 : loaded;
    }

    /// <summary>
    /// 파밍 비늘을 인벤토리에서 빼고 탄창에 넣는다. 다 못 채우면 뺀 것 중 남은 것을 되돌린다.
    /// 빼기에 실패하면 -1. 마나는 내지 않는다.
    /// </summary>
    int LoadCore(ScaleData scale, ScaleItemData item, int count)
    {
        if (scale == null || count < 1)
            return 0;

        IScaleMagazine magazine = Magazine;
        if (magazine == null)
            return 0;

        if (item != null && !TryTakeItem(item, count))
            return -1;

        int loaded = magazine.LoadGrown(scale, count);
        if (item != null && loaded < count)
            ReturnToInventoryOrDrop(item, count - loaded);

        return loaded;
    }

    private bool IsFarmed => _selectedItem != null;

    /// <summary>장전 1회에 실제로 노릴 칸 수. 파밍 비늘이면 인벤토리 수량을 넘지 않는다.</summary>
    private int PlannedCount(IScaleMagazine magazine, ScaleData scale, bool includeMana)
    {
        if (magazine == null || scale == null)
            return 0;

        int count = Mathf.Min(LoadPerAction, magazine.GrownCount);
        if (IsFarmed)
            count = Mathf.Min(count, CountSelectedItem());

        if (includeMana && !PlayerCostRules.IsFree && _playerMana != null && scale.LoadManaCost > 0)
            count = Mathf.Min(count, Mathf.FloorToInt(_playerMana.CurrentMana / scale.LoadManaCost));

        return count < 0 ? 0 : count;
    }

    private int CountSelectedItem()
    {
        if (_selectedItem == null)
            return 0;

        InventoryModel inventory = InventoryController.Instance != null ? InventoryController.Instance.Inventory : null;
        if (inventory == null)
            return 0;

        return inventory.CountOf(_selectedItem);
    }

    private bool TryTakeItem(ScaleItemData item, int count)
    {
        if (item == null || count < 1)
            return false;

        InventoryModel inventory = InventoryController.Instance != null ? InventoryController.Instance.Inventory : null;
        if (inventory == null)
            return false;

        return inventory.TryRemove(item, count);
    }

    /// <summary>해제한 비늘을 되돌린다. 직전 선택이 없으면 기본 비늘이라 버리고, 있으면 그 아이템으로 돌려준다.</summary>
    private void ReturnUnloaded(ScaleItemData selectedItem, ScaleData unloadedScale, int count)
    {
        if (count < 1)
            return;

        if (selectedItem == null)
            return;

        ReturnToInventoryOrDrop(selectedItem, count);
    }

    private void ReturnToInventoryOrDrop(ScaleItemData item, int count)
    {
        if (item == null || count < 1)
            return;

        int remain = count;
        if (InventoryController.Instance != null)
            remain = InventoryController.Instance.AddToInventory(item, count);

        if (remain <= 0)
            return;

        if (ItemDropManager.Instance != null && ItemDropManager.Instance.DropItemStack(item, remain, transform))
            return;

        Debug.LogWarning($"[ScaleLoader] '{item.name}' {remain}개를 인벤토리에도 바닥에도 두지 못했습니다.", this);
    }

    private bool TryEnterLoading(float duration)
    {
        if (_actionGate == null)
            return true;

        return _actionGate.TryEnter(PlayerAction.Loading, this, duration);
    }

    private void ExitLoading()
    {
        if (_actionGate != null)
            _actionGate.Exit(PlayerAction.Loading, this);
    }

    private void EndLoadState()
    {
        _isLoading = false;
        if (_loadRoutine == null)
            return;

        StopCoroutine(_loadRoutine);
        _loadRoutine = null;
    }

}
