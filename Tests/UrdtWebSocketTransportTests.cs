using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KBP.URDT.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// M5.1 engine-agnostic WebSocket transport: RFC 6455 handshake accept-key vector,
    /// frame codec round-trip (unmasked, masked, extended length), and a live loopback
    /// echo driven by a real <see cref="ClientWebSocket"/> against the <see cref="DebugServer"/>.
    /// </summary>
    public sealed class UrdtWebSocketTransportTests
    {
        [Test]
        public void Handshake_AcceptKey_MatchesRfc6455Vector()
        {
            // RFC 6455 §1.3 canonical example.
            string accept = WebSocketHandshake.ComputeAcceptKey("dGhlIHNhbXBsZSBub25jZQ==");
            Assert.AreEqual("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", accept);
        }

        [Test]
        public void FrameCodec_UnmaskedTextRoundTrip()
        {
            byte[] encoded = WebSocketFrameCodec.Encode(WebSocketFrame.Text("hello urdt"));

            using (MemoryStream ms = new MemoryStream(encoded))
            {
                WebSocketFrame frame;
                Assert.IsTrue(WebSocketFrameCodec.TryReadFrame(ms, out frame));
                Assert.AreEqual(WebSocketOpcode.Text, frame.Opcode);
                Assert.IsTrue(frame.IsFinal);
                Assert.AreEqual("hello urdt", frame.GetText());
            }
        }

        [Test]
        public void FrameCodec_MaskedClientFrame_IsUnmaskedOnRead()
        {
            byte[] maskKey = { 0x37, 0xFA, 0x21, 0x3D };
            byte[] encoded = WebSocketFrameCodec.Encode(WebSocketFrame.Text("masked payload"), maskKey);

            using (MemoryStream ms = new MemoryStream(encoded))
            {
                WebSocketFrame frame;
                Assert.IsTrue(WebSocketFrameCodec.TryReadFrame(ms, out frame));
                Assert.AreEqual("masked payload", frame.GetText());
            }
        }

        [Test]
        public void FrameCodec_ExtendedLength_RoundTrip()
        {
            string big = new string('x', 200); // > 125 → 16-bit extended length path
            byte[] encoded = WebSocketFrameCodec.Encode(WebSocketFrame.Text(big));

            using (MemoryStream ms = new MemoryStream(encoded))
            {
                WebSocketFrame frame;
                Assert.IsTrue(WebSocketFrameCodec.TryReadFrame(ms, out frame));
                Assert.AreEqual(200, frame.Payload.Length);
                Assert.AreEqual(big, frame.GetText());
            }
        }

        [UnityTest]
        public IEnumerator DebugServer_LoopbackEcho_OverRealWebSocketClient()
        {
            DebugServer server = new DebugServer();
            server.MessageProcessor = message => "echo:" + message;
            server.Start(0); // ephemeral loopback port
            int port = server.Port;

            ClientWebSocket client = new ClientWebSocket();
            string received = null;

            Task connect = client.ConnectAsync(new Uri("ws://127.0.0.1:" + port + "/"), CancellationToken.None);
            yield return WaitForTask(connect);

            byte[] payload = Encoding.UTF8.GetBytes("hello");
            Task send = client.SendAsync(
                new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
            yield return WaitForTask(send);

            byte[] buffer = new byte[1024];
            Task<WebSocketReceiveResult> receive = client.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
            yield return WaitForTask(receive);
            received = Encoding.UTF8.GetString(buffer, 0, receive.Result.Count);

            Assert.AreEqual("echo:hello", received, "The server must frame an echo back over the wire.");

            Task close = client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
            yield return WaitForTask(close, throwOnFault: false);
            client.Dispose();
            server.Stop();
        }

        [UnityTest]
        public IEnumerator DebugServer_NewClientSupersedesStaleClient()
        {
            DebugServer server = new DebugServer();
            server.MessageProcessor = message => "echo:" + message;
            server.Start(0);

            ClientWebSocket first = new ClientWebSocket();
            Task firstConnect = first.ConnectAsync(
                new Uri("ws://127.0.0.1:" + server.Port + "/"), CancellationToken.None);
            yield return WaitForTask(firstConnect);

            ClientWebSocket second = new ClientWebSocket();
            Task secondConnect = second.ConnectAsync(
                new Uri("ws://127.0.0.1:" + server.Port + "/"), CancellationToken.None);
            yield return WaitForTask(secondConnect);

            byte[] payload = Encoding.UTF8.GetBytes("replacement");
            Task send = second.SendAsync(
                new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
            yield return WaitForTask(send);

            byte[] buffer = new byte[1024];
            Task<WebSocketReceiveResult> receive = second.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
            yield return WaitForTask(receive);

            string received = Encoding.UTF8.GetString(buffer, 0, receive.Result.Count);
            Assert.AreEqual("echo:replacement", received);

            first.Dispose();
            second.Dispose();
            server.Stop();
        }

        [Test]
        public void DebugServer_OccupiedPreferredPort_UsesFallbackPort()
        {
            TcpListener blocker = new TcpListener(IPAddress.Loopback, 0);
            blocker.Start();
            int occupiedPort = ((IPEndPoint)blocker.LocalEndpoint).Port;
            DebugServer server = new DebugServer();

            try
            {
                int actualPort = server.StartWithFallback(occupiedPort, 1);

                Assert.AreNotEqual(occupiedPort, actualPort);
                Assert.Greater(actualPort, 0);
            }
            finally
            {
                server.Stop();
                blocker.Stop();
            }
        }

        private static IEnumerator WaitForTask(Task task, bool throwOnFault = true)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
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
    }
}
