using KBP.URDT.Inspect;
using UnityEngine;

namespace KBP.URDT.Tests.Stand
{
    /// <summary>
    /// Stand object whose inspectable <see cref="Value"/> increments once per frame while
    /// <see cref="AutoIncrement"/> is on — drives <c>wait_for</c> "state changes over frames"
    /// scenarios.
    /// </summary>
    public sealed class UrdtCounter : MonoBehaviour
    {
        [TestInspectable]
        private int _value;

        public bool AutoIncrement = true;

        [TestInspectable]
        public int Value
        {
            get { return _value; }
        }

        public void SetValue(int value)
        {
            _value = value;
        }

        private void Update()
        {
            if (AutoIncrement)
            {
                _value++;
            }
        }
    }
}
