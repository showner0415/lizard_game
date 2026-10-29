/// <summary>Payload for <see cref="ScaleSpender"/> events.</summary>
public readonly struct ScaleSpendEventArgs
{
    /// <summary>Scaled seconds since the spend began (0 for the started event).</summary>
    public float ElapsedSeconds { get; }

    /// <summary>True only when a scale was actually consumed by this spend.</summary>
    public bool ScaleConsumed { get; }

    /// <summary>Creates the payload.</summary>
    public ScaleSpendEventArgs(float elapsedSeconds, bool scaleConsumed)
    {
        ElapsedSeconds = elapsedSeconds;
        ScaleConsumed = scaleConsumed;
    }
}
