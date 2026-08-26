using System.Collections.Generic;
using KBP.URDT.Diagnostics;
using KBP.URDT.Driver;
using KBP.URDT.Handlers;
using KBP.URDT.Input;
using KBP.URDT.Inspect;
using KBP.URDT.Lifecycle;
using KBP.URDT.Net;
using KBP.URDT.Registry;
using KBP.URDT.Tests.Stand;
using KBP.URDT.Timing;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// M5.3 command handlers routed through the CommandRouter and bound to the URDT runtime:
    /// inspect/query, click/drag, step_frame time-mode gating, reset_state depth, capture
    /// (diagnostic-only), subscribe, wait_for, and server-push event bridging.
    /// </summary>
    public sealed class UrdtCommandHandlerTests : InputTestFixture
    {
        private InputSimulator _input;
        private TimeController _time;
        private TestIdRegistry _registry;
        private SceneLifecycleManager _scenes;
        private DiagnosticsCapture _diagnostics;
        private UnityUrdtDriver _driver;
        private UrdtRuntime _runtime;
        private CommandRouter _router;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();

            _input = new InputSimulator();
            _time = new TimeController();
            _registry = new TestIdRegistry();
            _scenes = new SceneLifecycleManager();
            _diagnostics = new DiagnosticsCapture();
            StateInspector inspector = new StateInspector();
            _driver = new UnityUrdtDriver(_input, inspector, _time, _registry);
            _runtime = new UrdtRuntime(_driver, _registry, inspector, _time, _scenes, _diagnostics, _input);

            _registry.AddRule(new SelectorRule(new HasComponentPredicate(nameof(UrdtRegistryMarker)), "marker"));
            _registry.AddRule(new SelectorRule(new HasComponentPredicate(nameof(UrdtClickTarget)), "clicker"));

            _router = new CommandRouter();
            UrdtCommandRegistrar.RegisterAll(_router, _runtime);
        }

        public override void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();
            _driver?.Dispose();
            _diagnostics?.Dispose();
            _scenes?.Dispose();
            _registry?.Dispose();
            _time?.Dispose();
            _input?.Dispose();

            base.TearDown();
        }

        private JObject Route(string action, JObject payload)
        {
            Response response = _router.Route(new Command(action + "-id", action, payload));
            Assert.IsTrue(response.Ok, action + " must succeed: " + response.ErrorCode + " " + response.ErrorMessage);
            return (JObject)response.Data;
        }

        private Response RouteRaw(string action, JObject payload)
        {
            return _router.Route(new Command(action + "-id", action, payload));
        }

        private GameObject Register(string name, System.Type component)
        {
            GameObject go = new GameObject(name);
            go.AddComponent(component);
            _spawned.Add(go);
            _registry.Register(go, RegistrationSource.Incremental);
            return go;
        }

        [Test]
        public void Inspect_ReturnsNodeSliceForTarget()
        {
            Register("Marked", typeof(UrdtRegistryMarker));

            JObject data = Route("inspect", new JObject { ["testId"] = "marker" });
            Assert.AreEqual("marker", data["testId"].ToString());
            Assert.IsTrue((bool)data["activeInHierarchy"]);
        }

        [Test]
        public void Inspect_UnknownTarget_ReturnsNotFound()
        {
            Response response = RouteRaw("inspect", new JObject { ["testId"] = "ghost" });
            Assert.IsFalse(response.Ok);
            Assert.AreEqual(ErrorCodes.E_NOT_FOUND, response.ErrorCode);
        }

        [Test]
        public void Query_ByComponent_ReturnsAllMatches()
        {
            Register("A", typeof(UrdtRegistryMarker));
            Register("B", typeof(UrdtRegistryMarker));

            JObject data = Route("query", new JObject { ["byComponent"] = nameof(UrdtRegistryMarker) });
            Assert.AreEqual(2, ((JArray)data["matches"]).Count);
        }

        [Test]
        public void Query_SelectorObject_ByComponent_ReturnsAllMatches()
        {
            Register("A", typeof(UrdtRegistryMarker));
            Register("B", typeof(UrdtRegistryMarker));

            JObject data = Route("query", new JObject
            {
                ["selector"] = new JObject { ["byComponent"] = nameof(UrdtRegistryMarker) }
            });

            Assert.AreEqual(2, ((JArray)data["matches"]).Count);
        }

        [Test]
        public void Click_ByPoint_ReturnsClickedAtResolvedPoint()
        {
            JObject data = Route("click", new JObject { ["x"] = 250f, ["y"] = 200f });
            Assert.IsTrue((bool)data["clicked"]);
            Assert.AreEqual(250f, (float)data["screenPosition"]["x"]);
            Assert.AreEqual(3, (int)data["queued_frames"]);
        }

        [Test]
        public void Drag_ByPoints_ReturnsDragged()
        {
            JObject payload = new JObject
            {
                ["from"] = new JObject { ["x"] = 100f, ["y"] = 100f },
                ["to"] = new JObject { ["x"] = 300f, ["y"] = 250f },
                ["steps"] = 5
            };
            JObject data = Route("drag", payload);
            Assert.IsTrue((bool)data["dragged"]);
            Assert.AreEqual(5, (int)data["points"]);
            Assert.AreEqual(8, (int)data["queued_frames"]);
        }

        [Test]
        public void Drag_PathPoints_ReturnsDragged()
        {
            JObject payload = new JObject
            {
                ["path"] = new JArray
                {
                    new JObject { ["x"] = 100f, ["y"] = 100f },
                    new JObject { ["x"] = 200f, ["y"] = 150f },
                    new JObject { ["x"] = 300f, ["y"] = 250f }
                },
                ["steps"] = 2
            };

            JObject data = Route("drag", payload);
            Assert.IsTrue((bool)data["dragged"]);
            Assert.AreEqual(2, (int)data["points"]);
            Assert.AreEqual(7, (int)data["queued_frames"]);
        }

        [Test]
        public void StepFrame_RealtimeRejected_DeterministicAccepted()
        {
            Response realtime = RouteRaw("step_frame", new JObject { ["frames"] = 2 });
            Assert.IsFalse(realtime.Ok);
            Assert.AreEqual(ErrorCodes.E_TIME_MODE, realtime.ErrorCode);

            _time.EnterDeterministic(123);
            JObject data = Route("step_frame", new JObject { ["frames"] = 3 });
            Assert.AreEqual(3, (int)data["advanced"]);
            _time.ExitDeterministic();
        }

        [Test]
        public void ResetState_UnsupportedDepth_ReturnsUnsupported()
        {
            Response response = RouteRaw("reset_state", new JObject { ["depth"] = "services" });
            Assert.IsFalse(response.Ok);
            Assert.AreEqual(ErrorCodes.E_UNSUPPORTED, response.ErrorCode);
        }

        [Test]
        public void Capture_ReturnsNonAuthoritativeDiagnostics()
        {
            JObject data = Route("capture", new JObject { ["screenshot"] = false, ["log_tail"] = 5 });
            Assert.IsFalse((bool)data["authoritative"]);
            Assert.IsNotNull(data["logs"]);
        }

        [Test]
        public void Subscribe_AcknowledgesRequestedEvents()
        {
            JObject data = Route("subscribe", new JObject
            {
                ["events"] = new JArray { ProtocolConstants.EVENT_OBJECT_REGISTERED }
            });
            Assert.AreEqual(1, ((JArray)data["subscribed"]).Count);
            Assert.AreEqual(ProtocolConstants.EVENT_OBJECT_REGISTERED, data["subscribed"][0].ToString());
        }

        [Test]
        public void WaitFor_EvaluatesFieldCondition()
        {
            Register("Clicker", typeof(UrdtClickTarget));

            JObject data = Route("wait_for", new JObject
            {
                ["testId"] = "clicker",
                ["condition"] = new JObject
                {
                    ["path"] = nameof(UrdtClickTarget) + ".ClickCount",
                    ["equals"] = "0"
                }
            });
            Assert.IsTrue((bool)data["satisfied"]);
        }

        [Test]
        public void EventPublisher_PushesEvent_OnObjectRegistration()
        {
            List<ResponseEnvelope> pushed = new List<ResponseEnvelope>();
            using (new UrdtEventPublisher(_registry, _scenes, null, null, env => pushed.Add(env)))
            {
                Register("Watched", typeof(UrdtRegistryMarker));
            }

            Assert.GreaterOrEqual(pushed.Count, 1, "A server-push event must be emitted on registration.");
            ResponseEnvelope evt = pushed[0];
            Assert.AreEqual(ProtocolConstants.TYPE_EVENT, evt.Type);
            Assert.AreEqual(ProtocolConstants.EVENT_OBJECT_REGISTERED, evt.EventName);
            Assert.AreEqual("marker", evt.Data["testId"].ToString());
        }
    }
}
