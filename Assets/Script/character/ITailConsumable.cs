using System;

/// <summary>
/// Lifecycle states of a tail. Valid transitions only:
/// <c>Available → Reserved → Consumed → Regenerating → Available</c>,
/// plus <c>Reserved → Available</c> (reservation released).
/// </summary>
public enum TailState
{
    /// <summary>Eligible for reservation.</summary>
    Available,

    /// <summary>Claimed by a pending cast. No visual change yet.</summary>
    Reserved,

    /// <summary>Momentary state entered by a confirmed reservation; immediately becomes <see cref="Regenerating"/>.</summary>
    Consumed,

    /// <summary>Visuals are disabled while the regeneration timer runs.</summary>
    Regenerating
}

/// <summary>
/// Consume-side contract between <see cref="Tail"/> and <see cref="TailManager"/>.
/// Every state-changing call is gated by a reservation token so a stale reservation
/// (e.g. one that timed out) can never act on a tail claimed by someone else.
/// </summary>
public interface ITailConsumable
{
    /// <summary>Current state of the tail.</summary>
    TailState State { get; }

    /// <summary>True when the tail can be reserved right now.</summary>
    bool IsAvailable { get; }

    /// <summary>Raised on every valid state change as (tail, previousState, newState).</summary>
    event Action<ITailConsumable, TailState, TailState> StateChanged;

    /// <summary>Raised when a confirmed reservation disables the tail's presentation.</summary>
    event Action<ITailConsumable> OnTailConsumed;

    /// <summary>Atomically moves <c>Available → Reserved</c> and returns the ownership token.</summary>
    bool TryReserve(out int token);

    /// <summary>Finalizes consumption (<c>Reserved → Consumed → Regenerating</c>). Returns false if not reserved by <paramref name="token"/>.</summary>
    bool Confirm(int token);

    /// <summary>Returns the tail to <c>Available</c> without visual change. Returns false if not reserved by <paramref name="token"/>.</summary>
    bool Release(int token);

    /// <summary>Finishes regeneration immediately. Only valid while <see cref="TailState.Regenerating"/>.</summary>
    bool ForceRegenerate();
}
