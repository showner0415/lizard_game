using System.IO;
using UnityEngine;

/// <summary>
/// SaveData ↔ JSON 파일. 저장/불러오기의 입구.
///
/// 저장: 각 시스템이 자기 데이터를 SaveData에 담음 → JSON → 디스크
/// 불러오기: 디스크 → JSON → SaveData → 각 시스템이 복원
/// </summary>
[DefaultExecutionOrder(-100)]
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    const string FileName = "save.json";

    [SerializeField] private ItemDatabase itemDatabase;

    [Header("Debug")]
    [SerializeField] private bool logSavePath = true;

    public SaveData Current { get; private set; } = new SaveData();
    public ItemDatabase Database => itemDatabase;
    public string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        if (itemDatabase != null)
            itemDatabase.Init();
        else
            Debug.LogError("SaveManager: ItemDatabase가 비어 있습니다. Inspector에서 할당하세요.");

        LoadFromDisk();
    }

    private void Start()
    {
        ApplyToSystems();
    }

    private void OnApplicationQuit()
    {
        // 게임 종료 시 최종 저장
        Debug.Log("[SaveManager] 게임 종료 - 최종 저장 실행");
        SaveGame();
    }

    // =========================================================
    // 게임 이벤트
    // =========================================================

    /// <summary>
    /// 게임 데이터를 즉시 저장 (메모리 → 디스크)
    /// 호출 시점: 던전 입장, 탈출, 사망, 게임 종료, 창고 닫기
    /// </summary>
    public void SaveGame()
    {
        Debug.Log("[SaveManager] SaveGame 시작");
        CaptureFromSystems();
        WriteToDisk();
        Debug.Log("[SaveManager] ✓ SaveGame 완료");
    }

    /// <summary>던전 입구. 현재 가방을 레이드 인벤으로 확정하고 저장.</summary>
    public void SaveOnEnterDungeon()
    {
        InventoryController.Instance?.BeginRaid();
        SaveGame();
    }

    /// <summary>탈출 성공. 들고 나온 인벤을 유지한 채 디스크에 확정.</summary>
    public void SaveOnExtract()
    {
        InventoryController.Instance?.EndRaidExtract();
        SaveGame();
    }

    /// <summary>사망. 던전 인벤과 장비를 버리고 창고는 남긴 뒤 저장.</summary>
    public void SaveOnDeath()
    {
        InventoryController.Instance?.LoseRaidInventory();
        SaveGame();
    }

    // =========================================================
    // 시스템 ↔ SaveData
    // =========================================================

    /// <summary>
    /// 각 시스템에서 현재 상태를 SaveData로 수집
    /// </summary>
    void CaptureFromSystems()
    {
        Debug.Log("[SaveManager] CaptureFromSystems 시작");

        // Controller → SaveData 직렬화
        if (InventoryController.Instance != null)
        {
            InventoryController.Instance.CaptureState(Current);
            Debug.Log($"[SaveManager] ✓ 인벤토리 캡처 완료 ({Current.inventory?.Length ?? 0}개 슬롯)");
        }

        if (HotbarController.Instance != null)
        {
            HotbarController.Instance.CaptureState(Current);
            Debug.Log($"[SaveManager] ✓ 핫바 캡처 완료 ({Current.hotbar?.Length ?? 0}개 슬롯)");
        }

        Debug.Log($"[SaveManager] ✓ CaptureFromSystems 완료 - 창고: {Current.stash?.Length ?? 0}개 슬롯");
    }

    void ApplyToSystems()
    {
        if (InventoryController.Instance != null)
            InventoryController.Instance.RestoreState(Current);

        if (HotbarController.Instance != null)
            HotbarController.Instance.RestoreState(Current);
    }

    // =========================================================
    // 파일 I/O
    // =========================================================

    void LoadFromDisk()
    {
        if (!File.Exists(SavePath))
        {
            Current = new SaveData();
            if (logSavePath)
                Debug.Log($"세이브 없음. 새로 시작. 경로: {SavePath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            Current = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            if (logSavePath)
                Debug.Log($"세이브 로드 완료: {SavePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"세이브 로드 실패: {e.Message}");
            Current = new SaveData();
        }
    }

    void WriteToDisk()
    {
        try
        {
            string json = JsonUtility.ToJson(Current, true);
            File.WriteAllText(SavePath, json);
            if (logSavePath)
                Debug.Log($"세이브 저장: {SavePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"세이브 저장 실패: {e.Message}");
        }
    }

    [ContextMenu("Save Now")]
    void DebugSave() => SaveGame();

    [ContextMenu("Delete Save File")]
    void DebugDeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);

        Current = new SaveData();
        Debug.Log("세이브 파일 삭제됨");
    }
}
