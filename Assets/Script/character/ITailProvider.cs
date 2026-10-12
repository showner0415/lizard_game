using System;

/// <summary>
/// Reservation contract consumed by <see cref="SpellCaster"/>. This is the DIP boundary:
/// callers know nothing about <see cref="Tail"/> or <see cref="TailManager"/>.
/// </summary>
public interface ITailProvider
{
    /// <summary>Number of tails that could be reserved right now.</summary>
    int AvailableTailCount { get; }

    /// <summary>Raised whenever <see cref="AvailableTailCount"/> may have changed (Observer hook for UI/AI).</summary>
    event Action AvailabilityChanged;

    /// <summary>
    /// Atomically claims one available tail. Returns false (and a null reservation) when none is available;
    /// the caller must then not start the cast.
    /// </summary>
    bool TryReserveTail(out ITailReservation reservation);
}
