using System;

namespace URDT.Runtime.SaveState
{
    /// <summary>
    /// Contract for stateful game entities, singletons, and DI controllers that participate
    /// in the 4-phase atomic save-state and rollback protocol for autonomous testing.
    /// </summary>
    public interface IUrdtSaveable
    {
        /// <summary>
        /// Unique deterministic identifier for the saveable entity (e.g., "inventory_manager", "player_transform").
        /// </summary>
        string SaveStateKey { get; }

        /// <summary>
        /// Captures the complete operational state into an immutable binary payload.
        /// </summary>
        byte[] CaptureState();

        /// <summary>
        /// Restores entity state from a previously captured binary payload.
        /// </summary>
        void RestoreState(byte[] stateData);

        /// <summary>
        /// Resets the entity to its initial baseline state.
        /// </summary>
        void ResetToDefault();
    }
}
