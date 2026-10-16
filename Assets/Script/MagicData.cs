using UnityEngine;

/// <summary>버튼을 어떻게 눌러 시전하는지.</summary>
public enum CastMode
{
    /// <summary>누르고 있으면 게이지가 차고, 떼었을 때 시간이 충분하면 시전한다.</summary>
    Charge = 0,

    /// <summary>누르는 동안 유지되고, 떼거나 꼬리가 떨어지면 끝난다.</summary>
    Channel = 1,

    /// <summary>누르는 순간 마나를 내고 바로 시전한다.</summary>
    Instant = 2
}

/// <summary>시전 방식, 시간, 마나, 꼬리, 흑마법 여부처럼 모든 마법이 공유하는 데이터.</summary>
public abstract class MagicData : ScriptableObject
{
    [Header("Basic")]
    /// <summary>흑마법이면 true. 상자 종류를 가를 때 사용한다.</summary>
    public bool isDarkMagic;

    [SerializeField, Tooltip("Charge는 누르고 떼기, Channel은 누르는 동안, Instant는 누르자마자.")]
    CastMode castMode = CastMode.Charge;

    /// <summary>시전 게이지가 차는 시간(초).</summary>
    public float castingTime = 2f;

    [Tooltip("시전 1회당 소모 마나. 0이면 무료.")]
    [SerializeField, Min(0)] private int manaCost;

    [Tooltip("시전 1회당 소모하는 꼬리 수. 0이면 꼬리를 쓰지 않는다. 한 번에 여러 발을 쏘아도 시전 1회분이다.")]
    [SerializeField, Min(0)] private int tailCost = 1;

    [SerializeField, Range(0f, 1f), Tooltip("범위 효과가 시전자에게 주는 피해 배율. 0이면 맞지 않고, 1이면 적과 같다.")]
    float selfDamageScale;

    /// <summary>시전 1회당 소모 마나 (0 이상).</summary>
    public int ManaCost => manaCost;

    /// <summary>시전 1회당 소모하는 꼬리 수. 0이면 꼬리를 쓰지 않는다. 채널링의 초당 소모와는 별개다.</summary>
    public int TailCost => tailCost < 0 ? 0 : tailCost;

    /// <summary>이 마법을 어떻게 누르는지.</summary>
    public CastMode Mode => castMode;

    /// <summary>범위 효과가 시전자에게 주는 배율. 0 이상 1 이하.</summary>
    public float SelfDamageScale => Mathf.Clamp01(selfDamageScale);

    /// <summary>이 마법의 피해 출처. 하위 클래스가 시전할 때 넘긴다.</summary>
    protected DamageSource CreateSource(GameObject caster)
    {
        return new DamageSource(caster, SelfDamageScale);
    }

    protected virtual void OnValidate()
    {
        if (tailCost < 0)
        {
            Debug.LogWarning($"[MagicData] '{name}' tailCost {tailCost}는 0으로 맞춥니다.", this);
            tailCost = 0;
        }

        if (float.IsNaN(selfDamageScale) || float.IsInfinity(selfDamageScale) || selfDamageScale < 0f || selfDamageScale > 1f)
        {
            float clamped = Mathf.Clamp01(float.IsNaN(selfDamageScale) ? 0f : selfDamageScale);
            Debug.LogWarning($"[MagicData] '{name}' selfDamageScale {selfDamageScale}는 {clamped}로 맞춥니다.", this);
            selfDamageScale = clamped;
        }
    }

    /// <summary>이 시전자가 지금 이 마법을 쓸 수 있으면 true. 기본은 항상 가능하다.</summary>
    public virtual bool CanCast(GameObject caster) => true;

    /// <summary>시전에 마우스 바닥 좌표가 필요하면 true. 없으면 시전자 위치로 대신할 수 있다.</summary>
    public virtual bool NeedsTargetPoint => true;

    /// <summary>시전을 실행한다. 투사체, 회복 등 실제 효과는 하위 클래스가 담당한다.</summary>
    public abstract void Cast(GameObject caster, Vector3 targetPos);
}
