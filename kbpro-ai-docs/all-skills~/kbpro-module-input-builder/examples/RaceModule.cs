using System;
using KBP.CORE;
using KBP.CORE.MODULES;
using UnityEngine;

namespace KBP.GAMEPLAY
{
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

    [ModuleIO(typeof(RaceInput), typeof(RaceOutput))]
    public sealed class RaceModule : AbstractGameModule, IInputUpdateAware, ISuspendable
    {
        private float _speed;
        private bool _isSuspended;

        public override void Initialize()
        {
            SyncInput();
            base.Initialize();
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
        }

        public void Resume()
        {
            _isSuspended = false;
        }

        public void FinishRace(float elapsedTime)
        {
            var output = RuntimeContext.ModuleOutput as RaceOutput;
            if (output != null)
            {
                output.TimeTaken = elapsedTime;
            }
            OnComplete();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
