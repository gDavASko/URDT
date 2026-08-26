using UnityEngine;

namespace KBP.URDT.Driver
{
    /// <summary>
    /// Addressing reference of a target object: by TestId, by stable handle,
    /// or by screen point (mirrors the RDT SPI resolveTarget contract).
    /// TestId resolution becomes available with the TestIdRegistry milestone (M3).
    /// </summary>
    public readonly struct TargetRef
    {
        private readonly string _testId;
        private readonly Handle _handle;
        private readonly Vector2? _point;

        private TargetRef(string testId, Handle handle, Vector2? point)
        {
            _testId = testId;
            _handle = handle;
            _point = point;
        }

        public string TestId
        {
            get { return _testId; }
        }

        public Handle Handle
        {
            get { return _handle; }
        }

        public Vector2? Point
        {
            get { return _point; }
        }

        public static TargetRef FromTestId(string testId)
        {
            return new TargetRef(testId, Handle.Invalid, null);
        }

        public static TargetRef FromHandle(Handle handle)
        {
            return new TargetRef(null, handle, null);
        }

        public static TargetRef FromPoint(Vector2 screenPoint)
        {
            return new TargetRef(null, Handle.Invalid, screenPoint);
        }
    }
}
