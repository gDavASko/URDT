using System;
using KBP.URDT.Diagnostics;
using KBP.URDT.Lifecycle;
using KBP.URDT.Net;
using KBP.URDT.Registry;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Bridges lifecycle signals (registry object_registered/unregistered, scene_loaded, and
    /// game log errors) to server-push protocol events (protocol §5). Only events the client
    /// has subscribed to are pushed (<see cref="SubscriptionSet"/>); a null set pushes all
    /// (used by direct unit tests). The host wires <paramref name="push"/> to
    /// <c>DebugServer.Send(ProtocolCodec.Serialize(env))</c>. Unsubscribes on dispose.
    /// </summary>
    public sealed class UrdtEventPublisher : IDisposable
    {
        private readonly TestIdRegistry _registry;
        private readonly SceneLifecycleManager _scenes;
        private readonly DiagnosticsCapture _diagnostics;
        private readonly SubscriptionSet _subscriptions;
        private readonly Action<ResponseEnvelope> _push;

        public UrdtEventPublisher(
            TestIdRegistry registry,
            SceneLifecycleManager scenes,
            DiagnosticsCapture diagnostics,
            SubscriptionSet subscriptions,
            Action<ResponseEnvelope> push)
        {
            _registry = registry;
            _scenes = scenes;
            _diagnostics = diagnostics;
            _subscriptions = subscriptions;
            _push = push;

            if (_registry != null)
            {
                _registry.ObjectRegistered += OnRegistered;
                _registry.ObjectUnregistered += OnUnregistered;
            }

            if (_scenes != null)
            {
                _scenes.SceneLoaded += OnSceneLoaded;
            }

            if (_diagnostics != null)
            {
                _diagnostics.LogReceived += OnLog;
            }
        }

        public void Dispose()
        {
            if (_registry != null)
            {
                _registry.ObjectRegistered -= OnRegistered;
                _registry.ObjectUnregistered -= OnUnregistered;
            }

            if (_scenes != null)
            {
                _scenes.SceneLoaded -= OnSceneLoaded;
            }

            if (_diagnostics != null)
            {
                _diagnostics.LogReceived -= OnLog;
            }
        }

        private void OnRegistered(RegistrationInfo info)
        {
            Push(ProtocolConstants.EVENT_OBJECT_REGISTERED, new JObject
            {
                ["testId"] = info.TestId,
                ["handle"] = info.Handle.Value,
                ["source"] = info.Source.ToString()
            });
        }

        private void OnUnregistered(RegistrationInfo info)
        {
            Push(ProtocolConstants.EVENT_OBJECT_UNREGISTERED, new JObject
            {
                ["testId"] = info.TestId,
                ["handle"] = info.Handle.Value,
                ["reason"] = info.Reason
            });
        }

        private void OnSceneLoaded(string sceneName, int buildIndex)
        {
            Push(ProtocolConstants.EVENT_SCENE_LOADED, new JObject
            {
                ["scene"] = sceneName,
                ["build_index"] = buildIndex
            });
        }

        private void OnLog(string level, string message, string stack, long timestampMs)
        {
            if (!IsErrorLevel(level))
            {
                return;
            }

            Push(ProtocolConstants.EVENT_LOG_ERROR, new JObject
            {
                ["level"] = level,
                ["msg"] = message,
                ["stack"] = stack,
                ["ts"] = timestampMs
            });
        }

        private static bool IsErrorLevel(string level)
        {
            return string.Equals(level, "Error", StringComparison.Ordinal)
                || string.Equals(level, "Exception", StringComparison.Ordinal)
                || string.Equals(level, "Assert", StringComparison.Ordinal);
        }

        private void Push(string eventName, JObject data)
        {
            Action<ResponseEnvelope> push = _push;
            if (push != null && (_subscriptions == null || _subscriptions.IsSubscribed(eventName)))
            {
                push(ResponseEnvelope.Event(eventName, data));
            }
        }
    }
}
