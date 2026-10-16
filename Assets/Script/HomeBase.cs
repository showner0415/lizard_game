using UnityEngine;

/// <summary>거점 씬에 있는 표식. 이 씬에 들어오면 플레이어를 전부 회복한다.</summary>
public sealed class HomeBase : MonoBehaviour
{
    void Start()
    {
        if (PlayerRecovery.Instance == null)
        {
            Debug.LogWarning("[HomeBase] PlayerRecovery가 없습니다.", this);
            return;
        }

        PlayerRecovery.Instance.RestoreAll();
    }
}
