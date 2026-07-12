using UnityEngine;

namespace KBP.URDT.Tests.Stand
{
    /// <summary>
    /// Drop target of the M0 test stand. Pure screen-space model:
    /// a point with an acceptance radius, no rendering and no game code involved.
    /// </summary>
    public sealed class UrdtTestReceiver : MonoBehaviour
    {
        private Vector2 _screenPosition;
        private float _acceptRadius = 40f;

        public Vector2 ScreenPosition
        {
            get { return _screenPosition; }
            set { _screenPosition = value; }
        }

        public float AcceptRadius
        {
            get { return _acceptRadius; }
            set { _acceptRadius = value; }
        }

        public bool Accepts(Vector2 screenPoint)
        {
            return (screenPoint - _screenPosition).sqrMagnitude <= _acceptRadius * _acceptRadius;
        }
    }
}
