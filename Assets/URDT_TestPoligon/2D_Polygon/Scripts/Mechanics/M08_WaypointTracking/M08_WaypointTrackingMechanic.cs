using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M08_WaypointTracking
{
    /// <summary>
    /// Механика #8: Трассировка пути по путевым точкам (Waypoint Tracking).
    /// Задача: непрерывно провести пилу по доске через 5 контрольных точек слева направо.
    /// Мешающие факторы:
    /// 1. Сучок с гвоздем (Hazard Marker 'X') на доске — при приближении к нему прогресс сбрасывается.
    /// 2. Требование строгого соблюдения последовательности прохождения точек.
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
                // Подсветить первую точку
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

            if (_instructionText != null)
            {
                _instructionText.text = "Проведите пилу через контрольные точки 1 -> 5. Избегайте сучка с гвоздем (X)!";
            }

            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void HandleToolMoved(Vector2 toolPos)
        {
            if (_isCompleted || _waypoints == null || _waypoints.Length == 0) return;

            // Проверка столкновения с опасным сучком
            if (_hazardObstacle != null)
            {
                float distToHazard = Vector2.Distance(toolPos, _hazardObstacle.anchoredPosition);
                if (distToHazard < _hazardRadius)
                {
                    // Штраф
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF4444>Пила задела гвоздь (X)! Распил сорван, возврат на старт.</color>";
                    }
                    Initialize();
                    return;
                }
            }

            // Проверка достижения следующей путевой точки
            if (_currentWaypointIndex < _waypoints.Length)
            {
                float distToCurrent = Vector2.Distance(toolPos, _waypoints[_currentWaypointIndex]);
                if (distToCurrent <= _waypointReachRadius)
                {
                    // Отметить пройденную точку зеленым
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
                        if (_instructionText != null)
                        {
                            _instructionText.text = "<color=#00FF99>Доска успешно и ровно распилена!</color>";
                        }
                    }
                    else
                    {
                        // Подсветить следующую точку
                        if (_waypointMarkers[_currentWaypointIndex] != null)
                        {
                            var img = _waypointMarkers[_currentWaypointIndex].GetComponent<Image>();
                            if (img != null) img.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
                        }

                        if (_instructionText != null)
                        {
                            _instructionText.text = $"Точка {_currentWaypointIndex}/{_waypoints.Length} пройдена! Ведите дальше.";
                        }
                    }
                }
            }
        }

        private void HandleToolReleased()
        {
            if (!_isCompleted && _currentWaypointIndex < _waypoints.Length)
            {
                // Если бросил пилу на полпути
                if (_instructionText != null)
                {
                    _instructionText.text = "Не отпускайте пилу до завершения распила!";
                }
            }
        }
    }
}
