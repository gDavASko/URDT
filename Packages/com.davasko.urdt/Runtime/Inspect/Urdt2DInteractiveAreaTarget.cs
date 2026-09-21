using System;
using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for interactive 2D zones (tap halves, grid cells, waypoints, dials, reaction spots).
    /// Single Responsibility: tracks area state, values, activation, and coordinates for input targeting.
    /// </summary>
    [DisallowMultipleComponent]
    public class Urdt2DInteractiveAreaTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_AREA_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query,
            UrdtCommandType.Click,
            UrdtCommandType.DoubleClick,
            UrdtCommandType.Drag,
            UrdtCommandType.PressMove,
            UrdtCommandType.PointerDown,
            UrdtCommandType.PointerUp,
            UrdtCommandType.Swipe
        };

        [Header("Area Attributes")]
        [SerializeField] private string _areaId = string.Empty;
        [SerializeField] private string _areaType = "interactive_zone";
        [SerializeField] private bool _isActiveArea = true;
        [SerializeField] private float _numericValue = 0f;
        [SerializeField] private string _stateString = "idle";

        [TestInspectable]
        public string AreaId
        {
            get { return string.IsNullOrEmpty(_areaId) ? TargetId : _areaId; }
            set { _areaId = value; }
        }

        [TestInspectable]
        public string AreaType
        {
            get { return _areaType; }
            set { _areaType = value; }
        }

        [TestInspectable]
        public bool IsActiveArea
        {
            get { return _isActiveArea; }
            set { _isActiveArea = value; }
        }

        [TestInspectable]
        public float NumericValue
        {
            get { return _numericValue; }
            set { _numericValue = value; }
        }

        [TestInspectable]
        public string StateString
        {
            get { return _stateString; }
            set { _stateString = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureDefaultCommands();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureDefaultCommands();
        }

        protected override void Reset()
        {
            base.Reset();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "interactive_area";
            }
        }

        public void SetAreaState(bool isActive, string state, float numericVal = 0f)
        {
            _isActiveArea = isActive;
            _stateString = state;
            _numericValue = numericVal;
        }

        public void ConfigureArea(
            string targetId,
            string areaId,
            string areaType,
            bool isActive = true,
            string activeWindow = "window_2d_suite",
            string activeModule = "2d")
        {
            base.ConfigureUi(
                targetId: targetId,
                targetKind: "interactive_area",
                activeWindow: activeWindow,
                activeModule: activeModule,
                module: "2d",
                supportedCommands: DEFAULT_AREA_COMMANDS
            );
            _areaId = areaId;
            _areaType = areaType;
            _isActiveArea = isActive;
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count == 0)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_AREA_COMMANDS);
            }
        }

        protected override string InferTargetKind()
        {
            return "interactive_area";
        }
    }
}
