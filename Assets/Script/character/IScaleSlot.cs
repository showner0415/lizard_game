using System;

/// <summary>
/// Lifecycle states of a scale slot. Valid transitions only:
/// <c>Empty → Grown → Loaded → Reserved → Empty</c>,
/// plus <c>Reserved → Loaded</c> (reservation released)
/// and <c>Loaded → Grown</c> (unloaded, the scale is dropped).
/// </summary>
public enum ScaleSlotState
{
    /// <summary>Nothing in the slot. Hidden. The magazine regrows Empty slots one at a time, front first.</summary>
    Empty,

    /// <summary>A base scale has grown back. Visible with the default look, but cannot be fired yet.</summary>
    Grown,

    /// <summary>Holds a <see cref="ScaleData"/> and can be reserved for firing. Visible with the scale's color.</summary>
    Loaded,

    /// <summary>Claimed by a pending spend (wind-up). Confirm empties it; release returns it to <see cref="Loaded"/>.</summary>
    Reserved
}

/// <summary>
/// Contract between <see cref="ScaleSlot"/> and <see cref="ScaleMagazine"/>. The slot has no timers;
/// its state only changes through these calls. Reserve/confirm/release are gated by a reservation token
/// so a stale reservation (e.g. one already released) can never act on a slot claimed by someone else.
/// </summary>
public interface IScaleSlot
{
    /// <summary>Current state of the slot.</summary>
    ScaleSlotState State { get; }

    /// <summary>The scale inside the slot. Non-null only while <see cref="ScaleSlotState.Loaded"/> or <see cref="ScaleSlotState.Reserved"/>.</summary>
    ScaleData Scale { get; }

    /// <summary>Raised on every valid state change as (slot, previousState, newState).</summary>
    event Action<IScaleSlot, ScaleSlotState, ScaleSlotState> StateChanged;

    /// <summary>Raised when a confirmed reservation empties the slot.</summary>
    event Action<IScaleSlot> OnScaleConsumed;

    /// <summary>Moves <c>Empty → Grown</c>. Returns false (and warns) in any other state.</summary>
    bool TryGrow();

    /// <summary>Moves <c>Grown → Loaded</c> holding <paramref name="scale"/>. Returns false (and warns) for null or any other state.</summary>
    bool TryLoad(ScaleData scale);

    /// <summary>Moves <c>Loaded → Grown</c> and drops the scale. Returns false (and warns) if the slot is not loaded.</summary>
    bool TryUnload();

    /// <summary>Atomically moves <c>Loaded → Reserved</c> and returns the ownership token. Returns false if not loaded.</summary>
    bool TryReserve(out int token);

    /// <summary>Finalizes consumption (<c>Reserved → Empty</c>). Returns false if not reserved by <paramref name="token"/>.</summary>
    bool Confirm(int token);

    /// <summary>Returns the slot to <c>Loaded</c> with its scale kept. Returns false if not reserved by <paramref name="token"/>.</summary>
    bool Release(int token);
}
