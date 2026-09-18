using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Base class for all URDT UI beacons (inherits from <see cref="UrdtDebugTarget"/>).
    /// Responsible ONLY for common UI geometry, Canvas resolution, and screen-space mapping.
    /// Specialized UI controls (buttons, toggles, sliders, inputs, dropdowns, scroll views)
    /// must inherit from this class and declare only their own specific bindings and states.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiTarget : UrdtDebugTarget
    {
        [Header("UI Hierarchy Bindings")]
        [SerializeField] private RectTransform _rectTransform = null;
        [SerializeField] private Canvas _canvas = null;

        private readonly Vector3[] _worldCorners = new Vector3[4];

        #region UI Base TestInspectable Properties

        [TestInspectable]
        public override Rect ScreenRect
        {
            get { return GetScreenRect(); }
        }

        [TestInspectable]
        public override Vector2 ScreenCenter
        {
            get { return GetScreenCenter(); }
        }

        [TestInspectable]
        public Vector2 RectSize
        {
            get { return _rectTransform != null ? _rectTransform.rect.size : Vector2.zero; }
        }

        public RectTransform RectTransform
        {
            get { return _rectTransform; }
            set { _rectTransform = value; }
        }

        public Canvas Canvas
        {
            get { return _canvas; }
            set { _canvas = value; }
        }

        #endregion

        #region Lifecycle & Binding

        protected override void Awake()
        {
            base.Awake();
            AutoBindBaseReferences();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            AutoBindBaseReferences();
        }

        protected override void Reset()
        {
            base.Reset();
            AutoBindBaseReferences();
        }

        public virtual void AutoBindBaseReferences()
        {
            if (_rectTransform == null)
            {
                TryGetComponent(out _rectTransform);
            }

            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }
        }

        public virtual void ConfigureUi(
            string targetId,
            string targetKind,
            string activeWindow,
            string activeModule,
            string module,
            IEnumerable<UrdtCommandType> supportedCommands,
            RectTransform rectTransform = null,
            Canvas canvas = null)
        {
            base.Configure(
                targetId,
                string.IsNullOrEmpty(targetKind) ? InferTargetKind() : targetKind,
                string.IsNullOrEmpty(activeWindow) ? "main" : activeWindow,
                string.IsNullOrEmpty(activeModule) ? (string.IsNullOrEmpty(module) ? "ui" : module) : activeModule,
                string.IsNullOrEmpty(module) ? "ui" : module,
                supportedCommands);

            _rectTransform = rectTransform != null ? rectTransform : _rectTransform;
            _canvas = canvas != null ? canvas : _canvas;

            AutoBindBaseReferences();
        }

        #endregion

        #region Screen Space Helpers

        protected override Rect GetScreenRect()
        {
            if (_rectTransform == null)
            {
                return base.GetScreenRect();
            }

            Canvas.ForceUpdateCanvases();
            Camera camera = GetCanvasCamera();
            _rectTransform.GetWorldCorners(_worldCorners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[0]);
            Vector2 max = min;

            for (int i = 1; i < _worldCorners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        protected override Vector2 GetScreenCenter()
        {
            if (_rectTransform == null)
            {
                return base.GetScreenCenter();
            }

            Canvas.ForceUpdateCanvases();
            Vector3 worldCenter = _rectTransform.TransformPoint(_rectTransform.rect.center);
            return RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(), worldCenter);
        }

        protected Camera GetCanvasCamera()
        {
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                return _canvas.worldCamera;
            }

            return null;
        }

        #endregion
    }
}
