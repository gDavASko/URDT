using System.Collections.Generic;
using UnityEngine;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M03_WeightComparator
{
    /// <summary>
    /// Механика #3: Балансировка массы / Весовой компаратор.
    /// Три этапа с растущей эталонной массой и уменьшающимся допуском.
    /// Провал: положить на весы бракованную гирю (0 кг, X).
    /// </summary>
    public class M03_WeightComparatorMechanic : BaseMechanic2DModule
    {
        [Header("Компоненты весов")]
        [SerializeField] private RectTransform _beamTransform = null;
        [SerializeField] private ScalePan _leftPan = null;
        [SerializeField] private ScalePan _rightPan = null;
        [SerializeField] private WeightItem[] _weights = null;

        [Header("Базовые параметры балансировки")]
        [SerializeField] private float _targetMass = 15f;
        [SerializeField] private float _massTolerance = 0.5f;
        [SerializeField] private float _tiltFactor = 1.6f;
        [SerializeField] private float _maxTiltAngle = 18f;
        [SerializeField] private float _requiredStableTime = 1.0f;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _instructionLabel = null;

        private float _stableTimer = 0f;
        private float _currentBeamAngle = 0f;
        private float _stageTargetMass;
        private float _stageTolerance;
        private readonly List<GameObject> _spawnedExtras = new List<GameObject>();

        public override int StageCount => 3;
        public float StageTargetMass => _stageTargetMass;
        public float StageTolerance => _stageTolerance;
        public float CurrentRightMass => _rightPan != null ? _rightPan.CurrentMass : 0f;

        protected override void Awake()
        {
            base.Awake();
            // BindEvents уже вызван из Initialize через base.Awake -> Initialize -> BindEvents
        }

        protected override string GetStageInstruction(int stage)
        {
            float target = GetTargetForStage(stage);
            float tol = GetToleranceForStage(stage);
            switch (stage)
            {
                case 1: return $"Этап 1/3. Уравновесьте весы: наберите {target:F0} кг на правой чаше (допуск ±{tol:F1} кг).";
                case 2: return $"Этап 2/3. Более тяжёлый эталон: {target:F0} кг, допуск строже (±{tol:F1} кг).";
                default: return $"Этап 3/3. Тонкая настройка: {target:F0} кг, допуск ±{tol:F1} кг. Не используйте гирю (X).";
            }
        }

        private float GetTargetForStage(int stage)
        {
            switch (stage) { case 1: return _targetMass; case 2: return _targetMass + 5f; default: return _targetMass + 10f; }
        }
        private float GetToleranceForStage(int stage)
        {
            switch (stage) { case 1: return _massTolerance; case 2: return _massTolerance * 0.7f; default: return _massTolerance * 0.5f; }
        }

        private void OnDestroy()
        {
            UnbindEvents();
            UnbindWeightEvents();
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;

            float leftMass = _leftPan != null ? _leftPan.CurrentMass : _stageTargetMass;
            if (leftMass <= 0f) leftMass = _stageTargetMass;
            float rightMass = _rightPan != null ? _rightPan.CurrentMass : 0f;

            float massDelta = rightMass - leftMass;
            float targetAngle = Mathf.Clamp(massDelta * _tiltFactor, -_maxTiltAngle, _maxTiltAngle);

            _currentBeamAngle = Mathf.Lerp(_currentBeamAngle, targetAngle, Time.unscaledDeltaTime * 6f);
            if (_beamTransform != null)
            {
                _beamTransform.localRotation = Quaternion.Euler(0f, 0f, -_currentBeamAngle);
            }

            bool isBalanced = Mathf.Abs(massDelta) <= _stageTolerance;
            if (isBalanced && rightMass > 0f)
            {
                _stableTimer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(_stableTimer / _requiredStableTime);
                SetProgress(0.5f + progress * 0.5f);

                if (_instructionLabel != null)
                {
                    _instructionLabel.text = $"<color=#00FF88>Баланс достигнут ({_stageTargetMass:F0} кг = {rightMass:F0} кг)! Фиксация: {progress * 100f:F0}%</color>";
                }

                if (_stableTimer >= _requiredStableTime)
                {
                    CompleteMechanic();
                }
            }
            else
            {
                _stableTimer = 0f;
                float massProgress = Mathf.Clamp01(rightMass / _stageTargetMass);
                SetProgress(massProgress * 0.5f);

                if (_instructionLabel != null)
                {
                    if (rightMass > _stageTargetMass)
                    {
                        _instructionLabel.text = $"<color=#FF6666>Перегруз! На правой чаше {rightMass:F0} кг (нужно {_stageTargetMass:F0} кг ±{_stageTolerance:F1}).</color>";
                    }
                    else if (rightMass == 0f)
                    {
                        _instructionLabel.text = $"Уравновесьте весы: {_stageTargetMass:F0} кг слева. Перетаскивайте гири на правую чашу.";
                    }
                    else
                    {
                        _instructionLabel.text = $"Справа {rightMass:F0} кг из {_stageTargetMass:F0} кг.";
                    }
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _stableTimer = 0f;
            _currentBeamAngle = 0f;

            _stageTargetMass = GetTargetForStage(CurrentStage);
            _stageTolerance = GetToleranceForStage(CurrentStage);

            // Отвязать все события во избежание дублирования подписок
            UnbindEvents();
            UnbindWeightEvents();
            for (int i = _spawnedExtras.Count - 1; i >= 0; i--)
            {
                if (_spawnedExtras[i] != null) Destroy(_spawnedExtras[i]);
            }
            _spawnedExtras.Clear();

            if (_leftPan != null)
            {
                _leftPan.SetBaseMass(_stageTargetMass);
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

            SpawnStageExtras(CurrentStage);
            BindEvents();

            SetProgress(0f);
        }

        private void SpawnStageExtras(int stage)
        {
            if (stage < 2 || _weights == null || _weights.Length == 0) return;

            WeightItem realTemplate = null;
            WeightItem junkTemplate = null;
            foreach (var w in _weights)
            {
                if (w == null) continue;
                if (!w.IsJunk && realTemplate == null) realTemplate = w;
                if (w.IsJunk && junkTemplate == null) junkTemplate = w;
            }

            // Этап 2: +5 кг (одна дополнительная гиря 5 кг). Этап 3: +10 кг (гиря 10 кг) и +1 брак.
            if (realTemplate != null)
            {
                if (stage == 2)
                {
                    SpawnWeightClone(realTemplate, 5f, false, new Vector2(90f, 0f), $"Weight_Extra_5kg_S{stage}");
                }
                else if (stage >= 3)
                {
                    SpawnWeightClone(realTemplate, 10f, false, new Vector2(90f, 0f), $"Weight_Extra_10kg_S{stage}");
                    SpawnWeightClone(realTemplate, 5f, false, new Vector2(160f, 0f), $"Weight_Extra_5kg_S{stage}");
                }
            }

            if (junkTemplate != null && stage >= 3)
            {
                SpawnWeightClone(junkTemplate, 0f, true, new Vector2(-90f, 0f), $"Weight_Junk_Extra_S{stage}");
            }
        }

        private void SpawnWeightClone(WeightItem template, float mass, bool junk, Vector2 offset, string name)
        {
            var clone = Instantiate(template, template.transform.parent);
            clone.name = name;
            clone.SetMass(mass);
            clone.SetJunk(junk);
            clone.transform.localScale = Vector3.one;
            var rt = clone.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 basePos = template.GetComponent<RectTransform>().anchoredPosition;
                rt.anchoredPosition = basePos + offset;
                clone.SetHomePosition(rt.anchoredPosition);
            }
            _spawnedExtras.Add(clone.gameObject);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void BindEvents()
        {
            if (_rightPan != null) _rightPan.OnMassChanged += HandleMassChanged;
            BindWeightEvents();
        }

        private void UnbindEvents()
        {
            if (_rightPan != null) _rightPan.OnMassChanged -= HandleMassChanged;
        }

        private void BindWeightEvents()
        {
            if (_weights != null)
            {
                foreach (var w in _weights)
                {
                    if (w != null) w.OnPlacedOnPan += HandleWeightPlaced;
                }
            }
            foreach (var extra in _spawnedExtras)
            {
                if (extra == null) continue;
                var wi = extra.GetComponent<WeightItem>();
                if (wi != null) wi.OnPlacedOnPan += HandleWeightPlaced;
            }
        }

        private void UnbindWeightEvents()
        {
            if (_weights != null)
            {
                foreach (var w in _weights)
                {
                    if (w != null) w.OnPlacedOnPan -= HandleWeightPlaced;
                }
            }
            foreach (var extra in _spawnedExtras)
            {
                if (extra == null) continue;
                var wi = extra.GetComponent<WeightItem>();
                if (wi != null) wi.OnPlacedOnPan -= HandleWeightPlaced;
            }
        }

        private void HandleWeightPlaced(WeightItem item, ScalePan pan)
        {
            if (IsInTransition || _isCompleted) return;
            if (item != null && item.IsJunk && pan != null && pan.AcceptsDrop)
            {
                if (_instructionLabel != null)
                {
                    _instructionLabel.text = "<color=#FF5555>Бракованная гиря на весах! Этап провален.</color>";
                }
                FailStage("Бракованная гиря (X) поставлена на весы");
            }
        }

        private void HandleMassChanged(ScalePan pan, float mass)
        {
            // Балансировка проверяется в Update.
        }
    }
}
