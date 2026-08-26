namespace KBP.URDT.Driver
{
    /// <summary>
    /// Phase of a synthetic pointer action (mirrors the RDT Engine Driver SPI).
    /// </summary>
    public enum PointerPhase
    {
        Move = 0,
        Down = 1,
        Up = 2
    }
}
