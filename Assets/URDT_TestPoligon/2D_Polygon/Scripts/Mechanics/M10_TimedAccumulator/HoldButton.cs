using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KBP.URDT.TestPoligon.Mechanics2D.M10_TimedAccumulator
{
    /// <summary>
    /// Кнопка удержания (Hold Button) для непрерывного накопления давления/энергии.
    /// </summary>
    [DisallowMultipleComponent]
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private bool _isPressed;
        public bool IsPressed => _isPressed;

        public event Action OnPressed;
        public event Action OnReleased;

        public void ResetButton()
        {
            _isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            OnPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            OnReleased?.Invoke();
        }

        private void OnDisable()
        {
            _isPressed = false;
        }
    }
}
