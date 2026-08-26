using KBP.URDT.Net;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// M5.2 protocol layer: envelope (de)serialization round-trip, handshake API/token
    /// negotiation, ping, and the pre-auth gate. Pure unit tests (no socket).
    /// </summary>
    public sealed class UrdtProtocolTests
    {
        private const int API = ProtocolConstants.API_VERSION;
        private const string TOKEN = "secret-token";

        private static UrdtProtocolSession NewSession(System.Func<RequestEnvelope, HandleOutcome> dispatch = null)
        {
            return new UrdtProtocolSession(API, TOKEN, ServerInfo.Default(1920, 1080), dispatch);
        }

        private static ResponseEnvelope Respond(UrdtProtocolSession session, string json, out bool close)
        {
            HandleOutcome outcome = session.Handle(json);
            close = outcome.CloseSocket;
            Assert.IsNotNull(outcome.ResponseText, "Expected a synchronous reply.");
            return ProtocolCodec.DeserializeResponse(outcome.ResponseText);
        }

        [Test]
        public void Envelope_RoundTrip_PreservesFieldsAndEchoesId()
        {
            ResponseEnvelope source = ResponseEnvelope.Ok("req-42", new JObject { ["value"] = 7 }, 12);
            string json = ProtocolCodec.Serialize(source);
            ResponseEnvelope back = ProtocolCodec.DeserializeResponse(json);

            Assert.AreEqual("req-42", back.Id);
            Assert.AreEqual(ProtocolConstants.STATUS_OK, back.Status);
            Assert.AreEqual(7, (int)back.Data["value"]);
            Assert.AreEqual(12, back.ElapsedMs);

            RequestEnvelope request;
            string err;
            Assert.IsTrue(ProtocolCodec.TryParseRequest(
                "{\"api\":1,\"id\":\"r1\",\"action\":\"inspect\",\"payload\":{\"testId\":\"btn\"}}", out request, out err));
            Assert.AreEqual(1, request.Api);
            Assert.AreEqual("r1", request.Id);
            Assert.AreEqual("inspect", request.Action);
            Assert.AreEqual("btn", request.Payload["testId"].ToString());
        }

        [Test]
        public void Handshake_ValidApiAndToken_ReturnsReady()
        {
            UrdtProtocolSession session = NewSession();
            bool close;
            ResponseEnvelope response = Respond(
                session,
                "{\"api\":1,\"id\":\"h1\",\"action\":\"handshake\",\"payload\":{\"token\":\"secret-token\"}}",
                out close);

            Assert.IsFalse(close);
            Assert.AreEqual("h1", response.Id);
            Assert.AreEqual(ProtocolConstants.STATUS_READY, response.Status);
            Assert.AreEqual(API, (int)response.Data["api"]);
            Assert.AreEqual(1920, (int)response.Data["screen"]["width"]);
            Assert.IsTrue(session.IsAuthenticated);
        }

        [Test]
        public void Handshake_IncompatibleApi_ReturnsApiVersionError()
        {
            UrdtProtocolSession session = NewSession();
            bool close;
            ResponseEnvelope response = Respond(
                session,
                "{\"api\":999,\"id\":\"h2\",\"action\":\"handshake\",\"payload\":{\"token\":\"secret-token\"}}",
                out close);

            Assert.IsFalse(close);
            Assert.AreEqual(ErrorCodes.E_API_VERSION, response.Error.Code);
            Assert.IsFalse(session.IsAuthenticated);
        }

        [Test]
        public void Handshake_BadToken_ReturnsUnauthorizedAndClosesSocket()
        {
            UrdtProtocolSession session = NewSession();
            bool close;
            ResponseEnvelope response = Respond(
                session,
                "{\"api\":1,\"id\":\"h3\",\"action\":\"handshake\",\"payload\":{\"token\":\"wrong\"}}",
                out close);

            Assert.IsTrue(close, "A bad token must close the socket (NFR-SEC-2).");
            Assert.AreEqual(ErrorCodes.E_UNAUTHORIZED, response.Error.Code);
            Assert.IsFalse(session.IsAuthenticated);
        }

        [Test]
        public void Command_BeforeHandshake_IsRejectedAsUnauthorized()
        {
            UrdtProtocolSession session = NewSession();
            bool close;
            ResponseEnvelope response = Respond(
                session, "{\"api\":1,\"id\":\"c1\",\"action\":\"inspect\"}", out close);

            Assert.AreEqual(ErrorCodes.E_UNAUTHORIZED, response.Error.Code);
        }

        [Test]
        public void Ping_AfterHandshake_ReturnsPong()
        {
            UrdtProtocolSession session = NewSession();
            bool close;
            Respond(session, "{\"api\":1,\"id\":\"h\",\"action\":\"handshake\",\"payload\":{\"token\":\"secret-token\"}}", out close);
            ResponseEnvelope pong = Respond(session, "{\"api\":1,\"id\":\"p1\",\"action\":\"ping\"}", out close);

            Assert.AreEqual("p1", pong.Id);
            Assert.AreEqual(ProtocolConstants.STATUS_OK, pong.Status);
            Assert.IsTrue((bool)pong.Data["pong"]);
        }

        [Test]
        public void UnknownAction_AfterHandshake_WithoutDispatch_ReturnsUnknownAction()
        {
            UrdtProtocolSession session = NewSession();
            bool close;
            Respond(session, "{\"api\":1,\"id\":\"h\",\"action\":\"handshake\",\"payload\":{\"token\":\"secret-token\"}}", out close);
            ResponseEnvelope response = Respond(session, "{\"api\":1,\"id\":\"u1\",\"action\":\"frobnicate\"}", out close);

            Assert.AreEqual(ErrorCodes.E_UNKNOWN_ACTION, response.Error.Code);
        }

        [Test]
        public void MalformedJson_ReturnsBadPayload()
        {
            UrdtProtocolSession session = NewSession();
            bool close;
            ResponseEnvelope response = Respond(session, "{ this is not json", out close);

            Assert.AreEqual(ErrorCodes.E_BAD_REQUEST, response.Error.Code);
            Assert.IsFalse(close);
        }

        [Test]
        public void Handshake_WrongExpectedInstance_ReturnsWrongInstanceAndCloses()
        {
            ServerInfo info = new ServerInfo(
                ProtocolConstants.SERVER_VERSION,
                ProtocolConstants.ORIGIN_BOTTOM_LEFT,
                1920,
                1080,
                "project-a",
                "hash-a",
                "editor-a",
                "run-a",
                "client-1",
                123,
                7777,
                true,
                "Ready",
                1L,
                0,
                0);
            UrdtProtocolSession session = new UrdtProtocolSession(API, TOKEN, info);

            bool close;
            ResponseEnvelope response = Respond(
                session,
                "{\"api\":1,\"id\":\"wrong\",\"action\":\"handshake\","
                + "\"payload\":{\"token\":\"secret-token\","
                + "\"expectedInstanceId\":\"editor-b\"}}",
                out close);

            Assert.IsTrue(close);
            Assert.AreEqual(ErrorCodes.E_WRONG_INSTANCE, response.Error.Code);
        }

        [Test]
        public void Health_AfterHandshake_ReturnsLiveIdentityAndReadiness()
        {
            ServerInfo info = new ServerInfo(
                ProtocolConstants.SERVER_VERSION,
                ProtocolConstants.ORIGIN_BOTTOM_LEFT,
                1920,
                1080,
                "project-a",
                "hash-a",
                "editor-a",
                "run-a",
                "host",
                123,
                7780,
                true,
                "Ready",
                10L,
                2,
                1);
            UrdtProtocolSession session = new UrdtProtocolSession(API, TOKEN, () => info);

            bool close;
            Respond(
                session,
                "{\"api\":1,\"id\":\"h\",\"action\":\"handshake\","
                + "\"payload\":{\"token\":\"secret-token\"}}",
                out close);
            ResponseEnvelope health = Respond(
                session,
                "{\"api\":1,\"id\":\"health\",\"action\":\"health\"}",
                out close);

            Assert.AreEqual("editor-a", health.Data["instanceId"].ToString());
            Assert.AreEqual("host", health.Data["peerRole"].ToString());
            Assert.IsTrue((bool)health.Data["runtime_ready"]);
            Assert.AreEqual(7780, (int)health.Data["port"]);
        }
    }
}
