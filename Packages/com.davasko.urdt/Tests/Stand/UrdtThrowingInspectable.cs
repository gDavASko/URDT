using System;
using KBP.URDT.Inspect;
using UnityEngine;

namespace KBP.URDT.Tests.Stand
{
    /// <summary>
    /// Stand component with two <see cref="TestInspectableAttribute"/> members: a well-behaved
    /// one and one whose getter throws. Used to prove that a misbehaving inspectable getter
    /// does not abort the whole inspect (fault isolation).
    /// </summary>
    public sealed class UrdtThrowingInspectable : MonoBehaviour
    {
        [TestInspectable]
        public int Good
        {
            get { return 7; }
        }

        [TestInspectable]
        public int Bad
        {
            get { throw new InvalidOperationException("intentional inspectable failure"); }
        }
    }
}
