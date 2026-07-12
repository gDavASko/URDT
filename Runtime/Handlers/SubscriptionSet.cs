using System.Collections.Generic;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Thread-safe set of the event names a client has subscribed to (protocol §4.10/§5).
    /// Read by the event publisher (which may push from the main thread or a threaded log
    /// callback) and mutated by the subscribe/unsubscribe handlers on the main thread.
    /// </summary>
    public sealed class SubscriptionSet
    {
        private readonly HashSet<string> _events = new HashSet<string>();
        private readonly object _lock = new object();

        public void Add(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                return;
            }

            lock (_lock)
            {
                _events.Add(eventName);
            }
        }

        public void Remove(string eventName)
        {
            lock (_lock)
            {
                _events.Remove(eventName);
            }
        }

        public bool IsSubscribed(string eventName)
        {
            lock (_lock)
            {
                return _events.Contains(eventName);
            }
        }

        public string[] Snapshot()
        {
            lock (_lock)
            {
                string[] snapshot = new string[_events.Count];
                _events.CopyTo(snapshot);
                return snapshot;
            }
        }
    }
}
