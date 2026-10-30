using UnityEngine;

/// <summary>
/// 거점에 들어오면 PlayerRoot 아래의 회복 대상을 한 번에 되돌린다.
/// 누가 회복되는지는 모른다.
/// </summary>
public sealed class PlayerRecovery : MonoBehaviour
{
    public static PlayerRecovery Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>PlayerRoot 아래의 회복 대상을 하나씩 되돌린다.</summary>
    public void RestoreAll()
    {
        PlayerHooks.InvokeAll<IHomeRestorable>(restorable => restorable.RestoreForHome());
    }
}
