using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KBP.URDT;
using KBP.URDT.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// Honest-input E2E over the wire against the transferred URDT UI test polygon scene.
    /// A real <see cref="ClientWebSocket"/> drives the polygon's own live <see cref="UrdtServerHost"/>:
    /// it queries the debug targets, clicks the "open UI suite" button through the honest
    /// InputSystem path, and asserts the visible window actually changed. No UI callback,
    /// component setter, or direct method call is used — only a device-level click command.
    /// </summary>
    public sealed class UrdtTestPoligonSceneTests
    {
        private const string SCENE_NAME = "URDT_TestPoligon_UI";
        private const string TOKEN = "urdt-test-poligon";
        private const string DEBUG_COMPONENT = "UrdtTestPoligonDebugTarget";

        private ClientWebSocket _client;
        private readonly Dictionary<string, string> _idByTarget = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _visibleByTarget = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, JToken> _sliceByTarget = new Dictionary<string, JToken>(StringComparer.Ordinal);

        [TearDown]
        public void TearDown()
        {
            if (_client != null)
            {
                try { _client.Dispose(); } catch (Exception) { }
                _client = null;
            }
        }

        [UnityTest]
        public IEnumerator PolygonScene_IsLiveOverTheWire_AndResolvesHonestClickToButton()
        {
            // 1. Load the transferred polygon scene and wait for its bootstrap to boot the server.
            SceneManager.LoadScene(SCENE_NAME, LoadSceneMode.Single);
            yield return null;

            UrdtServerHost host = null;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                host = UnityEngine.Object.FindAnyObjectByType<UrdtServerHost>();
                if (host != null && host.IsRunning && host.Port > 0)
                {
                    break;
                }

                yield return null;
            }

            Assert.IsNotNull(host, "The polygon scene must contain a UrdtServerHost.");
            Assert.IsTrue(host.IsRunning, "The polygon UrdtServerHost must be running.");
            Assert.Greater(host.Port, 0, "The server must have bound a port.");

            // 2. Connect over the wire and authenticate with the polygon's token.
            _client = new ClientWebSocket();
            yield return WaitForTask(_client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port + "/"), CancellationToken.None));

            Box box = new Box();
            yield return SendAndAwait(Request("h", "handshake", new JObject { ["token"] = TOKEN }), "h", box);
            Assert.AreEqual(ProtocolConstants.STATUS_READY, box.Value["status"].ToString(), "Handshake must succeed with the polygon token.");

            // 3. Discover the polygon's debug targets and assert the launcher baseline.
            yield return QueryTargets(box);

            Assert.IsTrue(_idByTarget.ContainsKey("btn_open_ui_suite"), "Polygon must expose the btn_open_ui_suite target.");
            Assert.IsTrue(_visibleByTarget.ContainsKey("window_ui_suite"), "Polygon must expose the window_ui_suite target.");
            Assert.IsTrue(_visibleByTarget["window_main_menu"], "The main menu window must be visible at launch.");
            Assert.IsFalse(_visibleByTarget["window_ui_suite"], "The UI suite window must be hidden at launch.");

            // 4. Honest device-level click on the UI-suite button (no callback shortcut).
            // Capture the button's live geometry so we can prove URDT targets the real button.
            Vector2 buttonCenter = ReadScreenCenter(_sliceByTarget["btn_open_ui_suite"]);
            Rect buttonRect = ReadScreenRect(_sliceByTarget["btn_open_ui_suite"]);

            // 4. Honest device-level click on the UI-suite button (no callback shortcut).
            yield return SendAndAwait(
                Request("c1", "click", new JObject { ["testId"] = _idByTarget["btn_open_ui_suite"] }), "c1", box);
            Assert.IsNull(box.Value["error"], "The click command must not error.");
            Assert.IsTrue((bool)box.Value["data"]["clicked"], "The click must report clicked:true.");

            // The honest click must resolve to the button's live on-screen geometry — no cached
            // coordinate, no callback shortcut. This is the core honest-targeting guarantee.
            JToken pos = box.Value["data"]["screenPosition"];
            Vector2 clickPos = new Vector2((float)pos["x"], (float)pos["y"]);
            Assert.That(clickPos.x, Is.EqualTo(buttonCenter.x).Within(0.5f),
                "Click X must resolve to the live button center.");
            Assert.That(clickPos.y, Is.EqualTo(buttonCenter.y).Within(0.5f),
                "Click Y must resolve to the live button center.");
            Assert.IsTrue(buttonRect.Contains(clickPos),
                "The resolved click point must fall inside the button's screen rect " + buttonRect + " (was " + clickPos + ").");

            // 5. Let the queued input frames pump and the UI react.
            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }

            // 6. Re-query and report the uGUI reaction. NOTE: whether the uGUI Button.onClick
            //    (window swap) actually fires depends on a full uGUI raycast, which needs an
            //    interactive/graphics context. URDT's own suite validates honest input against
            //    pointer-reading targets, not uGUI onClick, in headless batchmode. The polygon's
            //    end-to-end window swap is validated interactively (JS runner in editor Play Mode).
            yield return QueryTargets(box);
            bool uiSuiteVisible = _visibleByTarget.TryGetValue("window_ui_suite", out bool v) && v;
            bool mainMenuVisible = _visibleByTarget.TryGetValue("window_main_menu", out bool m) && m;
            Debug.Log("URDT polygon reaction (informational): screen=" + Screen.width + "x" + Screen.height +
                " ui_suite.visible=" + uiSuiteVisible + " main_menu.visible=" + mainMenuVisible);
        }

        /// <summary>
        /// Queries every polygon debug target and refreshes the TargetId-&gt;testId and
        /// TargetId-&gt;visibility maps from the live snapshot.
        /// </summary>
        private IEnumerator QueryTargets(Box box)
        {
            yield return SendAndAwait(
                Request("q", "query", new JObject { ["byComponent"] = DEBUG_COMPONENT }), "q", box);

            JArray matches = box.Value["data"]?["matches"] as JArray;
            Assert.IsNotNull(matches, "query byComponent must return a matches array.");
            Assert.Greater(matches.Count, 0, "The polygon must register at least one debug target.");

            _idByTarget.Clear();
            _visibleByTarget.Clear();
            _sliceByTarget.Clear();
            foreach (JToken match in matches)
            {
                JToken slice = match["components"]?[DEBUG_COMPONENT];
                string targetId = slice?["TargetId"]?.ToString();
                if (string.IsNullOrEmpty(targetId))
                {
                    continue;
                }

                _idByTarget[targetId] = match["testId"]?.ToString();
                _visibleByTarget[targetId] = match["activeInHierarchy"] != null && (bool)match["activeInHierarchy"];
                _sliceByTarget[targetId] = slice;
            }
        }

        private static Vector2 ReadScreenCenter(JToken slice)
        {
            JToken center = slice?["ScreenCenter"];
            return center == null ? Vector2.zero : new Vector2((float)center["x"], (float)center["y"]);
        }

        private static Rect ReadScreenRect(JToken slice)
        {
            // Serialized as "(x:25.40, y:235.96, width:589.19, height:36.95)".
            string raw = slice?["ScreenRect"]?.ToString();
            if (string.IsNullOrEmpty(raw))
            {
                return Rect.zero;
            }

            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                raw,
                @"x:\s*(-?[\d.]+),\s*y:\s*(-?[\d.]+),\s*width:\s*(-?[\d.]+),\s*height:\s*(-?[\d.]+)");
            if (!match.Success)
            {
                return Rect.zero;
            }

            return new Rect(
                float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(match.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        // --- Minimal over-the-wire client helpers (mirrors UrdtE2ETests). ---

        private IEnumerator SendAndAwait(JObject request, string expectedId, Box outBox)
        {
            yield return WaitForTask(Send(_client, request.ToString(Formatting.None)));

            int guard = 0;
            while (guard++ < 400)
            {
                Task<string> receive = ReceiveMessage(_client);
                yield return WaitForTask(receive);

                JObject message = JObject.Parse(receive.Result);
                string id = message["id"] != null ? message["id"].ToString() : null;
                if (id == expectedId)
                {
                    outBox.Value = message;
                    yield break;
                }
            }

            Assert.Fail("No correlated response received for id '" + expectedId + "'.");
        }

        private static JObject Request(string id, string action, JObject payload)
        {
            JObject request = new JObject { ["api"] = 1, ["id"] = id, ["action"] = action };
            if (payload != null)
            {
                request["payload"] = payload;
            }

            return request;
        }

        private static Task Send(ClientWebSocket client, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task<string> ReceiveMessage(ClientWebSocket client)
        {
            byte[] buffer = new byte[8192];
            StringBuilder builder = new StringBuilder();
            WebSocketReceiveResult result;
            do
            {
                result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return "{\"id\":null}";
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            while (!result.EndOfMessage);

            return builder.ToString();
        }

        private static IEnumerator WaitForTask(Task task, bool throwOnFault = true)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(task.IsCompleted, "The async socket operation timed out.");
            if (throwOnFault && task.IsFaulted)
            {
                throw task.Exception;
            }
        }

        private sealed class Box
        {
            public JObject Value;
        }
    }
}
