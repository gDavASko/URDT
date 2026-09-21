using System;
using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for drop slots, sockets, receptacles, buckets, and target areas in 2D mechanics.
    /// Single Responsibility: tracks slot identity, accepted types, occupancy, and capacity progress.
    /// </summary>
    [DisallowMultipleComponent]
    public class Urdt2DSlotTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_SLOT_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query
        };

        [Header("Slot Attributes")]
        [SerializeField] private string _slotId = string.Empty;
        [SerializeField] private string _acceptedType = string.Empty;
        [SerializeField] private bool _isOccupied = false;
        [SerializeField] private int _currentCount = 0;
        [SerializeField] private int _requiredCount = 1;
        [SerializeField] private string _highlightState = "normal";

        [TestInspectable]
        public string SlotId
        {
            get { return string.IsNullOrEmpty(_slotId) ? TargetId : _slotId; }
            set { _slotId = value; }
        }

        [TestInspectable]
        public string AcceptedType
        {
            get { return _acceptedType; }
            set { _acceptedType = value; }
        }

        [TestInspectable]
        public bool IsOccupied
        {
            get { return _isOccupied; }
            set { _isOccupied = value; }
        }

        [TestInspectable]
        public int CurrentCount
        {
            get { return _currentCount; }
            set { _currentCount = value; }
        }

        [TestInspectable]
        public int RequiredCount
        {
            get { return _requiredCount; }
            set { _requiredCount = value; }
        }

        [TestInspectable]
        public string HighlightState
        {
            get { return _highlightState; }
            set { _highlightState = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureDefaultCommands();
            AutoDetectSlotData();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureDefaultCommands();
            AutoDetectSlotData();
        }

        protected override void Reset()
        {
            base.Reset();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "drop_slot";
            }
            AutoDetectSlotData();
        }

        public void AutoDetectSlotData()
        {
            var components = GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null || comp == this) continue;

                var compType = comp.GetType();
                var idProp = compType.GetProperty("SlotId");
                if (idProp != null && string.IsNullOrEmpty(_slotId))
                {
                    _slotId = idProp.GetValue(comp) as string ?? string.Empty;
                }

                var occProp = compType.GetProperty("IsOccupied");
                if (occProp != null)
                {
                    _isOccupied = (bool)occProp.GetValue(comp);
                }

                var curProp = compType.GetProperty("CurrentCount");
                if (curProp != null)
                {
                    _currentCount = (int)curProp.GetValue(comp);
                }

                var reqProp = compType.GetProperty("RequiredCount");
                if (reqProp != null)
                {
                    _requiredCount = (int)reqProp.GetValue(comp);
                }
            }
        }

        public void UpdateSlotState(bool isOccupied, int currentCount, string highlight = "normal")
        {
            _isOccupied = isOccupied;
            _currentCount = currentCount;
            _highlightState = highlight;
        }

        public void ConfigureSlot(
            string targetId,
            string slotId,
            string acceptedType,
            int requiredCount = 1,
            string activeWindow = "window_2d_suite",
            string activeModule = "2d")
        {
            base.ConfigureUi(
                targetId: targetId,
                targetKind: "drop_slot",
                activeWindow: activeWindow,
                activeModule: activeModule,
                module: "2d",
                supportedCommands: DEFAULT_SLOT_COMMANDS
            );
            _slotId = slotId;
            _acceptedType = acceptedType;
            _requiredCount = requiredCount;
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count == 0)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_SLOT_COMMANDS);
            }
        }

        protected override string InferTargetKind()
        {
            return "drop_slot";
        }
    }
}
