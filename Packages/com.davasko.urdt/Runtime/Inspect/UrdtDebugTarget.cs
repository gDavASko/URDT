using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Universal URDT beacon component for tracking, observation, and automated testing of ANY GameObject.
    /// Attach this component to any entity, prop, actor, 3D/2D object, or manager in any Unity project.
    /// Exposes rich [TestInspectable] metadata and runtime states over the URDT WebSocket protocol.
    /// Pure universal component with zero UI/TMPro coupling (adheres strictly to SRP).
    /// For UI elements (buttons, toggles, sliders, text fields, dropdowns, scroll views),
    /// use the specialized <see cref="UrdtUiTarget"/> subclasses (e.g. <see cref="UrdtUiButtonTarget"/>, <see cref="UrdtUiToggleTarget"/>, etc.).
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtDebugTarget : MonoBehaviour, ISerializationCallbackReceiver
    {
        [Header("URDT Identification")]
        [Tooltip("Unique stable identifier used by external test runners. Defaults to GameObject name if empty.")]
        [SerializeField] private string _targetId = string.Empty;

        [Tooltip("Category of the target: entity, prop, actor, trigger, camera, light, target, etc.")]
        [SerializeField] private string _targetKind = string.Empty;

        [Tooltip("Logical subsystem or module name (e.g. 'core', 'gameplay', 'physics', 'ai').")]
        [SerializeField] private string _module = "core";

        [Tooltip("Name of the active context/window owning this target.")]
        [SerializeField] private string _activeWindow = "main";

        [Tooltip("Currently active module context.")]
        [SerializeField] private string _activeModule = "core";

        [Header("URDT Capabilities")]
        [Tooltip("List of supported URDT actions selectable as enums in Inspector.")]
        [SerializeField] private List<UrdtCommandType> _commands = new List<UrdtCommandType>
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query
        };

        [SerializeField, HideInInspector]
        private string _supportedCommands = string.Empty;

        private int _interactionCount;
        private string _lastInputAction = "none";
        private string _lastResult = "idle";
        private string _blockedReason = string.Empty;

        #region TestInspectable Properties

        [TestInspectable]
        public string TargetId
        {
            get { return string.IsNullOrEmpty(_targetId) ? name : _targetId; }
            set
            {
                if (_targetId == value) return;
                _targetId = value;
                // The registry fixes an object's id when it first registers; keep it in sync with a changed id.
                if (Application.isPlaying && UrdtServerHost.Instance != null && UrdtServerHost.Instance.Registry != null)
                {
                    UrdtServerHost.Instance.Registry.Rekey(gameObject);
                }
            }
        }

        [TestInspectable]
        public virtual string TargetKind
        {
            get
            {
                if (!string.IsNullOrEmpty(_targetKind))
                {
                    return _targetKind;
                }

                return InferTargetKind();
            }
            set { _targetKind = value; }
        }

        [TestInspectable]
        public string Module
        {
            get { return _module; }
            set { _module = value; }
        }

        [TestInspectable]
        public string ActiveWindow
        {
            get
            {
                if (!string.IsNullOrEmpty(UrdtUiWindowTarget.CurrentActiveWindow))
                {
                    return UrdtUiWindowTarget.CurrentActiveWindow;
                }
                return _activeWindow;
            }
            set { _activeWindow = value; }
        }

        [TestInspectable]
        public string ActiveModule
        {
            get
            {
                if (!string.IsNullOrEmpty(UrdtUiWindowTarget.CurrentActiveModule))
                {
                    return UrdtUiWindowTarget.CurrentActiveModule;
                }
                return _activeModule;
            }
            set { _activeModule = value; }
        }

        [TestInspectable]
        public string SupportedCommands
        {
            get
            {
                if (_commands == null || _commands.Count == 0)
                {
                    return string.Empty;
                }

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < _commands.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append(_commands[i].ToProtocolString());
                }

                return builder.ToString();
            }
        }

        public List<UrdtCommandType> Commands
        {
            get { return _commands; }
            set { _commands = value; }
        }

        [TestInspectable]
        public int InteractionCount
        {
            get { return _interactionCount; }
            set { _interactionCount = value; }
        }

        [TestInspectable]
        public string LastInputAction
        {
            get { return _lastInputAction; }
            set { _lastInputAction = value; }
        }

        [TestInspectable]
        public string LastResult
        {
            get { return _lastResult; }
            set { _lastResult = value; }
        }

        [TestInspectable]
        public string BlockedReason
        {
            get { return _blockedReason; }
            set { _blockedReason = value; }
        }

        [TestInspectable]
        public virtual bool IsVisible
        {
            get { return gameObject.activeInHierarchy; }
        }

        [TestInspectable]
        public virtual bool IsInteractable
        {
            get { return true; }
        }

        [TestInspectable]
        public virtual Vector3 WorldPosition
        {
            get { return transform.position; }
        }

        [TestInspectable]
        public virtual Rect ScreenRect
        {
            get { return GetScreenRect(); }
        }

        [TestInspectable]
        public virtual Vector2 ScreenCenter
        {
            get { return GetScreenCenter(); }
        }

        #endregion

        #region Lifecycle & Configuration

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (!string.IsNullOrEmpty(_supportedCommands) && (_commands == null || _commands.Count == 0))
            {
                _commands = ParseCommandString(_supportedCommands);
                _supportedCommands = string.Empty;
            }
        }

        protected virtual void Awake()
        {
            if (!string.IsNullOrEmpty(_supportedCommands) && (_commands == null || _commands.Count == 0))
            {
                _commands = ParseCommandString(_supportedCommands);
                _supportedCommands = string.Empty;
            }

            KBP.URDT.Registry.UrdtBeaconDiscovery.NotifyAwake(this);
        }

        protected virtual void OnValidate()
        {
            if (!string.IsNullOrEmpty(_supportedCommands) && (_commands == null || _commands.Count == 0))
            {
                _commands = ParseCommandString(_supportedCommands);
                _supportedCommands = string.Empty;
            }
        }

        protected virtual void Reset()
        {
            if (string.IsNullOrEmpty(_targetId))
            {
                _targetId = name;
            }

            _targetKind = InferTargetKind();
        }

        public virtual void Configure(
            string targetId,
            string targetKind,
            string activeWindow,
            string activeModule,
            string module,
            IEnumerable<UrdtCommandType> supportedCommands)
        {
            _targetId = targetId;
            _targetKind = string.IsNullOrEmpty(targetKind) ? InferTargetKind() : targetKind;
            _module = string.IsNullOrEmpty(module) ? "core" : module;
            _activeWindow = string.IsNullOrEmpty(activeWindow) ? "main" : activeWindow;
            _activeModule = string.IsNullOrEmpty(activeModule) ? _module : activeModule;

            _commands = supportedCommands != null
                ? new List<UrdtCommandType>(supportedCommands)
                : new List<UrdtCommandType> { UrdtCommandType.Inspect, UrdtCommandType.Query };
        }

        public virtual void Configure(
            string targetId,
            string targetKind,
            string activeWindow,
            string activeModule,
            string module,
            string supportedCommands)
        {
            Configure(targetId, targetKind, activeWindow, activeModule, module, ParseCommandString(supportedCommands));
        }

        public static List<UrdtCommandType> ParseCommandString(string raw)
        {
            List<UrdtCommandType> list = new List<UrdtCommandType>();
            if (string.IsNullOrEmpty(raw))
            {
                list.Add(UrdtCommandType.Inspect);
                list.Add(UrdtCommandType.Query);
                return list;
            }

            string[] parts = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                UrdtCommandType cmd;
                if (UrdtCommandExtensions.TryParseCommand(parts[i], out cmd))
                {
                    if (!list.Contains(cmd))
                    {
                        list.Add(cmd);
                    }
                }
            }

            if (list.Count == 0)
            {
                list.Add(UrdtCommandType.Inspect);
                list.Add(UrdtCommandType.Query);
            }

            return list;
        }

        public void SetContext(string activeWindow, string activeModule)
        {
            if (!string.IsNullOrEmpty(activeWindow))
            {
                _activeWindow = activeWindow;
            }

            if (!string.IsNullOrEmpty(activeModule))
            {
                _activeModule = activeModule;
            }
        }

        public bool SupportsCommand(UrdtCommandType command)
        {
            return _commands != null && _commands.Contains(command);
        }

        public bool SupportsCommand(string command)
        {
            UrdtCommandType type;
            if (UrdtCommandExtensions.TryParseCommand(command, out type))
            {
                return SupportsCommand(type);
            }

            return false;
        }

        public virtual void RecordInput(string action, string result, string blockedReason = "")
        {
            _interactionCount++;
            _lastInputAction = string.IsNullOrEmpty(action) ? "unknown" : action;
            _lastResult = string.IsNullOrEmpty(result) ? "ok" : result;
            _blockedReason = blockedReason ?? string.Empty;
        }

        public virtual void ResetState()
        {
            _interactionCount = 0;
            _lastInputAction = "reset_state";
            _lastResult = "reset";
            _blockedReason = string.Empty;
        }

        #endregion

        #region Virtual Helpers

        protected virtual string InferTargetKind()
        {
            if (GetComponent<Camera>() != null) return "camera";
            if (GetComponent<Light>() != null) return "light";
            if (GetComponent<Collider>() != null || GetComponent<Collider2D>() != null) return "collider";
            if (GetComponent<Renderer>() != null) return "renderer";
            return "target";
        }

        protected virtual Rect GetScreenRect()
        {
            Vector2 center = GetScreenCenter();
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null && Camera.main != null)
            {
                Bounds bounds = renderer.bounds;
                Vector3 minScreen = Camera.main.WorldToScreenPoint(bounds.min);
                Vector3 maxScreen = Camera.main.WorldToScreenPoint(bounds.max);
                return Rect.MinMaxRect(
                    Mathf.Min(minScreen.x, maxScreen.x),
                    Mathf.Min(minScreen.y, maxScreen.y),
                    Mathf.Max(minScreen.x, maxScreen.x),
                    Mathf.Max(minScreen.y, maxScreen.y));
            }

            return new Rect(center.x - 16f, center.y - 16f, 32f, 32f);
        }

        protected virtual Vector2 GetScreenCenter()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 screenPoint = cam.WorldToScreenPoint(transform.position);
                return new Vector2(screenPoint.x, screenPoint.y);
            }

            return Vector2.zero;
        }

        #endregion
    }
}
