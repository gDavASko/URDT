namespace KBP.URDT.Registry
{
    /// <summary>
    /// Where a registration originated (04 §2 — the three index sources).
    /// </summary>
    public enum RegistrationSource
    {
        SceneScan = 0,
        Incremental = 1,
        LazyResolve = 2
    }
}
