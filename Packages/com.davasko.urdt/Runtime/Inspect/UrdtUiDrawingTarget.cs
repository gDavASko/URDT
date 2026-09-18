using System;
using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for the interactive Drawing Canvas.
    /// Single Responsibility: exposes pen coordinates, drawing status, stroke counts, and drawn length.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiDrawingTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_DRAWING_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query
        };

        [SerializeField] private UrdtDrawingCanvas _drawingCanvas = null;

        [TestInspectable]
        public float PenX
        {
            get { return _drawingCanvas != null ? _drawingCanvas.PenPosition.x : 0f; }
        }

        [TestInspectable]
        public float PenY
        {
            get { return _drawingCanvas != null ? _drawingCanvas.PenPosition.y : 0f; }
        }

        [TestInspectable]
        public bool IsDrawing
        {
            get { return _drawingCanvas != null && _drawingCanvas.IsDrawing; }
        }

        [TestInspectable]
        public int StrokeCount
        {
            get { return _drawingCanvas != null ? _drawingCanvas.StrokeCount : 0; }
        }

        [TestInspectable]
        public float TotalDrawnLength
        {
            get { return _drawingCanvas != null ? _drawingCanvas.TotalDrawnLength : 0f; }
        }

        public UrdtDrawingCanvas DrawingCanvas
        {
            get { return _drawingCanvas; }
            set { _drawingCanvas = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            AutoBindCanvas();
            EnsureDefaultCommands();
        }

        public void AutoBindCanvas()
        {
            if (_drawingCanvas == null)
            {
                _drawingCanvas = GetComponent<UrdtDrawingCanvas>();
            }
        }

        public void ConfigureDrawing(
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            UrdtDrawingCanvas canvas = null,
            RectTransform rectTransform = null,
            Canvas uiCanvas = null)
        {
            base.ConfigureUi(
                targetId,
                "drawing_canvas",
                activeWindow,
                activeModule,
                module,
                DEFAULT_DRAWING_COMMANDS,
                rectTransform,
                uiCanvas);

            _drawingCanvas = canvas != null ? canvas : _drawingCanvas;
            AutoBindCanvas();
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count <= 1)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_DRAWING_COMMANDS);
            }
        }

        protected override string InferTargetKind()
        {
            return "drawing_canvas";
        }
    }
}
