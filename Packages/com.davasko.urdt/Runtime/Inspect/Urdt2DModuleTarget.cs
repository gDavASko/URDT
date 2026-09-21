using System;
using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for 2D mechanic modules and minigame root controllers.
    /// Single Responsibility: tracks mechanic lifecycle, instructions, completion status, and normalized progress.
    /// </summary>
    [DisallowMultipleComponent]
    public class Urdt2DModuleTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_MODULE_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query
        };

        [Header("2D Module Metadata")]
        [SerializeField] private string _mechanicId = string.Empty;
        [SerializeField] private string _mechanicTitle = string.Empty;
        [TextArea(2, 4)]
        [SerializeField] private string _instruction = string.Empty;
        [SerializeField] private bool _isCompleted = false;
        [Range(0f, 1f)]
        [SerializeField] private float _progressNormalized = 0f;
        [SerializeField] private float _score = 0f;
        [SerializeField] private float _targetScore = 1f;

        [TestInspectable]
        public string MechanicId
        {
            get { return _mechanicId; }
            set { _mechanicId = value; }
        }

        [TestInspectable]
        public string MechanicTitle
        {
            get { return _mechanicTitle; }
            set { _mechanicTitle = value; }
        }

        [TestInspectable]
        public string Instruction
        {
            get { return _instruction; }
            set { _instruction = value; }
        }

        [TestInspectable]
        public bool IsCompleted
        {
            get { return _isCompleted; }
            set { _isCompleted = value; }
        }

        [TestInspectable]
        public float ProgressNormalized
        {
            get { return _progressNormalized; }
            set { _progressNormalized = Mathf.Clamp01(value); }
        }

        [TestInspectable]
        public float Score
        {
            get { return _score; }
            set { _score = value; }
        }

        [TestInspectable]
        public float TargetScore
        {
            get { return _targetScore; }
            set { _targetScore = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureDefaultCommands();
            AutoDetectModuleData();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureDefaultCommands();
            AutoDetectModuleData();
        }

        protected override void Reset()
        {
            base.Reset();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "mechanic_2d";
            }
            AutoDetectModuleData();
        }

        public void AutoDetectModuleData()
        {
            // Auto-detect properties from attached BaseMechanic2DModule or IMechanic2DModule via reflection to avoid direct coupling
            var components = GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null || comp == this) continue;

                var compType = comp.GetType();
                var idProp = compType.GetProperty("MechanicId");
                if (idProp != null && string.IsNullOrEmpty(_mechanicId))
                {
                    _mechanicId = idProp.GetValue(comp) as string ?? string.Empty;
                }

                var titleProp = compType.GetProperty("Title");
                if (titleProp != null && string.IsNullOrEmpty(_mechanicTitle))
                {
                    _mechanicTitle = titleProp.GetValue(comp) as string ?? string.Empty;
                }

                var instrProp = compType.GetProperty("Instruction");
                if (instrProp != null && string.IsNullOrEmpty(_instruction))
                {
                    _instruction = instrProp.GetValue(comp) as string ?? string.Empty;
                }

                var compProp = compType.GetProperty("IsCompleted");
                if (compProp != null)
                {
                    _isCompleted = (bool)compProp.GetValue(comp);
                }

                var progProp = compType.GetProperty("ProgressNormalized");
                if (progProp != null)
                {
                    _progressNormalized = (float)progProp.GetValue(comp);
                }
            }
        }

        public void UpdateState(bool isCompleted, float progress, float score = 0f, float targetScore = 0f)
        {
            _isCompleted = isCompleted;
            _progressNormalized = Mathf.Clamp01(progress);
            if (score > 0f) _score = score;
            if (targetScore > 0f) _targetScore = targetScore;
        }

        public void ConfigureModule(
            string targetId,
            string mechanicId,
            string title,
            string instruction,
            string activeWindow = "window_2d_suite",
            string activeModule = "2d")
        {
            base.ConfigureUi(
                targetId: targetId,
                targetKind: "mechanic_2d",
                activeWindow: activeWindow,
                activeModule: activeModule,
                module: "2d",
                supportedCommands: DEFAULT_MODULE_COMMANDS
            );
            _mechanicId = mechanicId;
            _mechanicTitle = title;
            _instruction = instruction;
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count == 0)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_MODULE_COMMANDS);
            }
        }

        protected override string InferTargetKind()
        {
            return "mechanic_2d";
        }
    }
}
