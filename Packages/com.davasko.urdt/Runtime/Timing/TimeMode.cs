namespace KBP.URDT.Timing
{
    /// <summary>
    /// Time-control mode of URDT (see 03-determinism-time-control.md §2).
    /// Deterministic mode advances the game only via explicit frame stepping and
    /// guarantees reproducibility; realtime lets the engine drive frames (best-effort).
    /// </summary>
    public enum TimeMode
    {
        Realtime = 0,
        Deterministic = 1
    }
}
