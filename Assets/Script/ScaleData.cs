using UnityEngine;

/// <summary>
/// 비늘 한 종류의 정의. 탄창 칸에 장전되는 내용물이다.
/// 데미지, 속성, 투사체는 아직 없다.
/// </summary>
[CreateAssetMenu(fileName = "ScaleData", menuName = "Scale/Scale Data")]
public class ScaleData : ScriptableObject
{
    [Tooltip("화면에 보여 줄 이름.")]
    [SerializeField] private string displayName;

    [Tooltip("UI에 쓸 아이콘.")]
    [SerializeField] private Sprite icon;

    [Tooltip("비늘 등급. 0이면 기본 비늘이다.")]
    [SerializeField, Min(0)] private int tier;

    [Tooltip("이 비늘을 칸 1개에 장전할 때 드는 마나.")]
    [SerializeField, Min(0)] private int loadManaCost = 1;

    [Tooltip("장전 1회에 걸리는 시간(초, scaled time). 0이면 다음 프레임에 끝난다.")]
    [SerializeField, Min(0f)] private float loadTime = 1f;

    [Tooltip("이 비늘이 장전된 칸의 색.")]
    [SerializeField] private Color slotColor = Color.white;

    /// <summary>화면에 보여 줄 이름. 비어 있으면 에셋 이름.</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    /// <summary>UI 아이콘. 없을 수 있다.</summary>
    public Sprite Icon => icon;

    /// <summary>비늘 등급. 0이면 기본 비늘.</summary>
    public int Tier => tier < 0 ? 0 : tier;

    /// <summary>칸 1개를 장전할 때 드는 마나. 0 이상.</summary>
    public int LoadManaCost => loadManaCost < 0 ? 0 : loadManaCost;

    /// <summary>장전 1회에 걸리는 시간(초). 0 이상.</summary>
    public float LoadTime => float.IsNaN(loadTime) || float.IsInfinity(loadTime) || loadTime < 0f ? 0f : loadTime;

    /// <summary>장전된 칸의 색.</summary>
    public Color SlotColor => slotColor;

    private void OnValidate()
    {
        if (tier < 0)
        {
            Debug.LogWarning($"[ScaleData] '{name}' tier {tier}는 0으로 맞춥니다.", this);
            tier = 0;
        }

        if (loadManaCost < 0)
        {
            Debug.LogWarning($"[ScaleData] '{name}' loadManaCost {loadManaCost}는 0으로 맞춥니다.", this);
            loadManaCost = 0;
        }

        if (float.IsNaN(loadTime) || float.IsInfinity(loadTime) || loadTime < 0f)
        {
            Debug.LogWarning($"[ScaleData] '{name}' loadTime {loadTime}는 0으로 맞춥니다.", this);
            loadTime = 0f;
        }
    }
}
