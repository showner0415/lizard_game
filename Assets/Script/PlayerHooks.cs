using System;
using UnityEngine;

/// <summary>
/// PlayerRoot 아래에서 인터페이스를 모아 하나씩 호출한다.
/// 목록은 저장하지 않고, 부를 때마다 다시 찾는다.
/// </summary>
public static class PlayerHooks
{
    /// <summary>
    /// 꺼진 컴포넌트까지 포함해 <typeparamref name="T"/>를 찾아 <paramref name="call"/>한다.
    /// 하나가 예외를 내도 나머지는 계속한다.
    /// </summary>
    public static void InvokeAll<T>(Action<T> call) where T : class
    {
        if (PlayerRoot.Instance == null)
            return;

        T[] found = PlayerRoot.Instance.GetComponentsInChildren<T>(true);
        for (int i = 0; i < found.Length; i++)
        {
            T target = found[i];
            UnityEngine.Object unityObject = target as UnityEngine.Object;
            if (unityObject == null)
                continue;

            try
            {
                call(target);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, unityObject);
            }
        }
    }
}
