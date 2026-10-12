using UnityEngine;

/// <summary>시전 시간, 마나, 흑마법 여부처럼 모든 마법이 공유하는 데이터.</summary>
public abstract class MagicData : ScriptableObject
{
    [Header("Basic")]
    /// <summary>흑마법이면 true. 상자 종류를 가를 때 사용한다.</summary>
    public bool isDarkMagic;

    /// <summary>시전 게이지가 차는 시간(초).</summary>
    public float castingTime = 2f;

    [Tooltip("시전 1회당 소모 마나. 0이면 무료.")]
    [SerializeField, Min(0)] private int manaCost;

    /// <summary>시전 1회당 소모 마나 (0 이상).</summary>
    public int ManaCost => manaCost;

    /// <summary>시전을 실행한다. 투사체, 회복 등 실제 효과는 하위 클래스가 담당한다.</summary>
    public abstract void Cast(GameObject caster, Vector3 targetPos);
}
