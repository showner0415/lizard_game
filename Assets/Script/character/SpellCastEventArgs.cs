/// <summary>Payload for <see cref="SpellCaster"/> events.</summary>
public readonly struct SpellCastEventArgs
{
    /// <summary>Scaled seconds since the cast began (0 for the started event).</summary>
    public float ElapsedSeconds { get; }

    /// <summary>True only when a tail was actually consumed by this cast.</summary>
    public bool TailConsumed { get; }

    /// <summary>Creates the payload.</summary>
    public SpellCastEventArgs(float elapsedSeconds, bool tailConsumed)
    {
        ElapsedSeconds = elapsedSeconds;
        TailConsumed = tailConsumed;
    }
}
