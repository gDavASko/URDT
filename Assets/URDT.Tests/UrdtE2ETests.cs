using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KBP.URDT;
using KBP.URDT.Net;
using KBP.URDT.Registry;
using KBP.URDT.Tests.Stand;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// M5.4 over-the-wire E2E: a real <see cref="ClientWebSocket"/> drives a full scenario
    /// (handshake → ping → query → inspect → capture → deterministic step_frame) against the
    /// live <see cref="UrdtServerHost"/>, verifying id correlation, plus a negative
    /// bad-token path that is rejected and closed.
    /// </summary>
    public sealed class UrdtE2ETests
    {
        private const string TOKEN = "secret";

        private GameObject _hostGo;
        private GameObject _target;
        private ClientWebSocket _client;

        [TearDown]
        public void TearDown()
        {
            if (_client != null)
            {
                try { _client.Dispose(); } catch (Exception) { }
                _client = null;
            }

            if (_target != null)
            {
                UnityEngine.Object.DestroyImmediate(_target);
                _target = null;
            }

            if (_hostGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_hostGo);
                _hostGo = null;
            }
        }

        [UnityTest]
        public IEnumerator FullScenario_DrivenOverTheWire()
        {
            UrdtServerHost host = StartHostWithTarget();
            _client = new ClientWebSocket();
            yield return WaitForTask(_client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port + "/"), CancellationToken.None));

            Box box = new Box();

            yield return SendAndAwait(Request("h", "handshake", new JObject { ["token"] = TOKEN }), "h", box);
            Assert.AreEqual(ProtocolConstants.STATUS_READY, box.Value["status"].ToString());
            Assert.AreEqual(1, (int)box.Value["data"]["api"]);

            yield return SendAndAwait(Request("p", "ping", null), "p", box);
            Assert.IsTrue((bool)box.Value["data"]["pong"]);

            yield return SendAndAwait(Request("q", "query", new JObject { ["byComponent"] = nameof(UrdtRegistryMarker) }), "q", box);
            Assert.GreaterOrEqual(((JArray)box.Value["data"]["matches"]).Count, 1);

            yield return SendAndAwait(Request("i", "inspect", new JObject { ["testId"] = "marker" }), "i", box);
            Assert.AreEqual("marker", box.Value["data"]["testId"].ToString());

            yield return SendAndAwait(Request("c", "capture", new JObject { ["screenshot"] = false }), "c", box);
            Assert.IsFalse((bool)box.Value["data"]["authoritative"]);

            // Negative over the wire: unknown action → E_UNKNOWN_ACTION (dispatched + routed).
            yield return SendAndAwait(Request("u", "frobnicate", null), "u", box);
            Assert.AreEqual(ProtocolConstants.STATUS_ERROR, box.Value["status"].ToString());
            Assert.AreEqual("E_UNKNOWN_ACTION", box.Value["error"]["code"].ToString());

            // Deterministic step_frame over the wire (mode entered host-side to avoid a
            // destructive scene reload inside the live test).
            host.Runtime.Time.EnterDeterministic(7);
            yield return SendAndAwait(Request("s1", "step_frame", new JObject { ["frames"] = 2 }), "s1", box);
            Assert.AreEqual(2, (int)box.Value["data"]["advanced"]);
            yield return SendAndAwait(Request("s2", "step_frame", new JObject { ["frames"] = 2 }), "s2", box);
            Assert.AreEqual(2, (int)box.Value["data"]["advanced"], "step_frame must be reproducible over the wire.");
            host.Runtime.Time.ExitDeterministic();

            yield return WaitForTask(
                _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None), false);
        }

        [UnityTest]
        public IEnumerator BadToken_IsRejectedAndSocketClosed()
        {
            UrdtServerHost host = StartHostWithTarget();
            _client = new ClientWebSocket();
            yield return WaitForTask(_client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port + "/"), CancellationToken.None));

            Box box = new Box();
            yield return SendAndAwait(Request("h", "handshake", new JObject { ["token"] = "wrong" }), "h", box);
            Assert.AreEqual(ProtocolConstants.STATUS_ERROR, box.Value["status"].ToString());
            Assert.AreEqual("E_UNAUTHORIZED", box.Value["error"]["code"].ToString());

            // A trailing receive processes the server's Close frame (or faults on the socket
            // drop), so the client observes the closure.
            Task<string> tail = ReceiveMessage(_client);
            yield return WaitForTask(tail, throwOnFault: false);

            Assert.IsTrue(
                tail.IsFaulted || _client.State != WebSocketState.Open,
                "The socket must be closed after a bad token.");
        }

        [UnityTest]
        public IEnumerator BadApi_ReturnsApiVersionError()
        {
            UrdtServerHost host = StartHostWithTarget();
            _client = new ClientWebSocket();
            yield return WaitForTask(_client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port + "/"), CancellationToken.None));

            Box box = new Box();
            JObject badApi = new JObject
            {
                ["api"] = 999,
                ["id"] = "h",
                ["action"] = "handshake",
                ["payload"] = new JObject { ["token"] = TOKEN }
            };
            yield return SendAndAwait(badApi, "h", box);

            Assert.AreEqual(ProtocolConstants.STATUS_ERROR, box.Value["status"].ToString());
            Assert.AreEqual("E_API_VERSION", box.Value["error"]["code"].ToString());
        }

        private UrdtServerHost StartHostWithTarget()
        {
            _hostGo = new GameObject("UrdtServerHost");
            UrdtServerHost host = _hostGo.AddComponent<UrdtServerHost>();
            host.StartServer(0, TOKEN);
            host.Registry.AddRule(new SelectorRule(new HasComponentPredicate(nameof(UrdtRegistryMarker)), "marker"));

            _target = new GameObject("Target");
            _target.AddComponent<UrdtRegistryMarker>();
            host.Registry.Register(_target, RegistrationSource.Incremental);
            return host;
        }

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
            float deadline = Time.realtimeSinceStartup + 8f;
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
