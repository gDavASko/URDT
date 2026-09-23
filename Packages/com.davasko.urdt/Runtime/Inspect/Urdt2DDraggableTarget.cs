using System;
using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for draggable items, tokens, weights, cards, and objects in 2D mechanics.
    /// Single Responsibility: tracks drag states, item identity, category, and drop destinations.
    /// </summary>
    [DisallowMultipleComponent]
    public class Urdt2DDraggableTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_DRAGGABLE_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query,
            UrdtCommandType.Click,
            UrdtCommandType.Drag,
            UrdtCommandType.PressMove,
            UrdtCommandType.PointerDown,
            UrdtCommandType.PointerUp
        };

        [Header("Draggable Item Attributes")]
        [SerializeField] private string _itemId = string.Empty;
        [SerializeField] private string _category = string.Empty;
        [SerializeField] private bool _isJunk = false;
        [SerializeField] private bool _isSnapped = false;
        [SerializeField] private bool _isDragging = false;
        [SerializeField] private string _currentSlotId = string.Empty;
        [SerializeField] private Vector2 _homeAnchoredPosition;

        [TestInspectable]
        public string ItemId
        {
            get { return string.IsNullOrEmpty(_itemId) ? TargetId : _itemId; }
            set { _itemId = value; }
        }

        [TestInspectable]
        public string Category
        {
            get { return _category; }
            set { _category = value; }
        }

        [TestInspectable]
        public bool IsJunk
        {
            get { return UrdtLiveStateReader.ReadBool(gameObject, _isJunk, "IsJunk", "IsBroken", "IsJunkDust", "IsHazardBomb"); }
            set { _isJunk = value; }
        }

        [TestInspectable]
        public bool IsSnapped
        {
            get { return UrdtLiveStateReader.ReadBool(gameObject, _isSnapped, "IsLocked", "IsSnapped", "IsInstalled", "IsAttached", "IsEquipped", "IsConnected", "IsDeposited"); }
            set { _isSnapped = value; }
        }

        [TestInspectable]
        public bool IsDragging
        {
            get { return _isDragging; }
            set { _isDragging = value; }
        }

        /// <summary>Live read-only snapshot of the gameplay component(s) on this object.</summary>
        [TestInspectable]
        public System.Collections.Generic.Dictionary<string, object> GameState
        {
            get { return UrdtLiveStateReader.Read(gameObject); }
        }

        [TestInspectable]
        public string CurrentSlotId
        {
            get { return _currentSlotId; }
            set { _currentSlotId = value; }
        }

        [TestInspectable]
        public Vector2 HomePosition
        {
            get { return _homeAnchoredPosition; }
            set { _homeAnchoredPosition = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureDefaultCommands();
            AutoDetectItemData();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureDefaultCommands();
            AutoDetectItemData();
        }

        protected override void Reset()
        {
            base.Reset();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "draggable_item";
            }
            AutoDetectItemData();
        }

        public void AutoDetectItemData()
        {
            if (RectTransform != null && _homeAnchoredPosition == Vector2.zero)
            {
                _homeAnchoredPosition = RectTransform.anchoredPosition;
            }

            var components = GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null || comp == this) continue;

                var compType = comp.GetType();
                var idProp = compType.GetProperty("ItemId");
                if (idProp != null && string.IsNullOrEmpty(_itemId))
                {
                    _itemId = idProp.GetValue(comp) as string ?? string.Empty;
                }

                var junkProp = compType.GetProperty("IsJunk");
                if (junkProp != null)
                {
                    _isJunk = (bool)junkProp.GetValue(comp);
                }

                var snapProp = compType.GetProperty("IsLocked");
                if (snapProp != null)
                {
                    _isSnapped = (bool)snapProp.GetValue(comp);
                }
            }
        }

        public void SetDragState(bool isDragging, bool isSnapped = false, string currentSlotId = null)
        {
            _isDragging = isDragging;
            _isSnapped = isSnapped;
            if (currentSlotId != null)
            {
                _currentSlotId = currentSlotId;
            }
        }

        public void ConfigureDraggable(
            string targetId,
            string itemId,
            string category,
            bool isJunk = false,
            string activeWindow = "window_2d_suite",
            string activeModule = "2d")
        {
            base.ConfigureUi(
                targetId: targetId,
                targetKind: "draggable_item",
                activeWindow: activeWindow,
                activeModule: activeModule,
                module: "2d",
                supportedCommands: DEFAULT_DRAGGABLE_COMMANDS
            );
            _itemId = itemId;
            _category = category;
            _isJunk = isJunk;
            if (RectTransform != null)
            {
                _homeAnchoredPosition = RectTransform.anchoredPosition;
            }
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count == 0)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_DRAGGABLE_COMMANDS);
            }
        }

        protected override string InferTargetKind()
        {
            return "draggable_item";
        }
    }
}
