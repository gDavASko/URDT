using System;
using KBP.CORE;
using KBP.CORE.MODULES;
using UnityEngine;

namespace KBP.CORE.MODULES.TESTS
{
    /// <summary>
    /// Demonstrates programmatic wiring of node transitions and data-links.
    /// Useful for test harnesses and procedural graphs.
    /// </summary>
    public static class LinkWiringExample
    {
        public static void SetupLinksAndTransitions(ScenarioGraph graph)
        {
            // 1. Setup Exec Transition from 'node_a' to 'node_b'
            // Constructor: (fromNodeId, toNodeId, conditionId, sourceAction, condition)
            ScenarioTransitionData transition = new ScenarioTransitionData(
                fromNodeId: "node_a",
                toNodeId: "node_b",
                conditionId: AlwaysModuleTransitionCondition.ON_COMPLETE,
                sourceAction: SourceNodeAction.SuspendHidden,
                condition: new AlwaysModuleTransitionCondition()
            );

            graph.SetTransitions(new[] { transition });

            // 2. Setup Data-Link passing time from 'node_a' output to 'node_b' input
            // Constructor: (fromNodeId, fromPortId, toNodeId, toPortId, mode)
            ScenarioDataLink link = new ScenarioDataLink(
                fromNodeId: "node_a",
                fromPortId: "final_time",
                toNodeId: "node_b",
                toPortId: "time_limit",
                mode: LinkRefreshMode.OnEveryEntry
            );

            graph.SetDataLinks(new[] { link });
        }
    }
}
