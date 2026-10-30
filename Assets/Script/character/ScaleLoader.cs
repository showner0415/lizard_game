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
/// 기본 비늘은 <see cref="_defaultScale"/> 하나이며 해제하면 버린다. 파밍 비늘은 인벤토리로 돌아가고 넘치면 바닥에 떨어진다.
/// 장전 중이거나 예약된 칸이 있으면 선택을 바꾸지 않는다.
/// </para>
/// </summary>
public sealed class ScaleLoader : MonoBehaviour, IHomeRestorable, IDeathPenalty
{
    [Header("Magazine")]
    [Tooltip("IScaleMagazine을 구현한 컴포넌트(예: 캐릭터 루트의 ScaleMagazine). 비우면 ScaleSpender.Magazine, 그다음 이 오브젝트와 부모에서 찾는다.")]
    [SerializeField] private MonoBehaviour _magazineSource;

    [Tooltip("탄창을 직접 지정하지 않았을 때 Magazine을 빌려 올 ScaleSpender. 비우면 이 오브젝트와 부모에서 찾는다.")]
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

    private IScaleMagazine _magazine;
    private Coroutine _loadRoutine;
    private bool _isLoading;
    private ScaleData _selectedScale;
    private ScaleItemData _selectedItem;
    private bool _selectionRestored;

    /// <summary>지금 장전 중이면 true.</summary>
    public bool IsLoading => _isLoading;

    /// <summary>장전 1회에 채우는 칸 수. 1 이상.</summary>
    public int LoadPerAction => _loadPerAction < 1 ? 1 : _loadPerAction;

    /// <summary>R로 장전할 비늘. 처음에는 기본 비늘이다.</summary>
    public ScaleData SelectedScale => _selectedScale;

    /// <summary>선택한 파밍 비늘 아이템. 기본 비늘이면 null.</summary>
    public ScaleItemData SelectedItem => _selectedItem;

    /// <summary>목록 첫 항목으로 쓰는 기본 비늘. 지금 선택과 다를 수 있다.</summary>
    public ScaleData DefaultScale => _defaultScale;

    /// <summary>장전에 쓰는 탄창. 아직 없으면 그 시점에 한 번 더 찾는다.</summary>
    public IScaleMagazine Magazine
    {
        get
        {
            if (_magazine == null)
                ResolveMagazine();

            return _magazine;
        }
    }

    private void Awake()
    {
        if (!_selectionRestored)
        {
            _selectedScale = _defaultScale;
            _selectedItem = null;
        }

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

    private void OnEnable()
    {
        if (_actionGate != null)
            _actionGate.ReleaseRequested += HandleGateRelease;
    }

    private void OnDisable()
    {
        if (_actionGate != null)
            _actionGate.ReleaseRequested -= HandleGateRelease;

        CancelLoad();
    }

    void HandleGateRelease(PlayerAction action)
    {
        if (action == PlayerAction.Loading)
            CancelLoad();
    }

    private void OnValidate()
    {
        if (_loadPerAction < 1)
            _loadPerAction = 1;

        if (_magazineSource != null && !(_magazineSource is IScaleMagazine))
        {
            Debug.LogWarning($"[ScaleLoader] '{_magazineSource.name}' does not implement IScaleMagazine.", this);
            _magazineSource = null;
        }
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

        _selectedScale = nextScale;
        _selectedItem = item;
        return true;
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
            Debug.Log("장전할 칸이 없습니다.");
            return false;
        }

        if (IsFarmed && CountSelectedItem() < 1)
        {
            Debug.Log($"{scale.DisplayName} 비늘이 없습니다. G로 바꾸세요");
            return false;
        }

        int count = PlannedCount(magazine, scale, includeMana: false);
        if (count < 1)
            return false;

        int cost = scale.LoadManaCost * count;
        if (_playerMana != null && !_playerMana.CanAfford(cost))
        {
            Debug.Log($"마나가 부족해 장전할 수 없습니다. ({_playerMana.CurrentMana}/{cost})");
            return false;
        }

        if (!TryEnterLoading())
            return false;

        _isLoading = true;
        if (CastingBarController.Instance != null)
            CastingBarController.Instance.Show(this);

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
        if (CastingBarController.Instance != null)
            CastingBarController.Instance.Cancel(this);
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
        ScaleData scale = _defaultScale;
        int count = save.loadedScaleCount < 0 ? 0 : save.loadedScaleCount;

        if (save.loadedScaleItemNum >= 0)
        {
            ItemData found = database != null ? database.Get(save.loadedScaleItemNum) : null;
            if (found is ScaleItemData scaleItem && scaleItem.Scale != null)
            {
                item = scaleItem;
                scale = scaleItem.Scale;
            }
            else
            {
                Debug.LogWarning($"[ScaleLoader] itemNum {save.loadedScaleItemNum} 비늘을 찾지 못했습니다. 장전 칸은 복원하지 않습니다.", this);
                count = 0;
            }
        }

        _selectedItem = item;
        _selectedScale = scale;
        if (count < 1 || scale == null)
            return;

        IScaleMagazine magazine = Magazine;
        if (magazine == null)
        {
            Debug.LogWarning("[ScaleLoader] 탄창이 없어 장전 칸을 복원하지 못했습니다.", this);
            return;
        }

        int loaded = magazine.LoadGrown(scale, count);
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

        // Always wait at least one frame so a 0-second load still completes outside StartCoroutine.
        do
        {
            if (CastingBarController.Instance != null && loadTime > 0f)
                CastingBarController.Instance.UpdateFill(this, Time.time - startTime, loadTime);
            yield return null;
        }
        while (Time.time < completeAtTime);

        _loadRoutine = null;
        CompleteLoad(scale, item);
    }

    /// <summary>
    /// 끝나는 순간 칸 수, 인벤토리, 마나를 다시 계산한다.
    /// 파밍 비늘을 먼저 빼고, 그다음 마나를 낸 뒤 장전한다. 마나는 환불하지 않는다.
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
                Debug.Log("장전할 칸이 없거나 마나가 부족해 장전하지 못했습니다.");
                return;
            }

            if (item != null && !TryTakeItem(item, count))
            {
                Debug.Log($"{scale.DisplayName} 비늘이 없습니다. G로 바꾸세요");
                return;
            }

            int cost = scale.LoadManaCost * count;
            if (_playerMana != null && !_playerMana.TrySpendMana(cost))
            {
                // 마나를 내지 못했으므로 미리 뺀 비늘은 되돌린다. 낸 마나를 환불하는 경로는 아니다.
                if (item != null)
                    ReturnToInventoryOrDrop(item, count);

                Debug.Log("마나가 부족해 장전하지 못했습니다.");
                return;
            }

            int loaded = magazine.LoadGrown(scale, count);
            if (loaded < count)
            {
                if (item != null)
                    ReturnToInventoryOrDrop(item, count - loaded);

                Debug.LogWarning($"[ScaleLoader] {count}칸 비용을 냈지만 {loaded}칸만 장전됐습니다.", this);
            }
        }
        finally
        {
            EndLoadState();
            ExitLoading();
            if (CastingBarController.Instance != null)
                CastingBarController.Instance.Hide(this);
        }
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

        if (includeMana && _playerMana != null && scale.LoadManaCost > 0)
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

    /// <summary>해제한 파밍 비늘을 선택 아이템으로 되돌린다. 기본 비늘은 버리고 마나는 환불하지 않는다.</summary>
    private void ReturnUnloaded(ScaleItemData selectedItem, ScaleData unloadedScale, int count)
    {
        // 기본 비늘은 _defaultScale 하나로 판별한다. 티어로 판별하면 0티어 파밍 비늘이 사라진다.
        if (count < 1 || unloadedScale == null || unloadedScale == _defaultScale)
            return;

        if (selectedItem != null && selectedItem.Scale == unloadedScale)
        {
            ReturnToInventoryOrDrop(selectedItem, count);
            return;
        }

        string itemName = selectedItem != null ? selectedItem.name : "없음";
        Debug.LogWarning($"[ScaleLoader] 해제한 '{unloadedScale.name}'과 선택 아이템 '{itemName}'이 다릅니다. 인벤토리로 되돌리지 못했습니다.", this);
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

    private bool TryEnterLoading()
    {
        if (_actionGate == null)
            return true;

        return _actionGate.TryEnter(PlayerAction.Loading);
    }

    private void ExitLoading()
    {
        if (_actionGate != null)
            _actionGate.Exit(PlayerAction.Loading);
    }

    private void EndLoadState()
    {
        _isLoading = false;
        if (_loadRoutine == null)
            return;

        StopCoroutine(_loadRoutine);
        _loadRoutine = null;
    }

    private void ResolveMagazine()
    {
        if (_magazineSource != null)
        {
            _magazine = _magazineSource as IScaleMagazine;
            if (_magazine == null)
                Debug.LogWarning($"[ScaleLoader] '{_magazineSource.name}' does not implement IScaleMagazine.", this);
        }

        if (_magazine == null && _scaleSpender != null)
            _magazine = _scaleSpender.Magazine;

        if (_magazine == null)
            _magazine = GetComponentInParent<IScaleMagazine>();

        if (_magazine == null)
            Debug.LogWarning("[ScaleLoader] No IScaleMagazine resolved; loading cannot start.", this);
    }
}
