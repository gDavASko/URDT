using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M08_WaypointTracking
{
    /// <summary>
    /// Механика #8: Трассировка пути по путевым точкам (Waypoint Tracking).
    /// Три этапа: растущее число опасных сучков и увеличенный радиус их зоны.
    /// Провал: пила задевает сучок с гвоздём (X) — распил сорван, возврат на старт этапа.
    /// </summary>
    public class M08_WaypointTrackingMechanic : BaseMechanic2DModule
    {
        [Header("Инструмент и трасса")]
        [SerializeField] private WaypointTrackerTool _tool = null;
        [SerializeField] private RectTransform[] _waypointMarkers = null;
        [SerializeField] private RectTransform _hazardObstacle = null;
        [SerializeField] private float _hazardRadius = 50f;
        [SerializeField] private float _waypointReachRadius = 45f;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _instructionText = null;

        private int _currentWaypointIndex = 0;
        private Vector2[] _waypoints;
        private readonly List<RectTransform> _extraHazards = new List<RectTransform>();

        public override int StageCount => 3;
        public int CurrentWaypointIndex => _currentWaypointIndex;
        public int WaypointCount => _waypoints != null ? _waypoints.Length : 0;

        protected override string GetStageInstruction(int stage)
        {
            switch (stage)
            {
                case 1: return "Этап 1/3. Проведите пилу через контрольные точки 1 → 5. Избегайте сучка с гвоздём (X).";
                case 2: return "Этап 2/3. На доске стало больше сучков с гвоздями. Задели один — распил сорван.";
                default: return "Этап 3/3. Максимум опасных участков и повышенный радиус срыва. Ведите пилу максимально аккуратно.";
            }
        }

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
            if (_tool != null)
            {
                _tool.OnPositionMoved += HandleToolMoved;
                _tool.OnDragCancelled += HandleToolReleased;
            }
        }

        private void UnbindEvents()
        {
            if (_tool != null)
            {
                _tool.OnPositionMoved -= HandleToolMoved;
                _tool.OnDragCancelled -= HandleToolReleased;
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _currentWaypointIndex = 0;

            // Удалить дополнительные препятствия предыдущего этапа
            for (int i = _extraHazards.Count - 1; i >= 0; i--)
            {
                if (_extraHazards[i] != null) Destroy(_extraHazards[i].gameObject);
            }
            _extraHazards.Clear();

            if (_waypointMarkers != null)
            {
                _waypoints = new Vector2[_waypointMarkers.Length];
                for (int i = 0; i < _waypointMarkers.Length; i++)
                {
                    if (_waypointMarkers[i] != null)
                    {
                        _waypoints[i] = _waypointMarkers[i].anchoredPosition;
                        var img = _waypointMarkers[i].GetComponent<Image>();
                        if (img != null) img.color = new Color(0.3f, 0.5f, 0.7f, 0.5f);
                    }
                }
                if (_waypointMarkers.Length > 0 && _waypointMarkers[0] != null)
                {
                    var img = _waypointMarkers[0].GetComponent<Image>();
                    if (img != null) img.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
                }
            }

            if (_tool != null)
            {
                _tool.ResetTool();
            }

            SpawnStageExtras(CurrentStage);

            SetProgress(0f);
        }

        private void SpawnStageExtras(int stage)
        {
            if (stage < 2 || _hazardObstacle == null) return;

            int extras = stage == 2 ? 1 : 2;
            Vector2 basePos = _hazardObstacle.anchoredPosition;

            for (int i = 0; i < extras; i++)
            {
                var clone = Instantiate(_hazardObstacle.gameObject, _hazardObstacle.parent);
                clone.name = $"Hazard_Marker_Extra_S{stage}_{i + 1}";
                var rt = clone.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = basePos + new Vector2((i % 2 == 0 ? -140f : 140f) - i * 20f, (i + 1) * 15f);
                    _extraHazards.Add(rt);
                }
            }
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void HandleToolMoved(Vector2 toolPos)
        {
            if (_isCompleted || IsInTransition || _waypoints == null || _waypoints.Length == 0) return;

            float effectiveRadius = _hazardRadius * (CurrentStage >= 3 ? 1.25f : 1f);

            if (_hazardObstacle != null)
            {
                float distToHazard = Vector2.Distance(toolPos, _hazardObstacle.anchoredPosition);
                if (distToHazard < effectiveRadius)
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF4444>Пила задела гвоздь (X)! Этап начнётся заново.</color>";
                    }
                    FailStage("Пила задела сучок с гвоздём");
                    return;
                }
            }

            foreach (var extra in _extraHazards)
            {
                if (extra == null) continue;
                if (Vector2.Distance(toolPos, extra.anchoredPosition) < effectiveRadius)
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF4444>Пила задела дополнительный сучок (X)! Этап начнётся заново.</color>";
                    }
                    FailStage("Пила задела дополнительный сучок с гвоздём");
                    return;
                }
            }

            if (_currentWaypointIndex < _waypoints.Length)
            {
                float distToCurrent = Vector2.Distance(toolPos, _waypoints[_currentWaypointIndex]);
                if (distToCurrent <= _waypointReachRadius)
                {
                    if (_waypointMarkers[_currentWaypointIndex] != null)
                    {
                        var img = _waypointMarkers[_currentWaypointIndex].GetComponent<Image>();
                        if (img != null) img.color = new Color(0.1f, 1f, 0.4f, 1f);
                    }

                    _currentWaypointIndex++;
                    float progress = (float)_currentWaypointIndex / _waypoints.Length;
                    SetProgress(progress);

                    if (_currentWaypointIndex >= _waypoints.Length)
                    {
                        CompleteMechanic();
                        if (_instructionText != null && !IsInTransition)
                        {
                            _instructionText.text = "<color=#00FF99>Распил завершён!</color>";
                        }
                    }
                    else
                    {
                        if (_waypointMarkers[_currentWaypointIndex] != null)
                        {
                            var img = _waypointMarkers[_currentWaypointIndex].GetComponent<Image>();
                            if (img != null) img.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
                        }

                        if (_instructionText != null)
                        {
                            _instructionText.text = $"Точка {_currentWaypointIndex}/{_waypoints.Length} пройдена!";
                        }
                    }
                }
            }
        }

        private void HandleToolReleased()
        {
            if (!_isCompleted && _waypoints != null && _currentWaypointIndex < _waypoints.Length && !IsInTransition)
            {
                if (_instructionText != null)
                {
                    _instructionText.text = "Не отпускайте пилу до завершения распила!";
                }
            }
        }
    }
}
