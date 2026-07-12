using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KBP.URDT.Transport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// M4 main-thread dispatch and routing (AI-IMPLEMENTATION-GUIDE §4, TZ §7.2/§7.5).
    /// Proves background→main-thread bridging with correlated ids, per-frame budget,
    /// handler-exception isolation as E_INTERNAL, shutdown draining, and the
    /// DontDestroyOnLoad singleton bootstrap. Verdicts are state-based (invariant I3).
    /// </summary>
    public sealed class UrdtDispatcherTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();
            MainThreadDispatcher.ClearInstanceForTests();
        }

        [Test]
        public void Router_UnknownAction_ReturnsUnknownActionError()
        {
            CommandRouter router = new CommandRouter();

            Response response = router.Route(new Command("req-1", "nope"));

            Assert.IsFalse(response.Ok);
            Assert.AreEqual("req-1", response.Id, "The error must echo the request id.");
            Assert.AreEqual(ErrorCodes.E_UNKNOWN_ACTION, response.ErrorCode);
        }

        [Test]
        public void Router_HandlerThrows_IsolatedAsInternal_DispatcherKeepsRouting()
        {
            CommandRouter router = new CommandRouter();
            router.Register("boom", new ThrowingHandler());
            router.Register("echo", new RecordingHandler());

            Response failed = router.Route(new Command("req-boom", "boom"));
            Assert.IsFalse(failed.Ok);
            Assert.AreEqual(ErrorCodes.E_INTERNAL, failed.ErrorCode, "A thrown handler must be isolated as E_INTERNAL.");
            Assert.AreEqual("req-boom", failed.Id);

            Response ok = router.Route(new Command("req-echo", "echo"));
            Assert.IsTrue(ok.Ok, "The router must keep working after a handler throws.");
            Assert.AreEqual("req-echo", ok.Id);
        }

        [Test]
        public void Dispatcher_RespectsPerFrameBudget()
        {
            CommandRouter router = new CommandRouter();
            router.Register("echo", new RecordingHandler());
            MainThreadDispatcher dispatcher = CreateDispatcher(router, maxCommandsPerFrame: 2);

            for (int i = 0; i < 5; i++)
            {
                dispatcher.Submit(new Command("b" + i, "echo"));
            }

            Assert.AreEqual(2, dispatcher.PumpInbound(), "First pump must respect the budget of 2.");
            Assert.AreEqual(2, dispatcher.PumpInbound(), "Second pump processes the next 2.");
            Assert.AreEqual(1, dispatcher.PumpInbound(), "Third pump processes the final 1.");
            Assert.AreEqual(0, dispatcher.PumpInbound(), "Nothing left to process.");
        }

        [Test]
        public void Dispatcher_Shutdown_AnswersPendingAndNewWithShuttingDown()
        {
            CommandRouter router = new CommandRouter();
            router.Register("echo", new RecordingHandler());
            MainThreadDispatcher dispatcher = CreateDispatcher(router, maxCommandsPerFrame: 16);

            dispatcher.Submit(new Command("pending", "echo"));
            dispatcher.gameObject.SetActive(false); // OnDisable → drain for shutdown

            Response pending;
            Assert.IsTrue(dispatcher.TryDequeueResponse(out pending));
            Assert.AreEqual("pending", pending.Id);
            Assert.AreEqual(ErrorCodes.E_SHUTTING_DOWN, pending.ErrorCode);

            dispatcher.Submit(new Command("after", "echo"));
            Response after;
            Assert.IsTrue(dispatcher.TryDequeueResponse(out after));
            Assert.AreEqual(ErrorCodes.E_SHUTTING_DOWN, after.ErrorCode, "New commands after shutdown must be rejected.");
        }

        [Test]
        public void Dispatcher_ReconfigureAfterDisable_AcceptsNewConnectionScopedCommand()
        {
            CommandRouter router = new CommandRouter();
            router.Register("echo", new RecordingHandler());
            MainThreadDispatcher dispatcher = CreateDispatcher(router, maxCommandsPerFrame: 16);
            dispatcher.gameObject.SetActive(false);
            dispatcher.gameObject.SetActive(true);
            dispatcher.Configure(router, 16, true);

            dispatcher.Submit(42L, new Command("restart", "echo"), 1000);
            Assert.AreEqual(1, dispatcher.PumpInbound());

            long connectionId;
            Response response;
            Assert.IsTrue(dispatcher.TryDequeueResponse(out connectionId, out response));
            Assert.AreEqual(42L, connectionId);
            Assert.IsTrue(response.Ok);
            Assert.AreEqual("restart", response.Id);
        }

        [UnityTest]
        public IEnumerator Dispatcher_BackgroundSubmit_ExecutesOnMainThread_WithCorrelatedIds()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            RecordingHandler handler = new RecordingHandler();
            CommandRouter router = new CommandRouter();
            router.Register("echo", handler);
            MainThreadDispatcher dispatcher = CreateDispatcher(router, maxCommandsPerFrame: 16);

            int backgroundThreadId = -1;
            Task submit = Task.Run(() =>
            {
                backgroundThreadId = Thread.CurrentThread.ManagedThreadId;
                dispatcher.Submit(new Command("id-1", "echo"));
                dispatcher.Submit(new Command("id-2", "echo"));
                dispatcher.Submit(new Command("id-3", "echo"));
            });

            while (!submit.IsCompleted)
            {
                yield return null;
            }

            List<string> ids = new List<string>();
            int guard = 0;
            while (ids.Count < 3 && guard < 300)
            {
                Response response;
                while (dispatcher.TryDequeueResponse(out response))
                {
                    ids.Add(response.Id);
                }

                guard++;
                yield return null;
            }

            Assert.AreEqual(3, ids.Count, "All three background-submitted commands must be answered.");
            Assert.Contains("id-1", ids);
            Assert.Contains("id-2", ids);
            Assert.Contains("id-3", ids);
            Assert.AreEqual(mainThreadId, handler.LastThreadId, "Handlers must execute on the Unity main thread.");
            Assert.AreNotEqual(mainThreadId, backgroundThreadId, "Submission must have originated on a background thread.");
        }

        [UnityTest]
        public IEnumerator Dispatcher_ExternalPump_UsesSynchronizationContextForBackgroundSubmit()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            RecordingHandler handler = new RecordingHandler();
            CommandRouter router = new CommandRouter();
            router.Register("echo", handler);

            GameObject host = new GameObject("URDT_ExternalPumpDispatcher");
            _spawned.Add(host);
            MainThreadDispatcher dispatcher = host.AddComponent<MainThreadDispatcher>();
            dispatcher.Configure(router, 16, true);

            Assert.IsTrue(
                dispatcher.HasSynchronizationContextPump,
                "Externally pumped dispatchers must capture Unity's main-thread context.");

            Task submit = Task.Run(() => dispatcher.Submit(7L, new Command("posted", "echo"), 1000));
            while (!submit.IsCompleted)
            {
                yield return null;
            }

            Response response = null;
            long connectionId = 0L;
            int guard = 0;
            while (response == null && guard < 300)
            {
                dispatcher.TryDequeueResponse(out connectionId, out response);
                guard++;
                yield return null;
            }

            Assert.IsNotNull(response, "The synchronization-context pump must produce a response.");
            Assert.AreEqual(7L, connectionId);
            Assert.IsTrue(response.Ok);
            Assert.AreEqual("posted", response.Id);
            Assert.AreEqual(mainThreadId, handler.LastThreadId);
        }

        [UnityTest]
        public IEnumerator Bootstrap_CreatesDontDestroyOnLoadSingleton()
        {
            MainThreadDispatcher.ClearInstanceForTests();
            MainThreadDispatcher instance = MainThreadDispatcher.EnsureInstance();
            yield return null;

            Assert.IsNotNull(instance);
            Assert.AreSame(instance, MainThreadDispatcher.Instance, "Instance must be a singleton.");
            Assert.AreEqual(
                "DontDestroyOnLoad",
                instance.gameObject.scene.name,
                "The bootstrap singleton must be DontDestroyOnLoad.");

            Object.DestroyImmediate(instance.gameObject);
            MainThreadDispatcher.ClearInstanceForTests();
        }

        private MainThreadDispatcher CreateDispatcher(CommandRouter router, int maxCommandsPerFrame)
        {
            GameObject host = new GameObject("URDT_TestDispatcher");
            _spawned.Add(host);
            MainThreadDispatcher dispatcher = host.AddComponent<MainThreadDispatcher>();
            dispatcher.Configure(router, maxCommandsPerFrame);
            return dispatcher;
        }

        private sealed class RecordingHandler : ICommandHandler
        {
            public int LastThreadId { get; private set; }

            public Response Handle(Command command)
            {
                LastThreadId = Thread.CurrentThread.ManagedThreadId;
                return Response.Success(command.Id, command.Action);
            }
        }

        private sealed class ThrowingHandler : ICommandHandler
        {
            public Response Handle(Command command)
            {
                throw new System.InvalidOperationException("intentional test failure");
            }
        }
    }
}
