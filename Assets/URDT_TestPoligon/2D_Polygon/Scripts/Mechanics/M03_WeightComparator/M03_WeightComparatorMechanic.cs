using UnityEngine;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M03_WeightComparator
{
    /// <summary>
    /// Механика #3: Балансировка массы / Весовой компаратор (Weight Threshold / Comparator).
    /// Задача: уравновесить чаши весов, набрав на правой чаше точную массу (15 кг).
    /// Мешающие факторы:
    /// 1. Бракованная гиря (Weight_Junk), имеющая нулевую массу или ложный вес.
    /// 2. Инерция рычага весов (требуется удержание равновесия 1 секунду).
    /// </summary>
    public class M03_WeightComparatorMechanic : BaseMechanic2DModule
    {
        [Header("Компоненты весов")]
        [SerializeField] private RectTransform _beamTransform = null;
        [SerializeField] private ScalePan _leftPan = null;
        [SerializeField] private ScalePan _rightPan = null;
        [SerializeField] private WeightItem[] _weights = null;

        [Header("Параметры балансировки")]
        [SerializeField] private float _targetMass = 15f;
        [SerializeField] private float _massTolerance = 0.5f;
        [SerializeField] private float _tiltFactor = 1.6f;
        [SerializeField] private float _maxTiltAngle = 18f;
        [SerializeField] private float _requiredStableTime = 1.0f;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _instructionLabel = null;

        private float _stableTimer = 0f;
        private float _currentBeamAngle = 0f;

        protected override void Awake()
        {
            base.Awake();
            BindEvents();
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        private void Update()
        {
            if (_isCompleted) return;

            float leftMass = _leftPan != null ? _leftPan.CurrentMass : _targetMass;
            if (leftMass <= 0f) leftMass = _targetMass;
            float rightMass = _rightPan != null ? _rightPan.CurrentMass : 0f;

            float massDelta = rightMass - leftMass;
            float targetAngle = Mathf.Clamp(massDelta * _tiltFactor, -_maxTiltAngle, _maxTiltAngle);

            _currentBeamAngle = Mathf.Lerp(_currentBeamAngle, targetAngle, Time.unscaledDeltaTime * 6f);
            if (_beamTransform != null)
            {
                _beamTransform.localRotation = Quaternion.Euler(0f, 0f, -_currentBeamAngle);
            }

            // Проверка баланса
            bool isBalanced = Mathf.Abs(massDelta) <= _massTolerance;
            if (isBalanced && rightMass > 0f)
            {
                _stableTimer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(_stableTimer / _requiredStableTime);
                SetProgress(0.5f + progress * 0.5f);

                if (_instructionLabel != null)
                {
                    _instructionLabel.text = $"<color=#00FF88>Весы в идеальном балансе (15 кг = 15 кг)! Фиксация: {progress * 100f:F0}%</color>";
                }

                if (_stableTimer >= _requiredStableTime)
                {
                    CompleteMechanic();
                    if (_instructionLabel != null)
                    {
                        _instructionLabel.text = "<color=#00FF99>Идеальный баланс достигнут! Механика успешно пройдена.</color>";
                    }
                }
            }
            else
            {
                _stableTimer = 0f;
                float massProgress = Mathf.Clamp01(rightMass / _targetMass);
                SetProgress(massProgress * 0.5f);

                if (_instructionLabel != null)
                {
                    if (rightMass > _targetMass)
                    {
                        _instructionLabel.text = $"<color=#FF6666>Перегруз! На правой чаше {rightMass:F0} кг (нужно ровно {_targetMass:F0} кг)</color>";
                    }
                    else if (rightMass == 0f)
                    {
                        _instructionLabel.text = $"Уравновесьте весы: {_targetMass:F0} кг слева против 0 кг справа. Перетащите гири на правую чашу!";
                    }
                    else
                    {
                        float remaining = _targetMass - rightMass;
                        _instructionLabel.text = $"На правой чаше {rightMass:F0} кг из {_targetMass:F0} кг (осталось набрать {remaining:F0} кг).";
                    }
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _stableTimer = 0f;
            _currentBeamAngle = 0f;

            if (_leftPan != null)
            {
                _leftPan.SetBaseMass(_targetMass);
            }

            if (_rightPan != null)
            {
                _rightPan.ResetPan();
            }

            if (_weights != null)
            {
                foreach (var w in _weights)
                {
                    if (w != null)
                    {
                        w.transform.localScale = Vector3.one;
                        w.ResetWeight();
                    }
                }
            }

            if (_instructionLabel != null)
            {
                _instructionLabel.text = $"Уравновесьте весы. Наберите ровно {_targetMass:F0} кг на правой чаше (10 кг + 5 кг).";
            }

            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void BindEvents()
        {
            if (_rightPan != null) _rightPan.OnMassChanged += HandleMassChanged;
        }

        private void UnbindEvents()
        {
            if (_rightPan != null) _rightPan.OnMassChanged -= HandleMassChanged;
        }

        private void HandleMassChanged(ScalePan pan, float mass)
        {
            // Handled in Update
        }
    }
}
