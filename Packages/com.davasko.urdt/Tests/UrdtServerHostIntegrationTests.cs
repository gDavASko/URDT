using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KBP.URDT.Registry;
using KBP.URDT.Tests.Stand;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// End-to-end transport proof covering socket, protocol, main-thread dispatch and
    /// response delivery.
    /// </summary>
    public sealed class UrdtServerHostIntegrationTests
    {
        private const string TOKEN = "integration-token";

        [UnityTest]
        public IEnumerator Host_HandshakeThenQuery_ReturnsOnSameConnection()
        {
            GameObject target = new GameObject("IntegrationQueryTarget");
            target.AddComponent<UrdtClickTarget>();

            GameObject hostObject = new GameObject("URDT_IntegrationHost");
            UrdtServerHost host = hostObject.AddComponent<UrdtServerHost>();
            host.RegisterSelectorRule(
                new HasComponentPredicate(nameof(UrdtClickTarget)),
                "integration_target");
            host.StartServer(0, TOKEN);
            yield return null;

            Assert.IsTrue(host.IsRuntimeReady, host.LastError);
            ClientWebSocket client = new ClientWebSocket();

            Task connect = client.ConnectAsync(
                new Uri("ws://127.0.0.1:" + host.Port + "/"), CancellationToken.None);
            yield return WaitForTask(connect);

            Task<string> handshake = SendReceiveAsync(
                client,
                "{\"api\":1,\"id\":\"h\",\"action\":\"handshake\","
                + "\"payload\":{\"token\":\"integration-token\"}}");
            yield return WaitForTask(handshake);
            StringAssert.Contains("\"status\":\"ready\"", handshake.Result);

            Task<string> query = SendReceiveAsync(
                client,
                "{\"api\":1,\"id\":\"q\",\"action\":\"query\","
                + "\"payload\":{\"byName\":\"IntegrationQueryTarget\"}}");
            yield return WaitForTask(query);
            StringAssert.Contains("\"status\":\"ok\"", query.Result);
            StringAssert.Contains("IntegrationQueryTarget", query.Result);

            client.Dispose();
            host.StopServer();
            UnityEngine.Object.DestroyImmediate(hostObject);
            UnityEngine.Object.DestroyImmediate(target);
        }

        [UnityTest]
        public IEnumerator Host_InvalidToken_ReturnsStructuredErrorBeforeClose()
        {
            GameObject hostObject = new GameObject("URDT_InvalidTokenHost");
            UrdtServerHost host = hostObject.AddComponent<UrdtServerHost>();
            host.StartServer(0, TOKEN);
            yield return null;

            ClientWebSocket client = new ClientWebSocket();
            Task connect = client.ConnectAsync(
                new Uri("ws://127.0.0.1:" + host.Port + "/"), CancellationToken.None);
            yield return WaitForTask(connect);

            Task<string> handshake = SendReceiveAsync(
                client,
                "{\"api\":1,\"id\":\"bad-auth\",\"action\":\"handshake\","
                + "\"payload\":{\"token\":\"wrong-token\"}}");
            yield return WaitForTask(handshake);
            StringAssert.Contains("\"status\":\"error\"", handshake.Result);
            StringAssert.Contains("\"code\":\"E_UNAUTHORIZED\"", handshake.Result);

            client.Dispose();
            host.StopServer();
            UnityEngine.Object.DestroyImmediate(hostObject);
        }

        private static async Task<string> SendReceiveAsync(ClientWebSocket client, string json)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            await client.SendAsync(
                new ArraySegment<byte>(payload),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            byte[] buffer = new byte[262144];
            WebSocketReceiveResult result = await client.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.AreEqual(WebSocketMessageType.Text, result.MessageType);
            return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }

        private static IEnumerator WaitForTask(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(task.IsCompleted, "The URDT integration operation timed out.");
            if (task.IsFaulted)
            {
                throw task.Exception;
            }
        }
    }
}
