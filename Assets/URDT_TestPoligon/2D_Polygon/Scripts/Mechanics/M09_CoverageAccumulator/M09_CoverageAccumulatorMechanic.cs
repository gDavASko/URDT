using UnityEngine;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M09_CoverageAccumulator
{
    /// <summary>
    /// Механика #9: Накопление покрытия площади / Стирание маски (Coverage Accumulator).
    /// Задача: оттереть губкой грязь с металлической пластины (минимум 90% площади).
    /// Мешающие факторы:
    /// 1. Несмываемый поврежденный дефект (Hazard Cell) со знаком 'X' (не поддается стиранию).
    /// 2. Высокий порог чистоты (требуется пройти почти всю поверхность).
    /// </summary>
    public class M09_CoverageAccumulatorMechanic : BaseMechanic2DModule
    {
        [Header("Компоненты")]
        [SerializeField] private SpongeBrush _brush = null;
        [SerializeField] private DirtCell[] _dirtCells = null;
        [SerializeField] private float _requiredCleanRatio = 0.90f;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _instructionText = null;

        private int _totalCleanable = 0;
        private int _cleanedCount = 0;

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

        public override void Initialize()
        {
            base.Initialize();
            _cleanedCount = 0;
            _totalCleanable = 0;

            if (_dirtCells != null)
            {
                foreach (var cell in _dirtCells)
                {
                    if (cell != null)
                    {
                        cell.ResetCell();
                        if (!cell.IsPermanentHazard) _totalCleanable++;
                    }
                }
            }

            if (_brush != null)
            {
                _brush.ResetBrush();
            }

            if (_instructionText != null)
            {
                _instructionText.text = "Возьмите губку и круговыми движениями сотрите грязь (цель: 90%).";
            }

            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void HandleBrushMoved(Vector2 brushWorldPos)
        {
            if (_isCompleted || _dirtCells == null || _totalCleanable == 0) return;

            float radius = _brush != null ? _brush.BrushRadius : 40f;

            foreach (var cell in _dirtCells)
            {
                if (cell == null || cell.IsCleaned || cell.IsPermanentHazard) continue;

                float dist = Vector2.Distance(brushWorldPos, cell.transform.position);
                if (dist <= radius)
                {
                    if (cell.TryClean())
                    {
                        _cleanedCount++;
                    }
                }
            }

            float currentRatio = (float)_cleanedCount / _totalCleanable;
            float progress = Mathf.Clamp01(currentRatio / _requiredCleanRatio);
            SetProgress(progress);

            if (currentRatio >= _requiredCleanRatio)
            {
                CompleteMechanic();
                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Поверхность очищена на {(currentRatio * 100f):F0}%! Идеальная чистота.</color>";
                }
            }
            else
            {
                if (_instructionText != null)
                {
                    _instructionText.text = $"Очищено: {(currentRatio * 100f):F0}% / {(_requiredCleanRatio * 100f):F0}%";
                }
            }
        }
    }
}
