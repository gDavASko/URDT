using System;
using KBP.CORE;
using UnityEngine;
using UnityEngine.Scripting;

namespace KBP.CORE.MODULES
{
    /// <summary>
    /// Custom ModuleOutput data carrier to return gameplay combat results to subsequent nodes.
    /// </summary>
    [Serializable]
    [Preserve]
    public sealed class GameRewardOutput : ModuleOutput
    {
        [SerializeField, PortId("gold_earned"), PortDisplayName("Gold Earned"), PortDescription("Total amount of gold rewarded from this combat.")]
        public int GoldEarned;

        [SerializeField, PortId("is_victory"), PortDisplayName("Victory"), PortDescription("Was the encounter completed successfully?")]
        public bool IsVictory;
    }
}
