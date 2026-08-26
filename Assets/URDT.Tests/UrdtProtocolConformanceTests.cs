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
    /// Protocol-conformance coverage for the commands/features added to complete URDT:
    /// set_time_scale, pin_fixed_delta, unsubscribe, custom:* routing, subscription
    /// filtering, log_error events, and the services-reset hook (reset_state depth:"services").
    /// </summary>
    public sealed class UrdtProtocolConformanceTests : InputTestFixture
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
            _runtime = new UrdtRuntime(_driver, _registry, inspector, _time, _scenes, _diagnostics);
            _registry.AddRule(new SelectorRule(new HasComponentPredicate(nameof(UrdtRegistryMarker)), "marker"));
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

        private Response RouteRaw(string action, JObject payload)
        {
            return _router.Route(new Command(action + "-id", action, payload));
        }

        private JObject Route(string action, JObject payload)
        {
            Response response = RouteRaw(action, payload);
            Assert.IsTrue(response.Ok, action + " failed: " + response.ErrorCode + " " + response.ErrorMessage);
            return (JObject)response.Data;
        }

        [Test]
        public void SetTimeScale_AppliesAndEchoes()
        {
            JObject data = Route(ProtocolConstants.ACTION_SET_TIME_SCALE, new JObject { ["scale"] = 0f });
            Assert.AreEqual(0f, (float)data["time_scale"]);
            Assert.AreEqual(0f, UnityEngine.Time.timeScale);
            UnityEngine.Time.timeScale = 1f;
        }

        [Test]
        public void PinFixedDelta_RequiresDeterministicMode()
        {
            Response realtime = RouteRaw(ProtocolConstants.ACTION_PIN_FIXED_DELTA, new JObject { ["fixed_delta_ms"] = 20f });
            Assert.IsFalse(realtime.Ok);
            Assert.AreEqual(ErrorCodes.E_TIME_MODE, realtime.ErrorCode);

            _time.EnterDeterministic(1);
            JObject data = Route(ProtocolConstants.ACTION_PIN_FIXED_DELTA, new JObject { ["fixed_delta_ms"] = 25f });
            Assert.AreEqual(25f, (float)data["fixed_delta_ms"]);
            _time.ExitDeterministic();
        }

        [Test]
        public void Subscribe_UnknownEvent_ReturnsUnknownEventWithDetails()
        {
            Response response = RouteRaw(ProtocolConstants.ACTION_SUBSCRIBE, new JObject
            {
                ["events"] = new JArray { "not_a_real_event" }
            });

            Assert.IsFalse(response.Ok);
            Assert.AreEqual(ErrorCodes.E_UNKNOWN_EVENT, response.ErrorCode);
            JObject details = response.ErrorDetails as JObject;
            Assert.IsNotNull(details);
            Assert.AreEqual("not_a_real_event", ((JArray)details["unknown"])[0].ToString());
        }

        [Test]
        public void Unsubscribe_RemovesSubscription()
        {
            Route(ProtocolConstants.ACTION_SUBSCRIBE, new JObject { ["events"] = new JArray { ProtocolConstants.EVENT_SCENE_LOADED } });
            Assert.IsTrue(_runtime.Subscriptions.IsSubscribed(ProtocolConstants.EVENT_SCENE_LOADED));

            JObject data = Route(ProtocolConstants.ACTION_UNSUBSCRIBE, new JObject { ["events"] = new JArray { ProtocolConstants.EVENT_SCENE_LOADED } });
            Assert.AreEqual(1, ((JArray)data["unsubscribed"]).Count);
            Assert.IsFalse(_runtime.Subscriptions.IsSubscribed(ProtocolConstants.EVENT_SCENE_LOADED));
        }

        [Test]
        public void CustomCommand_Unregistered_ReturnsUnsupported()
        {
            Response response = RouteRaw("custom:open_level", new JObject { ["level"] = 3 });
            Assert.IsFalse(response.Ok);
            Assert.AreEqual(ErrorCodes.E_UNSUPPORTED, response.ErrorCode);
        }

        [Test]
        public void EventPublisher_FiltersBySubscription()
        {
            SubscriptionSet subscriptions = new SubscriptionSet();
            List<ResponseEnvelope> pushed = new List<ResponseEnvelope>();

            using (new UrdtEventPublisher(_registry, _scenes, _diagnostics, subscriptions, env => pushed.Add(env)))
            {
                // Not subscribed → no push.
                GameObject a = new GameObject("A");
                a.AddComponent<UrdtRegistryMarker>();
                _spawned.Add(a);
                _registry.Register(a, RegistrationSource.Incremental);
                Assert.AreEqual(0, pushed.Count, "Unsubscribed events must not be pushed.");

                // Subscribe → push.
                subscriptions.Add(ProtocolConstants.EVENT_OBJECT_REGISTERED);
                GameObject b = new GameObject("B");
                b.AddComponent<UrdtRegistryMarker>();
                _spawned.Add(b);
                _registry.Register(b, RegistrationSource.Incremental);
                Assert.AreEqual(1, pushed.Count, "Subscribed events must be pushed.");
                Assert.AreEqual(ProtocolConstants.EVENT_OBJECT_REGISTERED, pushed[0].EventName);
            }
        }

        [Test]
        public void ResetState_ServicesDepth_WithoutHook_IsUnsupported()
        {
            // No services-reset hook registered → depth:services is rejected (E_UNSUPPORTED at the
            // handler; false at the manager) WITHOUT any destructive scene reload.
            Assert.IsFalse(_scenes.ResetScene(SceneLifecycleManager.DEPTH_SERVICES, null));

            Response response = RouteRaw(ProtocolConstants.ACTION_RESET_STATE, new JObject { ["depth"] = "services" });
            Assert.IsFalse(response.Ok);
            Assert.AreEqual(ErrorCodes.E_UNSUPPORTED, response.ErrorCode);
        }
    }
}
