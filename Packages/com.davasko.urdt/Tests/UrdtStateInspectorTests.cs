using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Inspect;
using KBP.URDT.Tests.Stand;
using NUnit.Framework;
using UnityEngine;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// Verifies StateInspector fault isolation: a single throwing <c>[TestInspectable]</c>
    /// getter must not abort the whole component slice (TZ §7.5).
    /// </summary>
    public sealed class UrdtStateInspectorTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
                _go = null;
            }
        }

        [Test]
        public void Inspect_ThrowingInspectableMember_IsIsolated_OthersStillReported()
        {
            _go = new GameObject("Throwing");
            _go.AddComponent<UrdtThrowingInspectable>();

            StateInspector inspector = new StateInspector();
            NodeState state = null;
            Assert.DoesNotThrow(
                () => state = inspector.BuildNodeState(_go, Handle.Invalid),
                "A throwing inspectable getter must not propagate out of the inspector.");

            Dictionary<string, object> slice;
            Assert.IsTrue(
                state.Components.TryGetValue(nameof(UrdtThrowingInspectable), out slice),
                "The component slice must still be produced.");
            Assert.AreEqual(7, (int)slice["Good"], "The well-behaved member must be reported.");
            StringAssert.StartsWith("E_READ:", (string)slice["Bad"], "The throwing member must be reported as E_READ.");
        }
    }
}
