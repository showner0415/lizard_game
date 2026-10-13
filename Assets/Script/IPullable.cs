using UnityEngine;

/// <summary>올가미에 한 번 붙잡히면, 그 다음 이동은 스스로 한다.</summary>
public interface IPullable
{
    /// <summary>대상을 따라간다. 플레이어가 움직여도 계속 쫓는다.</summary>
    void BeginPull(Transform target, float speed);
}
