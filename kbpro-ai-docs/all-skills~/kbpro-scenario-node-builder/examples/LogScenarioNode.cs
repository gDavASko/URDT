using System;
using KBP.CORE;
using UnityEngine;
using UnityEngine.Scripting;

namespace KBP.CORE.MODULES
{
    /// <summary>
    /// Example of a custom visual ScenarioGraphNode that triggers a debug log at runtime.
    /// </summary>
    [Serializable]
    [Preserve]
    [ScenarioNodeMetadata(
        displayName: "Log Scenario Message",
        description: "Triggers debug log at runtime",
        icon: "L",
        menuPath: "Examples/Log Scenario")]
    public sealed class LogScenarioNode : ScenarioGraphNode
    {
        [SerializeField] private string _message = "Default Scenario Message";
        [SerializeField] private LogType _logType = LogType.Log;

        public string Message => _message;
        public LogType LogType => _logType;
    }
}
