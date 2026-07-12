using KBP.URDT.Driver;
using UnityEngine;

namespace KBP.URDT.Registry
{
    /// <summary>
    /// Payload of an <c>object_registered</c>/<c>object_unregistered</c> event
    /// (04 §4). Carries the stable handle, the assigned TestId, the object and the
    /// registration source; <see cref="Reason"/> is set for unregistration.
    /// </summary>
    public readonly struct RegistrationInfo
    {
        public RegistrationInfo(
            Handle handle,
            string testId,
            GameObject gameObject,
            RegistrationSource source,
            string reason)
        {
            Handle = handle;
            TestId = testId;
            GameObject = gameObject;
            Source = source;
            Reason = reason;
        }

        public Handle Handle { get; }

        public string TestId { get; }

        public GameObject GameObject { get; }

        public RegistrationSource Source { get; }

        public string Reason { get; }
    }
}
