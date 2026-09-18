using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M10_TimedAccumulator
{
    /// <summary>
    /// Механика #10: Временной интегратор с контролем диапазона (Timed Accumulator / Hold).
    /// Задача: удерживать кнопку насоса, чтобы накачать давление в зеленую зону (70% - 85%), и вовремя отпустить!
    /// Мешающие факторы:
    /// 1. Взрывной сброс предохранительного клапана при перекачке (>90%).
    /// 2. Постепенная утечка давления при раннем отпускании кнопки.
    /// </summary>
    public class M10_TimedAccumulatorMechanic : BaseMechanic2DModule
    {
        [Header("Компоненты манометра")]
        [SerializeField] private RectTransform _needleTransform = null;
        [SerializeField] private HoldButton _holdButton = null;
        [SerializeField] private TMP_Text _pressureDisplay = null;
        [SerializeField] private TMP_Text _instructionText = null;

        [Header("Параметры накачки")]
        [SerializeField] private float _fillRate = 0.35f;
        [SerializeField] private float _decayRate = 0.20f;
        [SerializeField] private float _targetMin = 0.70f;
        [SerializeField] private float _targetMax = 0.85f;
        [SerializeField] private float _blowoutThreshold = 0.92f;
        [SerializeField] private float _minNeedleAngle = 120f;
        [SerializeField] private float _maxNeedleAngle = -120f;

        private float _currentValue = 0f;
        private float _lockTimer = 0f;
        private bool _isLocked = false;

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

        public override void Initialize()
        {
            base.Initialize();
            _currentValue = 0f;
            _lockTimer = 0f;
            _isLocked = false;

            if (_holdButton != null)
            {
                _holdButton.ResetButton();
            }

            UpdateNeedle();

            if (_instructionText != null)
            {
                _instructionText.text = "Зажмите кнопку «НАКАЧКА» и отпустите в зеленой зоне (70-85%). Не перекачивайте!";
            }

            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void Update()
        {
            if (_isCompleted || _isLocked) return;

            if (_holdButton != null && _holdButton.IsPressed)
            {
                _currentValue += _fillRate * Time.unscaledDeltaTime;

                // Проверка переполнения / срыва клапана
                if (_currentValue >= _blowoutThreshold)
                {
                    _currentValue = 0f;
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF4444>Взрыв клапана! Перекачка выше 90%. Давление сброшено.</color>";
                    }
                }
            }
            else
            {
                // Затухание / утечка давления
                if (_currentValue > 0f)
                {
                    _currentValue -= _decayRate * Time.unscaledDeltaTime;
                    _currentValue = Mathf.Max(0f, _currentValue);
                }
            }

            UpdateNeedle();
            SetProgress(_currentValue);
        }

        private void HandleButtonReleased()
        {
            if (_isCompleted || _isLocked) return;

            // Проверка попадания в целевой диапазон
            if (_currentValue >= _targetMin && _currentValue <= _targetMax)
            {
                _isLocked = true;
                CompleteMechanic();
                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Идеальное давление ({(_currentValue * 100f):F0}%) зафиксировано! Успех.</color>";
                }
            }
            else
            {
                if (_currentValue < _targetMin)
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = $"<color=#FFCC00>Недобор давления ({(_currentValue * 100f):F0}%). Нужно минимум 70%!</color>";
                    }
                }
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
                _pressureDisplay.color = (_currentValue >= _targetMin && _currentValue <= _targetMax)
                    ? new Color(0.2f, 1f, 0.4f)
                    : Color.white;
            }
        }
    }
}
