using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// A button that tracks held down state (IPointerDownHandler, IPointerUpHandler).
    /// Used for "Hold to Draw" mechanics where drawing only happens while held.
    /// Free of direct TextMeshPro dependencies for modular compatibility.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Graphic _background;
        [SerializeField] private GameObject _labelObject;
        [SerializeField] private Color _idleColor = new Color(0.18f, 0.45f, 0.72f, 1f);
        [SerializeField] private Color _heldColor = new Color(0.15f, 0.75f, 0.35f, 1f);
        [SerializeField] private string _idleText = "Hold to Draw";
        [SerializeField] private string _heldText = "DRAWING...";

        private bool _isHeld = false;

        public event Action<bool> OnHoldChanged;

        public bool IsHeld
        {
            get { return _isHeld; }
        }

        private void Awake()
        {
            if (_background == null)
            {
                _background = GetComponent<Graphic>();
            }
            if (_labelObject == null)
            {
                Transform child = transform.Find("Label");
                if (child != null)
                {
                    _labelObject = child.gameObject;
                }
            }
            UpdateVisual();
        }

        public void Configure(Graphic bg, GameObject labelObject, string idleText = "Hold to Draw", string heldText = "DRAWING...")
        {
            _background = bg;
            _labelObject = labelObject;
            _idleText = idleText;
            _heldText = heldText;
            UpdateVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isHeld = true;
            UpdateVisual();
            Action<bool> handler = OnHoldChanged;
            if (handler != null)
            {
                handler(true);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isHeld = false;
            UpdateVisual();
            Action<bool> handler = OnHoldChanged;
            if (handler != null)
            {
                handler(false);
            }
        }

        private void UpdateVisual()
        {
            if (_background != null)
            {
                _background.color = _isHeld ? _heldColor : _idleColor;
            }

            SetLabelText(_isHeld ? _heldText : _idleText);
        }

        private void SetLabelText(string val)
        {
            if (_labelObject == null)
            {
                return;
            }

            Text uguiText = _labelObject.GetComponent<Text>();
            if (uguiText != null)
            {
                uguiText.text = val;
                return;
            }

            Component[] comps = _labelObject.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null) continue;
                PropertyInfo prop = comps[i].GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
                {
                    prop.SetValue(comps[i], val, null);
                    break;
                }
            }
        }
    }
}
