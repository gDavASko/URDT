namespace KBP.URDT.Driver
{
    /// <summary>
    /// Query selector for node lookup (mirrors the RDT SPI Selector contract).
    /// Only one criterion needs to be set; unset criteria are ignored.
    /// </summary>
    public sealed class Selector
    {
        public string ByRule { get; set; }

        public string ByTag { get; set; }

        public string ByPath { get; set; }

        public string ByComponent { get; set; }
    }
}
