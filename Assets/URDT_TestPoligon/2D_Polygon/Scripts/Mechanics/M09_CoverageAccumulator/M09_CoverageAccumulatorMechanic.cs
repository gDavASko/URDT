using System.Collections.Generic;
using UnityEngine;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M09_CoverageAccumulator
{
    /// <summary>
    /// Механика #9: Накопление покрытия площади / Стирание маски (Coverage Accumulator).
    /// Три этапа: с ростом сложности повышается требуемая чистота, добавляется грязь и вводится
    /// лимит времени на этап; провал по таймеру откатывает прогресс этапа.
    /// </summary>
    public class M09_CoverageAccumulatorMechanic : BaseMechanic2DModule
    {
        [Header("Компоненты")]
        [SerializeField] private SpongeBrush _brush = null;
        [SerializeField] private DirtCell[] _dirtCells = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _instructionText = null;

        // Параметры по этапам
        private static readonly float[] _stageCleanRatio = { 0.90f, 0.94f, 0.97f };
        private static readonly int[] _stageExtraCells = { 0, 4, 8 };
        private static readonly float[] _stageTimeLimit = { 0f, 45f, 35f };

        private readonly List<DirtCell> _activeCells = new List<DirtCell>();
        private readonly List<GameObject> _spawnedCells = new List<GameObject>();

        private int _totalCleanable = 0;
        private int _cleanedCount = 0;
        private float _timeRemaining = 0f;
        private bool _hasTimeLimit = false;

        public override int StageCount => 3;

        public float RequiredCleanRatio => _stageCleanRatio[Mathf.Clamp(CurrentStage - 1, 0, _stageCleanRatio.Length - 1)];
        public float CleanedRatio => _totalCleanable > 0 ? (float)_cleanedCount / _totalCleanable : 0f;
        public float TimeRemaining => _timeRemaining;
        public bool HasTimeLimit => _hasTimeLimit;

        protected override void Awake()
        {
            base.Awake();
            BindEvents();
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        private void BindEvents()
        {
            if (_brush != null) _brush.OnBrushMoved += HandleBrushMoved;
        }

        private void UnbindEvents()
        {
            if (_brush != null) _brush.OnBrushMoved -= HandleBrushMoved;
        }

        protected override string GetStageInstruction(int stage)
        {
            float ratioPct = _stageCleanRatio[Mathf.Clamp(stage - 1, 0, _stageCleanRatio.Length - 1)] * 100f;
            switch (stage)
            {
                case 1:
                    return $"Этап 1/3: возьмите губку и сотрите грязь (цель: {ratioPct:F0}%).";
                case 2:
                    return $"Этап 2/3: пятен больше — очистите не менее {ratioPct:F0}% за 45 секунд.";
                default:
                    return $"Этап 3/3: генеральная уборка — {ratioPct:F0}% чистоты за 35 секунд.";
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _cleanedCount = 0;
            _totalCleanable = 0;
            _activeCells.Clear();

            // Уничтожаем клоны предыдущего этапа/попытки
            for (int i = 0; i < _spawnedCells.Count; i++)
            {
                if (_spawnedCells[i] != null) Destroy(_spawnedCells[i]);
            }
            _spawnedCells.Clear();

            // Сбрасываем авторские ячейки
            if (_dirtCells != null)
            {
                foreach (var cell in _dirtCells)
                {
                    if (cell == null) continue;
                    cell.gameObject.SetActive(true);
                    cell.ResetCell();
                    _activeCells.Add(cell);
                }
            }

            // Клонируем дополнительные ячейки под текущий этап
            int extra = _stageExtraCells[Mathf.Clamp(CurrentStage - 1, 0, _stageExtraCells.Length - 1)];
            DirtCell template = FindCleanableTemplate();
            if (template != null && extra > 0)
            {
                RectTransform templateRt = template.transform as RectTransform;
                Vector2 basePos = templateRt != null ? templateRt.anchoredPosition : Vector2.zero;
                for (int i = 0; i < extra; i++)
                {
                    DirtCell clone = Instantiate(template, template.transform.parent);
                    clone.name = $"DirtCell_Extra_{i + 1:00}";
                    clone.SetHazard(false);
                    RectTransform crt = clone.transform as RectTransform;
                    if (crt != null)
                    {
                        float ang = (i / (float)Mathf.Max(extra, 1)) * Mathf.PI * 2f;
                        float radius = 90f + (i % 3) * 30f;
                        crt.anchoredPosition = basePos + new Vector2(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius);
                    }
                    clone.ResetCell();
                    _activeCells.Add(clone);
                    _spawnedCells.Add(clone.gameObject);
                    Urdt2DBeaconUtility.InstrumentGameObject(clone.gameObject);
                }
            }

            foreach (var c in _activeCells)
            {
                if (c != null && !c.IsPermanentHazard) _totalCleanable++;
            }

            if (_brush != null) _brush.ResetBrush();

            _hasTimeLimit = _stageTimeLimit[Mathf.Clamp(CurrentStage - 1, 0, _stageTimeLimit.Length - 1)] > 0f;
            _timeRemaining = _stageTimeLimit[Mathf.Clamp(CurrentStage - 1, 0, _stageTimeLimit.Length - 1)];

            if (_instructionText != null) _instructionText.text = GetStageInstruction(CurrentStage);
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private DirtCell FindCleanableTemplate()
        {
            if (_dirtCells == null) return null;
            foreach (var c in _dirtCells)
            {
                if (c != null && !c.IsPermanentHazard) return c;
            }
            return null;
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;
            if (!_hasTimeLimit) return;
            _timeRemaining -= Time.unscaledDeltaTime;
            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                FailStage("Время на этап истекло — не успели отчистить поверхность.");
            }
        }

        private void HandleBrushMoved(Vector2 brushWorldPos)
        {
            if (_isCompleted || IsInTransition || _totalCleanable == 0) return;

            float radius = _brush != null ? _brush.BrushRadius : 40f;

            for (int i = 0; i < _activeCells.Count; i++)
            {
                var cell = _activeCells[i];
                if (cell == null || cell.IsCleaned || cell.IsPermanentHazard) continue;
                float dist = Vector2.Distance(brushWorldPos, cell.transform.position);
                if (dist <= radius && cell.TryClean())
                {
                    _cleanedCount++;
                }
            }

            float currentRatio = (float)_cleanedCount / _totalCleanable;
            float required = RequiredCleanRatio;
            float progress = Mathf.Clamp01(currentRatio / required);
            SetProgress(progress);

            if (currentRatio >= required)
            {
                CompleteMechanic();
                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Этап {CurrentStage} пройден: {(currentRatio * 100f):F0}% чистоты!</color>";
                }
            }
            else
            {
                if (_instructionText != null)
                {
                    string timer = _hasTimeLimit ? $"  ⏱ {_timeRemaining:F0}с" : string.Empty;
                    _instructionText.text = $"Этап {CurrentStage}/3 — {(currentRatio * 100f):F0}% / {(required * 100f):F0}%{timer}";
                }
            }
        }
    }
}
