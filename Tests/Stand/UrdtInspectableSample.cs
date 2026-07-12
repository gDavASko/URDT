using KBP.URDT.Inspect;
using UnityEngine;

namespace KBP.URDT.Tests.Stand
{
    /// <summary>
    /// Stand object exposing a wide range of <see cref="TestInspectableAttribute"/> field types
    /// (int/float/bool/string/Vector3/Color) so inspect coverage exercises every value mapping.
    /// </summary>
    public sealed class UrdtInspectableSample : MonoBehaviour
    {
        [TestInspectable]
        public int IntValue = 42;

        [TestInspectable]
        public float FloatValue = 3.5f;

        [TestInspectable]
        public bool BoolFlag = true;

        [TestInspectable]
        public string Label = "hello";

        [TestInspectable]
        public Vector3 Vec = new Vector3(1f, 2f, 3f);

        [TestInspectable]
        public Color Col = Color.green;
    }
}
