using KBP.URDT.Diagnostics;
using KBP.URDT.Driver;
using KBP.URDT.Handlers;
using KBP.URDT.Input;
using KBP.URDT.Inspect;
using KBP.URDT.Lifecycle;
using KBP.URDT.Net;
using KBP.URDT.Registry;
using KBP.URDT.Timing;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// Covers semantic input commands required by the URDT TestPoligon matrix.
    /// </summary>
    public sealed class UrdtGestureCommandTests : InputTestFixture
    {
        private InputSimulator _input;
        private TimeController _time;
        private TestIdRegistry _registry;
        private SceneLifecycleManager _scenes;
        private DiagnosticsCapture _diagnostics;
        private UnityUrdtDriver _driver;
        private UrdtRuntime _runtime;
        private CommandRouter _router;

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
            _router = new CommandRouter();
            UrdtCommandRegistrar.RegisterAll(_router, _runtime);
        }

        public override void TearDown()
        {
            _driver?.Dispose();
            _diagnostics?.Dispose();
            _scenes?.Dispose();
            _registry?.Dispose();
            _time?.Dispose();
            _input?.Dispose();
            base.TearDown();
        }

        [Test]
        public void Registrar_RoutesSemanticInputCommands()
        {
            AssertRouteOk(ProtocolConstants.ACTION_DOUBLE_CLICK, PointPayload());
            AssertRouteOk(ProtocolConstants.ACTION_PRESS_MOVE, PathPayload());
            AssertRouteOk(ProtocolConstants.ACTION_SWIPE, SwipePayload());
            AssertRouteOk(ProtocolConstants.ACTION_SCROLL, ScrollPayload());
            AssertRouteOk(ProtocolConstants.ACTION_MULTI_CLICK, MultiClickPayload());
            AssertRouteOk(ProtocolConstants.ACTION_MULTI_DRAG, MultiDragPayload());
            AssertRouteOk(ProtocolConstants.ACTION_PINCH, PinchPayload());
        }

        private void AssertRouteOk(string action, JObject payload)
        {
            Response response = _router.Route(new Command(action + "-id", action, payload));
            Assert.IsTrue(response.Ok, action + " failed: " + response.ErrorCode + " " + response.ErrorMessage);
        }

        private static JObject PointPayload()
        {
            return new JObject
            {
                ["x"] = 120f,
                ["y"] = 160f
            };
        }

        private static JObject PathPayload()
        {
            return new JObject
            {
                ["from"] = new JObject { ["x"] = 100f, ["y"] = 100f },
                ["to"] = new JObject { ["x"] = 240f, ["y"] = 140f },
                ["steps"] = 2
            };
        }

        private static JObject SwipePayload()
        {
            return new JObject
            {
                ["x"] = 120f,
                ["y"] = 160f,
                ["direction"] = "right",
                ["distance"] = 80f,
                ["steps"] = 2
            };
        }

        private static JObject ScrollPayload()
        {
            return new JObject
            {
                ["x"] = 120f,
                ["y"] = 160f,
                ["delta"] = -90f
            };
        }

        private static JObject MultiClickPayload()
        {
            return new JObject
            {
                ["points"] = new JArray
                {
                    new JObject { ["x"] = 100f, ["y"] = 100f },
                    new JObject { ["x"] = 200f, ["y"] = 100f }
                }
            };
        }

        private static JObject MultiDragPayload()
        {
            return new JObject
            {
                ["paths"] = new JArray
                {
                    new JObject
                    {
                        ["path"] = new JArray
                        {
                            new JObject { ["x"] = 100f, ["y"] = 100f },
                            new JObject { ["x"] = 120f, ["y"] = 140f }
                        }
                    },
                    new JObject
                    {
                        ["path"] = new JArray
                        {
                            new JObject { ["x"] = 200f, ["y"] = 100f },
                            new JObject { ["x"] = 220f, ["y"] = 140f }
                        }
                    }
                },
                ["steps"] = 2
            };
        }

        private static JObject PinchPayload()
        {
            return new JObject
            {
                ["x"] = 180f,
                ["y"] = 160f,
                ["from_distance"] = 40f,
                ["to_distance"] = 120f,
                ["steps"] = 2
            };
        }
    }
}
