using System;
using KBP.CORE;
using KBP.CORE.MODULES;
using UnityEngine;

namespace KBP.GAMEPLAY
{
    // Minimal mock inputs/outputs for the compilable template.
    [Serializable]
    public sealed class RaceInput : ModuleInput
    {
        public float Speed = 5f;
    }

    [Serializable]
    public sealed class RaceOutput : ModuleOutput
    {
        public float TimeTaken;
    }

    /// <summary>
    /// Demonstrates full setup of a gameplay module integrated with the ScenarioGraph Module I/O framework.
    /// Handles parameters initialization, caching, Suspend/Resume lifecycles, and outputs flushing.
    /// </summary>
    [ModuleIO(typeof(RaceInput), typeof(RaceOutput))]
    public sealed class RaceModule : AbstractGameModule, IInputUpdateAware, ISuspendable
    {
        private float _speed;
        private bool _isSuspended;

        public override void Initialize()
        {
            SyncInput();
            base.Initialize(); // Always call base last in Initialize
        }

        public void OnInputUpdated()
        {
            SyncInput();
        }

        private void SyncInput()
        {
            var input = RuntimeContext.ModuleInput as RaceInput;
            if (input != null)
            {
                _speed = input.Speed;
            }
        }

        public bool IsSuspended => _isSuspended;

        public void Suspend(bool hide = true)
        {
            _isSuspended = true;
            // Place logic here to pause local timers, stop tweens, or hide UI
        }

        public void Resume()
        {
            _isSuspended = false;
            // Place logic here to resume combat loops and reactive bindings
        }

        public void FinishRace(float elapsedTime)
        {
            var output = RuntimeContext.ModuleOutput as RaceOutput;
            if (output != null)
            {
                output.TimeTaken = elapsedTime;
            }

            // Fire completion which schedules data links evaluation and flushes outputs to snapshot store.
            OnComplete();
        }

        public override void Dispose()
        {
            // Unregister event bindings and cancel CancellationTokenSources
            base.Dispose();
        }
    }
}
