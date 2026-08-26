using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KBP.URDT.Timing
{
    /// <summary>
    /// Deterministic/realtime time control (invariant I4, 03-determinism-time-control.md).
    /// In deterministic mode the game is frozen at rest (<c>Time.timeScale = 0</c>),
    /// Input System events are pumped manually (<c>ProcessEventsManually</c>), physics
    /// is scripted (<c>SimulationMode.Script</c>), and RNG is seeded — so a scenario is
    /// advanced only by explicit <see cref="StepFrame"/> calls and reproduces run-to-run
    /// independently of host FPS. The prior engine state is restored on exit.
    /// <para>
    /// Full single-loop frame control is only reliable inside a PlayMode harness that
    /// owns the yield loop; in a standalone player it is a documented approximation
    /// (03 §2.3). Time-mode-only commands throw <see cref="NotSupportedException"/>
    /// (protocol error E_TIME_MODE) when the mode does not match.
    /// </para>
    /// </summary>
    public sealed class TimeController : IDisposable
    {
        private const float MS_TO_SECONDS = 1f / 1000f;

        private TimeMode _mode = TimeMode.Realtime;
        private bool _deterministicActive;
        private bool _manualInputPump;
        private bool _inputModeSaved;

        private float _savedTimeScale;
        private float _savedFixedDelta;
        private float _savedMaxDelta;
        private SimulationMode _savedPhysicsMode;
        private SimulationMode2D _savedPhysics2DMode;
        private InputSettings.UpdateMode _savedInputUpdateMode;

        public TimeMode Mode
        {
            get { return _mode; }
        }

        /// <summary>
        /// Enters deterministic mode with manual input pumping (standalone/CI-faithful,
        /// see 03-determinism-time-control.md §2.5). Equivalent to
        /// <c>EnterDeterministic(seed, manualInputPump: true)</c>.
        /// </summary>
        public void EnterDeterministic(int seed)
        {
            EnterDeterministic(seed, true);
        }

        /// <summary>
        /// Enters deterministic mode and seeds RNG. Saves the previous engine time,
        /// physics and (optionally) Input System update state so it can be restored on exit.
        /// <para>
        /// When <paramref name="manualInputPump"/> is true, the Input System is switched to
        /// <c>ProcessEventsManually</c> and virtual-device events are pumped by
        /// <see cref="StepFrame"/> (§2.5) — the standalone/CI path. When false, the engine
        /// keeps driving input per frame: this is the PlayMode-harness path where the test's
        /// yield loop owns frame advancement (§2.3), and forcing manual input pumping would
        /// otherwise fight the running player loop.
        /// </para>
        /// </summary>
        public void EnterDeterministic(int seed, bool manualInputPump)
        {
            if (_deterministicActive)
            {
                UnityEngine.Random.InitState(seed);
                return;
            }

            _savedTimeScale = UnityEngine.Time.timeScale;
            _savedFixedDelta = UnityEngine.Time.fixedDeltaTime;
            _savedMaxDelta = UnityEngine.Time.maximumDeltaTime;
            _savedPhysicsMode = Physics.simulationMode;
            _savedPhysics2DMode = Physics2D.simulationMode;

            Physics.simulationMode = SimulationMode.Script;
            Physics2D.simulationMode = SimulationMode2D.Script;

            if (manualInputPump)
            {
                _savedInputUpdateMode = InputSystem.settings.updateMode;
                _inputModeSaved = true;
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            }

            UnityEngine.Time.timeScale = 0f;
            UnityEngine.Random.InitState(seed);

            _manualInputPump = manualInputPump;
            _mode = TimeMode.Deterministic;
            _deterministicActive = true;
        }

        /// <summary>
        /// Leaves deterministic mode and restores the previously saved engine state.
        /// </summary>
        public void ExitDeterministic()
        {
            if (!_deterministicActive)
            {
                return;
            }

            UnityEngine.Time.timeScale = _savedTimeScale;
            UnityEngine.Time.fixedDeltaTime = _savedFixedDelta;
            UnityEngine.Time.maximumDeltaTime = _savedMaxDelta;
            Physics.simulationMode = _savedPhysicsMode;
            Physics2D.simulationMode = _savedPhysics2DMode;

            if (_inputModeSaved)
            {
                InputSystem.settings.updateMode = _savedInputUpdateMode;
                _inputModeSaved = false;
            }

            _manualInputPump = false;
            _mode = TimeMode.Realtime;
            _deterministicActive = false;
        }

        /// <summary>
        /// Pins <c>Time.fixedDeltaTime</c>/<c>Time.maximumDeltaTime</c> so physics stepping
        /// does not depend on host FPS. Deterministic mode only.
        /// </summary>
        public void PinFixedDelta(float fixedDeltaMs, float maxDeltaMs)
        {
            EnsureDeterministic();
            UnityEngine.Time.fixedDeltaTime = fixedDeltaMs * MS_TO_SECONDS;
            UnityEngine.Time.maximumDeltaTime = maxDeltaMs * MS_TO_SECONDS;
        }

        /// <summary>
        /// Sets <c>Time.timeScale</c> (pause/accelerate). Allowed in both modes.
        /// </summary>
        public void SetTimeScale(float scale)
        {
            UnityEngine.Time.timeScale = scale;
        }

        /// <summary>
        /// Advances the given number of frames with a fixed delta. Each step pumps the
        /// Input System once and simulates 3D and 2D physics with the pinned delta.
        /// Deterministic mode only (else E_TIME_MODE).
        /// </summary>
        public void StepFrame(int frames, float deltaMs)
        {
            EnsureDeterministic();

            float deltaSeconds = deltaMs * MS_TO_SECONDS;
            for (int i = 0; i < frames; i++)
            {
                if (_manualInputPump)
                {
                    InputSystem.Update();
                }

                Physics.Simulate(deltaSeconds);
                Physics2D.Simulate(deltaSeconds);
            }
        }

        public void Dispose()
        {
            ExitDeterministic();
        }

        private void EnsureDeterministic()
        {
            if (_mode != TimeMode.Deterministic)
            {
                throw new NotSupportedException(
                    "E_TIME_MODE: command requires deterministic time mode.");
            }
        }
    }
}
