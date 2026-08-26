using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Driver
{
    /// <summary>
    /// Snapshot of an inspected node (mirrors the RDT SPI NodeState contract):
    /// identity, hierarchy activity, screen position and a whitelisted
    /// per-component slice of field/property values.
    /// </summary>
    public sealed class NodeState
    {
        public string TestId { get; set; }

        public Handle Handle { get; set; }

        public string Name { get; set; }

        public bool ActiveInHierarchy { get; set; }

        public Vector2? ScreenPosition { get; set; }

        public Dictionary<string, Dictionary<string, object>> Components { get; set; }
    }
}
