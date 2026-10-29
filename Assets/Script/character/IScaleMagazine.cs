using System;

/// <summary>
/// Scale magazine contract consumed by <see cref="ScaleSpender"/> and <see cref="ScaleLoader"/>. This is the DIP boundary:
/// callers know nothing about <see cref="ScaleSlot"/> or <see cref="ScaleMagazine"/>.
/// The magazine knows nothing about players, inventory or mana, so enemies can use it too.
/// </summary>
public interface IScaleMagazine
{
    /// <summary>Total number of slots.</summary>
    int Capacity { get; }

    /// <summary>Slots that hold a scale and can be reserved right now.</summary>
    int LoadedCount { get; }

    /// <summary>Slots claimed by a pending spend. They still hold a scale.</summary>
    int ReservedCount { get; }

    /// <summary>Slots that have regrown and can be loaded.</summary>
    int GrownCount { get; }

    /// <summary>Slots that are empty and waiting to regrow.</summary>
    int EmptyCount { get; }

    /// <summary>
    /// The <see cref="ScaleData"/> held by a Loaded or Reserved slot.
    /// Null when no slot holds a scale. A magazine never holds two different assets.
    /// </summary>
    ScaleData CurrentScale { get; }

    /// <summary>Raised whenever any slot changes state (Observer hook for UI/AI).</summary>
    event Action SlotsChanged;

    /// <summary>
    /// Atomically claims the front-most Loaded slot. Returns false (and a null reservation) when none is loaded;
    /// the caller must then not start the spend.
    /// </summary>
    bool TryReserveScale(out IScaleReservation reservation);

    /// <summary>
    /// Loads <paramref name="scale"/> into up to <paramref name="count"/> Grown slots, front first.
    /// Loaded slots are never touched. Returns 0 (and warns) when the magazine already holds a different scale.
    /// Returns how many slots were actually loaded.
    /// </summary>
    int LoadGrown(ScaleData scale, int count);

    /// <summary>
    /// Immediately grows up to <paramref name="count"/> Empty slots, front first.
    /// Returns how many actually became Grown.
    /// </summary>
    int RestoreRegeneratingScales(int count);

    /// <summary>
    /// Returns every Loaded slot to Grown and drops the scale it held.
    /// <paramref name="unloadedScale"/> is that scale, or null when nothing was unloaded.
    /// Reserved slots are not touched. Returns how many slots were unloaded.
    /// </summary>
    int UnloadAll(out ScaleData unloadedScale);
}
