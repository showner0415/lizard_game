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
    const string BackupFileName = "save.json.bak";
    const string SessionBackupFileName = "save.session.bak";

    [SerializeField] private ItemDatabase itemDatabase;

    [Header("Debug")]
    [SerializeField] private bool logSavePath = true;

    [Header("Raid")]
    [Tooltip("에디터에서 플레이를 멈추는 것도 게임 종료로 잡힌다. 끄면 에디터에서만 레이드 중 세이브를 사망 처리하지 않는다.")]
    [SerializeField] private bool _raidQuitIsDeathInEditor = true;

    bool _loadFailed;

    public SaveData Current { get; private set; } = new SaveData();
    public ItemDatabase Database => itemDatabase;
    public string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);
    string SessionBackupPath => Path.Combine(Application.persistentDataPath, SessionBackupFileName);
    string TempPath => SavePath + ".tmp";

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
    /// 게임 데이터를 즉시 저장 (메모리 → 디스크).
    /// 호출 시점: 던전 입장, 탈출, 사망 패널티를 적용한 뒤, 레이드 중 종료 세이브를 불러온 뒤, 게임 종료, 창고 닫기.
    /// 사망으로 잃는 처리는 하지 않는다. 그 일은 저장 전에 PlayerDeath.ApplyPenalties가 한다.
    /// </summary>
    public void SaveGame()
    {
        if (_loadFailed)
        {
            Debug.LogError("[SaveManager] 불러오기에 실패해 이번 세션은 저장하지 않습니다. 원본 파일은 수정하지 않았습니다.");
            return;
        }

        Debug.Log("[SaveManager] SaveGame 시작");
        CaptureFromSystems();
        WriteToDisk();
        Debug.Log("[SaveManager] ✓ SaveGame 완료");
    }

    /// <summary>던전 입구. 현재 가방을 레이드 인벤으로 확정하고 저장.</summary>
    public void SaveOnEnterDungeon()
    {
        if (InventoryController.Instance != null)
            InventoryController.Instance.BeginRaid();
        SaveGame();
    }

    /// <summary>탈출 성공. 들고 나온 인벤을 유지한 채 디스크에 확정.</summary>
    public void SaveOnExtract()
    {
        if (InventoryController.Instance != null)
            InventoryController.Instance.EndRaidExtract();
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

        // 커서는 어느 모델에도 없어서, 되돌리지 않으면 저장에서 빠진다.
        ItemTransfer.ReturnHeld();

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

        ScaleLoader scaleLoader = FindScaleLoader();
        if (scaleLoader != null)
        {
            scaleLoader.CaptureState(Current);
            if (Current.version < 2)
                Current.version = 2;
            Debug.Log($"[SaveManager] ✓ 비늘 캡처 완료 (item {Current.loadedScaleItemNum}, {Current.loadedScaleCount}칸)");
        }

        Debug.Log($"[SaveManager] ✓ CaptureFromSystems 완료 - 창고: {Current.stash?.Length ?? 0}개 슬롯");
    }

    void ApplyToSystems()
    {
        try
        {
            if (InventoryController.Instance != null)
                InventoryController.Instance.RestoreState(Current);
        }
        catch (System.Exception exception)
        {
            MarkLoadFailed();
            Debug.LogException(exception, this);
        }

        try
        {
            if (HotbarController.Instance != null)
                HotbarController.Instance.RestoreState(Current);
        }
        catch (System.Exception exception)
        {
            MarkLoadFailed();
            Debug.LogException(exception, this);
        }

        // version 2 미만은 탄창 필드가 없다. 없는 int는 0이라 itemNum 0번 아이템으로 오해할 수 있다.
        if (Current.version >= 2)
        {
            try
            {
                ScaleLoader scaleLoader = FindScaleLoader();
                if (scaleLoader != null)
                    scaleLoader.RestoreState(Current, itemDatabase);
            }
            catch (System.Exception exception)
            {
                MarkLoadFailed();
                Debug.LogException(exception, this);
            }
        }

        if (_loadFailed)
            return;

        // 종료 시점이 아니라 불러올 때 처리한다. 강제 종료와 크래시도 마지막 세이브가 레이드로 남기 때문이다.
        if (Current.inRaid && ShouldTreatRaidQuitAsDeath())
        {
            try
            {
                Debug.Log("[SaveManager] 레이드 중 종료된 세이브입니다. 사망으로 처리합니다.");
                PlayerDeath.ApplyPenalties();
                SaveGame();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    /// <summary>빌드에서는 항상 사망으로 본다. 에디터는 인스펙터 값을 따른다.</summary>
    bool ShouldTreatRaidQuitAsDeath()
    {
        if (Application.isEditor)
            return _raidQuitIsDeathInEditor;

        return true;
    }

    static ScaleLoader FindScaleLoader()
    {
        if (PlayerRoot.Instance == null)
            return null;

        return PlayerRoot.Instance.GetComponentInChildren<ScaleLoader>(true);
    }

    // =========================================================
    // 파일 I/O
    // =========================================================

    void LoadFromDisk()
    {
        if (TryRead(SavePath, out SaveData data))
        {
            Current = data;
            RememberSessionStart(SavePath);
            if (logSavePath)
                Debug.Log($"세이브 로드 완료: {SavePath}");
            return;
        }

        if (File.Exists(SavePath))
        {
            Debug.LogError("[SaveManager] save.json을 읽지 못했습니다. 백업을 시도합니다.");
            TryCopy(SavePath, SavePath + ".corrupt");
        }

        if (TryRead(BackupPath, out data))
        {
            Current = data;
            Debug.LogWarning("[SaveManager] 백업으로 복구했습니다.");
            // 깨진 save.json을 그대로 두면 다음 저장의 File.Replace가 그 파일을 bak으로 올려 복구본을 지운다.
            if (!TryCopy(BackupPath, SavePath))
                TryDelete(SavePath);
            RememberSessionStart(BackupPath);
            return;
        }

        if (TryRead(TempPath, out data))
        {
            Current = data;
            Debug.LogWarning("[SaveManager] 임시 파일로 복구했습니다.");
            if (!TryCopy(TempPath, SavePath))
                TryDelete(SavePath);
            RememberSessionStart(TempPath);
            return;
        }

        Current = new SaveData();
        if (File.Exists(SavePath) || File.Exists(BackupPath) || File.Exists(TempPath))
        {
            MarkLoadFailed();
            Debug.LogError("[SaveManager] 세이브와 백업을 모두 읽지 못했습니다. 이번 실행은 저장하지 않습니다.");
            return;
        }

        if (logSavePath)
            Debug.Log($"세이브 없음. 새로 시작. 경로: {SavePath}");
    }

    void WriteToDisk()
    {
        try
        {
            File.WriteAllText(TempPath, JsonUtility.ToJson(Current, true));
            if (File.Exists(SavePath))
                ReplaceSaveFile();
            else
                File.Move(TempPath, SavePath);

            if (logSavePath)
                Debug.Log($"세이브 저장: {SavePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"세이브 저장 실패: {e.Message}");
        }
    }

    void ReplaceSaveFile()
    {
        try
        {
            File.Replace(TempPath, SavePath, BackupPath);
        }
        catch (System.Exception)
        {
            TryCopy(SavePath, BackupPath);
            File.Copy(TempPath, SavePath, true);
            File.Delete(TempPath);
        }
    }

    void RememberSessionStart(string sourcePath)
    {
        TryCopy(sourcePath, SessionBackupPath);
    }

    void MarkLoadFailed()
    {
        _loadFailed = true;
        if (GetComponent<SaveFailureNotice>() == null)
            gameObject.AddComponent<SaveFailureNotice>();
    }

    static bool TryRead(string path, out SaveData data)
    {
        data = null;
        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            data = JsonUtility.FromJson<SaveData>(json);
            return data != null;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    static bool TryCopy(string source, string destination)
    {
        try
        {
            File.Copy(source, destination, true);
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[SaveManager] 파일을 복사하지 못했습니다. {exception.Message}");
            return false;
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[SaveManager] 파일을 지우지 못했습니다. {exception.Message}");
        }
    }

    [ContextMenu("Save Now")]
    void DebugSave() => SaveGame();

    [ContextMenu("Delete Save File")]
    void DebugDeleteSave()
    {
        TryDelete(SavePath);
        TryDelete(BackupPath);
        TryDelete(SessionBackupPath);
        TryDelete(TempPath);
        TryDelete(SavePath + ".corrupt");

        Current = new SaveData();
        _loadFailed = false;
        Debug.Log("세이브 파일 삭제됨");
    }
}
