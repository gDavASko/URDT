using UnityEngine;

namespace KBP.URDT.TestPoligon
{
    /// <summary>
    /// Keeps the polygon root inside the current device safe area.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UrdtTestPoligonSafeArea : MonoBehaviour
    {
        [SerializeField] private RectTransform _rectTransform = null;

        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            CacheReferences();
            ApplySafeArea();
        }

        private void OnEnable()
        {
            CacheReferences();
            ApplySafeArea();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (_lastSafeArea != Screen.safeArea || _lastScreenSize != screenSize)
            {
                ApplySafeArea();
            }
        }

        private void OnValidate()
        {
            CacheReferences();
            ApplySafeArea();
        }

        public void ApplySafeArea()
        {
            CacheReferences();

            if (_rectTransform == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }

        private void CacheReferences()
        {
            if (_rectTransform == null)
            {
                TryGetComponent(out _rectTransform);
            }
        }
    }
}
