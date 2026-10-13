/// <summary>피해를 받을 수 있는 대상. 적, 부서지는 오브젝트, 소환수가 구현한다.</summary>
public interface IDamageable
{
    /// <summary>피해를 받는다.</summary>
    void TakeDamage(float damage);
}
