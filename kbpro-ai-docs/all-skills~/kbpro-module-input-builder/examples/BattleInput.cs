using System;
using KBP.CORE;
using UnityEngine;
using UnityEngine.Scripting;

namespace KBP.CORE.MODULES
{
    /// <summary>
    /// Custom ModuleInput template carrying simple values (safe for Shallow Copy).
    /// </summary>
    [Serializable]
    [Preserve]
    public sealed class BattleInput : ModuleInput
    {
        [SerializeField, PortId("enemy_count"), PortDisplayName("Enemy Count"), PortDescription("Number of enemies to spawn in this encounter.")]
        public int EnemyCount = 3;

        [SerializeField, PortId("difficulty"), PortDisplayName("Difficulty"), PortDescription("Multiplier for enemy health and attack speed.")]
        public float DifficultyMultiplier = 1.0f;
    }
}
