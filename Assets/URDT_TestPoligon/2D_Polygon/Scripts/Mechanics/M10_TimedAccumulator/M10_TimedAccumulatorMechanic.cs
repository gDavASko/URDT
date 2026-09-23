using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M10_TimedAccumulator
{
    /// <summary>
    /// Механика #10: Временной интегратор с контролем диапазона (Timed Accumulator / Hold).
    /// Три этапа: от широкой зеленой зоны (70-85%) до узкой (78-82%). Взрыв клапана и
    /// перебор давления при отпускании считаются провалом этапа и сбрасывают прогресс.
    /// </summary>
    public class M10_TimedAccumulatorMechanic : BaseMechanic2DModule
    {
        [Header("Компоненты манометра")]
        [SerializeField] private RectTransform _needleTransform = null;
        [SerializeField] private HoldButton _holdButton = null;
        [SerializeField] private TMP_Text _pressureDisplay = null;
        [SerializeField] private TMP_Text _instructionText = null;

        [Header("Параметры накачки")]
        [SerializeField] private float _minNeedleAngle = 120f;
        [SerializeField] private float _maxNeedleAngle = -120f;

        // Параметры по этапам
        private static readonly float[] _stageTargetMin = { 0.70f, 0.75f, 0.78f };
        private static readonly float[] _stageTargetMax = { 0.85f, 0.82f, 0.82f };
        private static readonly float[] _stageFillRate  = { 0.35f, 0.45f, 0.55f };
        private static readonly float[] _stageDecayRate = { 0.20f, 0.25f, 0.30f };
        private static readonly float[] _stageBlowout   = { 0.92f, 0.90f, 0.88f };

        private float _currentValue = 0f;
        private bool _isLocked = false;

        public override int StageCount => 3;

        public float TargetMin => _stageTargetMin[Mathf.Clamp(CurrentStage - 1, 0, _stageTargetMin.Length - 1)];
        public float TargetMax => _stageTargetMax[Mathf.Clamp(CurrentStage - 1, 0, _stageTargetMax.Length - 1)];
        public float BlowoutThreshold => _stageBlowout[Mathf.Clamp(CurrentStage - 1, 0, _stageBlowout.Length - 1)];
        public float CurrentValue => _currentValue;

        protected override void Awake()
        {
            base.Awake();
            if (_holdButton != null)
            {
                _holdButton.OnReleased += HandleButtonReleased;
            }
        }

        private void OnDestroy()
        {
            if (_holdButton != null)
            {
                _holdButton.OnReleased -= HandleButtonReleased;
            }
        }

        protected override string GetStageInstruction(int stage)
        {
            float lo = _stageTargetMin[Mathf.Clamp(stage - 1, 0, _stageTargetMin.Length - 1)] * 100f;
            float hi = _stageTargetMax[Mathf.Clamp(stage - 1, 0, _stageTargetMax.Length - 1)] * 100f;
            switch (stage)
            {
                case 1: return $"Этап 1/3: удерживайте «НАКАЧКУ» и отпустите в диапазоне {lo:F0}–{hi:F0}%.";
                case 2: return $"Этап 2/3: узкая зона {lo:F0}–{hi:F0}%. Клапан срабатывает раньше!";
                default: return $"Этап 3/3: снайперская точность {lo:F0}–{hi:F0}%. Одно движение — и всё.";
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _currentValue = 0f;
            _isLocked = false;

            if (_holdButton != null) _holdButton.ResetButton();

            UpdateNeedle();

            if (_instructionText != null) _instructionText.text = GetStageInstruction(CurrentStage);
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void Update()
        {
            if (_isCompleted || _isLocked || IsInTransition) return;

            int idx = Mathf.Clamp(CurrentStage - 1, 0, _stageFillRate.Length - 1);
            float fillRate = _stageFillRate[idx];
            float decayRate = _stageDecayRate[idx];
            float blowout = _stageBlowout[idx];

            if (_holdButton != null && _holdButton.IsPressed)
            {
                _currentValue += fillRate * Time.unscaledDeltaTime;

                if (_currentValue >= blowout)
                {
                    _currentValue = 0f;
                    _isLocked = true;
                    UpdateNeedle();
                    FailStage($"Взрыв клапана: давление превысило {(blowout * 100f):F0}%.");
                    return;
                }
            }
            else
            {
                if (_currentValue > 0f)
                {
                    _currentValue -= decayRate * Time.unscaledDeltaTime;
                    _currentValue = Mathf.Max(0f, _currentValue);
                }
            }

            UpdateNeedle();
            SetProgress(_currentValue);
        }

        private void HandleButtonReleased()
        {
            if (_isCompleted || _isLocked || IsInTransition) return;

            float lo = TargetMin;
            float hi = TargetMax;

            if (_currentValue >= lo && _currentValue <= hi)
            {
                _isLocked = true;
                CompleteMechanic();
                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Идеально ({(_currentValue * 100f):F0}%). Этап {CurrentStage}/3 пройден!</color>";
                }
                return;
            }

            if (_currentValue > hi)
            {
                // Перебор при отпускании — провал этапа.
                _isLocked = true;
                FailStage($"Перебор давления: {(_currentValue * 100f):F0}% (нужно ≤ {(hi * 100f):F0}%).");
                return;
            }

            // Недобор — предупреждение, давление продолжит стекать, попытку можно повторить.
            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#FFCC00>Недобор ({(_currentValue * 100f):F0}%). Нужно минимум {(lo * 100f):F0}%.</color>";
            }
        }

        private void UpdateNeedle()
        {
            if (_needleTransform != null)
            {
                float angle = Mathf.Lerp(_minNeedleAngle, _maxNeedleAngle, _currentValue);
                _needleTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (_pressureDisplay != null)
            {
                _pressureDisplay.text = $"{(_currentValue * 100f):F0}%";
                _pressureDisplay.color = (_currentValue >= TargetMin && _currentValue <= TargetMax)
                    ? new Color(0.2f, 1f, 0.4f)
                    : Color.white;
            }
        }
    }
}
