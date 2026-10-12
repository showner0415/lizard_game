/// <summary>
/// Handle for a tail claimed by a pending cast. Exactly one of <see cref="Confirm"/> or
/// <see cref="Release"/> takes effect; the handle is inert afterwards.
/// </summary>
public interface ITailReservation
{
    /// <summary>True until the reservation is confirmed, released or auto-released by timeout.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Finalizes consumption: the tail's visuals are disabled and its regeneration timer starts.
    /// Returns false (and warns) if the reservation is no longer active.
    /// </summary>
    bool Confirm();

    /// <summary>
    /// Cancels the claim: the tail becomes available again with no visual change.
    /// Returns false (and warns) if the reservation is no longer active.
    /// </summary>
    bool Release();
}
