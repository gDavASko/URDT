using System;
using KBP.URDT.Diagnostics;
using KBP.URDT.Driver;
using KBP.URDT.Input;
using KBP.URDT.Inspect;
using KBP.URDT.Lifecycle;
using KBP.URDT.Registry;
using KBP.URDT.Timing;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Dependency holder shared by all command handlers — the Unity-side binding of the
    /// URDT SPI. Constructed once by the server host; handlers read from it on the main thread.
    /// </summary>
    public sealed class UrdtRuntime
    {
        public UrdtRuntime(
            UnityUrdtDriver driver,
            TestIdRegistry registry,
            StateInspector inspector,
            TimeController time,
            SceneLifecycleManager scenes,
            DiagnosticsCapture diagnostics,
            InputSimulator input = null)
        {
            Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
            Time = time;
            Scenes = scenes;
            Diagnostics = diagnostics;
            Input = input;
        }

        public UnityUrdtDriver Driver { get; }

        public TestIdRegistry Registry { get; }

        public StateInspector Inspector { get; }

        public TimeController Time { get; }

        public SceneLifecycleManager Scenes { get; }

        public DiagnosticsCapture Diagnostics { get; }

        public InputSimulator Input { get; }

        /// <summary>Event-subscription state shared with the event publisher.</summary>
        public SubscriptionSet Subscriptions { get; } = new SubscriptionSet();
    }
}
