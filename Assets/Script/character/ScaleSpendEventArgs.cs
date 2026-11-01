/// <summary>Payload for <see cref="ScaleSpender"/> events.</summary>
public readonly struct ScaleSpendEventArgs
{
    /// <summary>Scaled seconds since the spend began (0 for the started event).</summary>
    public float ElapsedSeconds { get; }

    /// <summary>비늘이 실제로 빠졌으면 true. 거점처럼 허용만 된 완료는 false.</summary>
    public bool Consumed { get; }

    /// <summary>Creates the payload.</summary>
    public ScaleSpendEventArgs(float elapsedSeconds, bool consumed)
    {
        ElapsedSeconds = elapsedSeconds;
        Consumed = consumed;
    }
}
