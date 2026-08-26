using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Driver
{
    /// <summary>
    /// Engine Driver SPI seam of URDT (AI-IMPLEMENTATION-GUIDE §5.1, RDT SPI 02).
    /// Input is honest and device-level (invariant I1); the game owns hit logic
    /// (invariant I2); addressing uses stable handles (invariant I8).
    /// Time-control members are optional capabilities: implementations that do not
    /// support them yet throw <see cref="System.NotSupportedException"/>
    /// (protocol error E_UNSUPPORTED).
    /// </summary>
    public interface IUrdtDriver
    {
        void InjectPointer(Vector2 screenPos, PointerPhase phase, int pointerId = 0);

        Handle ResolveTarget(TargetRef reference);

        IReadOnlyList<NodeState> QueryNodes(Selector selector);

        NodeState InspectNode(Handle target, IReadOnlyList<string> componentWhitelist = null);

        void StepFrame(int frames, float deltaMs);

        void SetTimeScale(float scale);

        void PinFixedDelta(float fixedDeltaMs, float maxDeltaMs);

        void ResetScene(SceneRef scene);

        CaptureResult Capture(CaptureOptions options);
    }
}
