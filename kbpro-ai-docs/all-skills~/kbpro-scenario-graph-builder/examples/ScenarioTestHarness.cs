using System.Collections.Generic;
using KBP.CORE;
using KBP.CORE.MODULES;
using UnityEngine;
using NUnit.Framework;

namespace KBP.CORE.MODULES.TESTS
{
    /// <summary>
    /// Programmatic Test Harness demonstrating in-memory setup and simulation of a ScenarioGraph.
    /// Used for TDD validation of modular transitions.
    /// </summary>
    public sealed class ScenarioTestHarness
    {
        public ScenarioGraph CreateTestGraph()
        {
            ScenarioGraph graph = ScriptableObject.CreateInstance<ScenarioGraph>();

            // 1. Create nodes via constructors (NodeId is read-only, set through ctor)
            EntryScenarioNode entryNode = new EntryScenarioNode(
                "entry_node", Vector2.zero);

            ModuleScenarioNode module1Node = new ModuleScenarioNode(
                "combat_module", new Vector2(300f, 0f), moduleRef: null);

            ExitScenarioNode exitNode = new ExitScenarioNode(
                "exit_node", new Vector2(600f, 0f), ScriptExitReason.Victory);

            ScenarioGraphNode[] nodes = new ScenarioGraphNode[]
            {
                entryNode,
                module1Node,
                exitNode
            };

            // 2. Setup transitions
            // Constructor: (fromNodeId, toNodeId, conditionId, sourceAction, condition)
            ScenarioTransitionData entryToCombat = new ScenarioTransitionData(
                fromNodeId: "entry_node",
                toNodeId: "combat_module",
                conditionId: AlwaysModuleTransitionCondition.ON_COMPLETE,
                sourceAction: SourceNodeAction.Destroy,
                condition: new AlwaysModuleTransitionCondition()
            );

            ScenarioTransitionData combatToExit = new ScenarioTransitionData(
                fromNodeId: "combat_module",
                toNodeId: "exit_node",
                conditionId: AlwaysModuleTransitionCondition.ON_COMPLETE,
                sourceAction: SourceNodeAction.Destroy,
                condition: new AlwaysModuleTransitionCondition()
            );

            ScenarioTransitionData[] transitions = new ScenarioTransitionData[]
            {
                entryToCombat,
                combatToExit
            };

            // 3. Configure graph
            graph.ConfigureRuntime(
                scriptId: "combat_flow_test",
                finalTransitionId: "exit_node",
                nodes: nodes,
                transitions: transitions
            );

            return graph;
        }
    }
}
