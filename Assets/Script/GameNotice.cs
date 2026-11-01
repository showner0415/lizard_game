using System;
using UnityEngine;

/// <summary>플레이어에게 한 줄 알림을 보낸다. 구독자가 없으면 콘솔에 남긴다.</summary>
public static class GameNotice
{
    /// <summary>알림의 세기.</summary>
    public enum NoticeLevel
    {
        /// <summary>정보.</summary>
        Info,

        /// <summary>주의.</summary>
        Warning
    }

    /// <summary>알림이 올라올 때 문구와 세기와 함께 발생한다.</summary>
    public static event Action<string, NoticeLevel> Posted;

    /// <summary>알림을 올린다. 빈 문구는 무시한다.</summary>
    public static void Show(string message, NoticeLevel level = NoticeLevel.Warning)
    {
        if (string.IsNullOrEmpty(message))
            return;

        Action<string, NoticeLevel> handlers = Posted;
        if (handlers == null)
        {
            Debug.Log(message);
            return;
        }

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<string, NoticeLevel>)handler).Invoke(message, level);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Posted = null;
    }
}
