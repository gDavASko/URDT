using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M06_NodePairing
{
    /// <summary>
    /// Отрисовка UI провода/линии между двумя точками.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class WireLine : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Image _image;

        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());
        public Image Image => _image != null ? _image : (_image = GetComponent<Image>());

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            if (_image != null) _image.raycastTarget = false;
        }

        public void SetLocalEndpoints(Vector2 localStart, Vector2 localEnd, float thickness = 8f)
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_image == null) _image = GetComponent<Image>();
            if (_image != null) _image.raycastTarget = false;

            _rectTransform.pivot = new Vector2(0f, 0.5f);
            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.localPosition = new Vector3(localStart.x, localStart.y, 0f);

            Vector2 dir = localEnd - localStart;
            float distance = dir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            _rectTransform.sizeDelta = new Vector2(distance, thickness);
            _rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void SetPositions(Vector3 worldStart, Vector3 worldEnd, float thickness = 8f)
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            RectTransform parentRt = transform.parent as RectTransform;
            if (parentRt != null)
            {
                Vector2 localStart = parentRt.InverseTransformPoint(worldStart);
                Vector2 localEnd = parentRt.InverseTransformPoint(worldEnd);
                SetLocalEndpoints(localStart, localEnd, thickness);
            }
            else
            {
                _rectTransform.pivot = new Vector2(0f, 0.5f);
                transform.position = worldStart;
                Vector3 dir = worldEnd - worldStart;
                float distance = dir.magnitude;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                _rectTransform.sizeDelta = new Vector2(distance, thickness);
                _rectTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        public void SetColor(Color c)
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image != null) _image.color = c;
        }
    }
}
