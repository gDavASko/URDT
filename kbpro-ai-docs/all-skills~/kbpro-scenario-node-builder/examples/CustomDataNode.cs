using System;
using KBP.CORE;
using UnityEngine;
using UnityEngine.Scripting;

namespace KBP.CORE.MODULES
{
    /// <summary>
    /// Example of a custom data-only ScenarioGraphNode that acts as a Data Source in ScenarioGraph data-flow.
    /// </summary>
    [Serializable]
    [Preserve]
    [ScenarioNodeMetadata(
        displayName: "Custom Data",
        description: "Example custom data node",
        icon: "D",
        menuPath: "Examples/Custom Data")]
    public sealed class CustomDataNode : ScenarioGraphNode, IDataSourceNode, IDataOnlyNode
    {
        [SerializeField, ConstSelector("Params")] private string _paramId = string.Empty;

        public object ResolvePort(string portId, Type expectedType, IDataSourceContext ctx)
        {
            if (ctx.SharedContext.TryGet(_paramId, out object val))
            {
                if (val != null && expectedType.IsInstanceOfType(val))
                    return val;
            }
            return null;
        }
    }
}
