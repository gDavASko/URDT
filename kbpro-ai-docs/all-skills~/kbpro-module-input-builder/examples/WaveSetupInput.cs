using System;
using System.Collections.Generic;
using KBP.CORE;
using UnityEngine;
using UnityEngine.Scripting;

namespace KBP.CORE.MODULES
{
    /// <summary>
    /// Custom ModuleInput template containing mutable collections (demands Deep Copy Clone override).
    /// </summary>
    [Serializable]
    [Preserve]
    public sealed class WaveSetupInput : ModuleInput
    {
        [SerializeField, PortId("wave_ids"), PortDisplayName("Wave IDs"), PortDescription("Ordered list of encounter waves.")]
        public List<string> WaveIds = new();

        /// <summary>
        /// Explicit Deep Copy implementation to prevent template contamination.
        /// </summary>
        public override ModuleInput Clone()
        {
            return new WaveSetupInput
            {
                WaveIds = WaveIds != null ? new List<string>(WaveIds) : null
            };
        }
    }
}
