using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerRoot 아래의 <see cref="IHomeRestorable"/>을 모아 거점 회복을 한 번에 부른다.
/// 누가 회복되는지는 모른다.
/// </summary>
public sealed class PlayerRecovery : MonoBehaviour
{
    public static PlayerRecovery Instance { get; private set; }

    readonly List<IHomeRestorable> _restorables = new List<IHomeRestorable>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Collect();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>모아 둔 회복 대상을 하나씩 되돌린다.</summary>
    public void RestoreAll()
    {
        for (int i = 0; i < _restorables.Count; i++)
        {
            IHomeRestorable restorable = _restorables[i];
            if (restorable is Object unityObject && unityObject == null)
                continue;

            restorable.RestoreForHome();
        }
    }

    void Collect()
    {
        _restorables.Clear();
        IHomeRestorable[] found = GetComponentsInChildren<IHomeRestorable>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                _restorables.Add(found[i]);
        }
    }
}
