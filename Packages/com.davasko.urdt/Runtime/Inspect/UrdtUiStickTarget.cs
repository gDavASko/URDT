using System;
using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for Virtual Stick / Joystick elements.
    /// Single Responsibility: tracks stick deflection vector, magnitude, press state, and handle coordinates.
    /// Free of any buttons, sliders, or unrelated controls.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiStickTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_STICK_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query,
            UrdtCommandType.Drag,
            UrdtCommandType.PressMove,
            UrdtCommandType.Swipe
        };

        [Header("Stick Component")]
        [Tooltip("Virtual stick component bound to this beacon (auto-detected if null).")]
        [SerializeField] private UrdtVirtualStick _stick = null;

        [TestInspectable]
        public float StickX
        {
            get { return _stick != null ? _stick.InputVector.x : 0f; }
        }

        [TestInspectable]
        public float StickY
        {
            get { return _stick != null ? _stick.InputVector.y : 0f; }
        }

        [TestInspectable]
        public float Magnitude
        {
            get { return _stick != null ? _stick.Magnitude : 0f; }
        }

        [TestInspectable]
        public bool IsPressed
        {
            get { return _stick != null && _stick.IsPressed; }
        }

        [TestInspectable]
        public Vector2 HandleScreenCenter
        {
            get
            {
                if (_stick != null && _stick.Handle != null)
                {
                    Canvas c = Canvas;
                    Camera cam = (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) ? c.worldCamera : null;
                    return RectTransformUtility.WorldToScreenPoint(cam, _stick.Handle.position);
                }
                return ScreenCenter;
            }
        }

        [TestInspectable]
        public override bool IsInteractable
        {
            get { return _stick != null && isActiveAndEnabled; }
        }

        public UrdtVirtualStick Stick
        {
            get { return _stick; }
            set { _stick = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            AutoBindStick();
            EnsureDefaultCommands();
        }

        protected virtual void OnEnable()
        {
            if (_stick != null)
            {
                _stick.OnValueChanged += OnStickChanged;
            }
        }

        protected virtual void OnDisable()
        {
            if (_stick != null)
            {
                _stick.OnValueChanged -= OnStickChanged;
            }
        }

        private void OnStickChanged(Vector2 val)
        {
            RecordInput("drag", "stick_x=" + val.x.ToString("0.00") + ",y=" + val.y.ToString("0.00"));
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            AutoBindStick();
        }

        protected override void Reset()
        {
            base.Reset();
            AutoBindStick();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "stick";
            }
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count <= 2)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_STICK_COMMANDS);
            }
        }

        public void AutoBindStick()
        {
            if (_stick == null)
            {
                _stick = GetComponent<UrdtVirtualStick>();
            }
        }

        public void ConfigureStick(
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            IEnumerable<UrdtCommandType> supportedCommands = null,
            UrdtVirtualStick stick = null,
            RectTransform rectTransform = null,
            Canvas canvas = null)
        {
            base.ConfigureUi(
                targetId,
                "stick",
                activeWindow,
                activeModule,
                module,
                supportedCommands != null ? supportedCommands : DEFAULT_STICK_COMMANDS,
                rectTransform,
                canvas);

            _stick = stick != null ? stick : _stick;
            AutoBindStick();
        }

        protected override string InferTargetKind()
        {
            return "stick";
        }
    }
}
