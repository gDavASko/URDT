using System;
using System.Collections.Generic;
using KBP.CORE;
using UnityEngine;
using UnityEngine.Scripting;

namespace KBP.CORE.MODULES
{
    [Serializable]
    [Preserve]
    public sealed class WaveSetupInput : ModuleInput
    {
        [SerializeField, PortId("wave_ids"), PortDisplayName("Wave IDs"), PortDescription("Ordered list of encounter waves.")]
        public List<string> WaveIds = new();

        public override ModuleInput Clone()
        {
            return new WaveSetupInput
            {
                WaveIds = WaveIds != null ? new List<string>(WaveIds) : null
            };
        }
    }
}
