using System;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Marks a field or property of a component as visible to the URDT
    /// StateInspector, so `inspect` includes it in the whitelisted state slice
    /// without modifying URDT itself.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TestInspectableAttribute : Attribute
    {
    }
}
