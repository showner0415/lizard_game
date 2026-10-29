/// <summary>
/// Handle for a loaded scale slot claimed by a pending spend. Exactly one of <see cref="Confirm"/> or
/// <see cref="Release"/> takes effect; the handle is inert afterwards.
/// </summary>
public interface IScaleReservation
{
    /// <summary>True until the reservation is confirmed or released. Reservations never expire on their own.</summary>
    bool IsActive { get; }

    /// <summary>The scale that was loaded in the reserved slot. Stays readable after confirm/release.</summary>
    ScaleData Scale { get; }

    /// <summary>
    /// Finalizes consumption: the slot becomes Empty and the magazine regrows it in order.
    /// Returns false (and warns) if the reservation is no longer active.
    /// </summary>
    bool Confirm();

    /// <summary>
    /// Cancels the claim: the slot returns to Loaded with its scale kept.
    /// Returns false (and warns) if the reservation is no longer active.
    /// </summary>
    bool Release();
}
