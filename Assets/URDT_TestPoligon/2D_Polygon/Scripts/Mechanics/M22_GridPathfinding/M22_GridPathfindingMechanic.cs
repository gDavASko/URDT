using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M22_GridPathfinding
{
    /// <summary>
    /// Механика #22: Навигация сущности по дискретной тайловой сетке (Grid Pathfinding).
    /// Задача: провести робота от старта (0, 4) к флагу финиша (4, 0), обходя стены и ловушки.
    /// Мешающие факторы:
    /// 1. Электро-ловушка [X] на ячейке (1, 1) — заклинивает робота на 1 секунду.
    /// 2. Непроходимые стены-препятствия, требующие построения обходного пути.
    /// </summary>
    public class M22_GridPathfindingMechanic : BaseMechanic2DModule
    {
        [Header("Сетка")]
        [SerializeField] private GridTileButton[] _gridTiles = null;
        [SerializeField] private int _gridSize = 5;
        [SerializeField] private RectTransform _robotAvatar = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _stepText = null;
        [SerializeField] private TMP_Text _instructionText = null;

        private Vector2Int _robotPos = new Vector2Int(0, 4);
        private Vector2Int _goalPos = new Vector2Int(4, 0);
        private Vector2Int _trapPos = new Vector2Int(1, 1);
        private HashSet<Vector2Int> _walls = new HashSet<Vector2Int>
        {
            new Vector2Int(1, 3),
            new Vector2Int(2, 3),
            new Vector2Int(2, 1),
            new Vector2Int(3, 1)
        };

        private int _stepsTaken = 0;
        private bool _isTrapped = false;
        private float _trapTimer = 0f;
        private bool _visualsRefreshed = false;

        protected override void Awake()
        {
            base.Awake();
            InitializeTileCoordinates();
            RefreshAllTilesVisuals();

            if (_robotAvatar != null)
            {
                var rImg = _robotAvatar.GetComponent<Image>();
                if (rImg != null) rImg.raycastTarget = false;
            }
        }

        private void Start()
        {
            RefreshAllTilesVisuals();
        }

        private void OnEnable()
        {
            RefreshAllTilesVisuals();
        }

        private void OnDestroy()
        {
            if (_gridTiles != null)
            {
                foreach (var t in _gridTiles) if (t != null) t.OnTileClicked -= HandleTileClicked;
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            InitializeTileCoordinates();
            RefreshAllTilesVisuals();

            _robotPos = new Vector2Int(0, 4);
            _stepsTaken = 0;
            _isTrapped = false;
            _trapTimer = 0f;

            if (_robotAvatar != null)
            {
                var rImg = _robotAvatar.GetComponent<Image>();
                if (rImg != null) rImg.raycastTarget = false;
            }

            UpdateRobotVisual();
            UpdateUI();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void InitializeTileCoordinates()
        {
            if (_gridTiles != null)
            {
                for (int i = 0; i < _gridTiles.Length; i++)
                {
                    if (_gridTiles[i] != null)
                    {
                        _gridTiles[i].CellX = i % _gridSize;
                        _gridTiles[i].CellY = i / _gridSize;
                        _gridTiles[i].OnTileClicked -= HandleTileClicked;
                        _gridTiles[i].OnTileClicked += HandleTileClicked;
                    }
                }
            }
        }

        public void RefreshAllTilesVisuals()
        {
            if (_gridTiles == null) return;
            for (int i = 0; i < _gridTiles.Length; i++)
            {
                var tile = _gridTiles[i];
                if (tile == null) continue;
                int x = i % _gridSize;
                int y = i / _gridSize;
                Vector2Int pos = new Vector2Int(x, y);

                GridCellType type = GridCellType.Walkable;
                if (_walls.Contains(pos))
                {
                    type = GridCellType.ObstacleWall;
                }
                else if (pos == _trapPos)
                {
                    type = GridCellType.GlitchTrapHazard;
                }
                else if (pos == _goalPos)
                {
                    type = GridCellType.Goal;
                }

                tile.ApplyCellType(type);
            }
        }

        private void HandleTileClicked(GridTileButton tile)
        {
            if (tile == null) return;
            TryMoveTo(new Vector2Int(tile.CellX, tile.CellY));
        }

        public void TryMoveTo(Vector2Int targetPos)
        {
            if (_isCompleted || _isTrapped) return;
            if (targetPos.x < 0 || targetPos.x >= _gridSize || targetPos.y < 0 || targetPos.y >= _gridSize) return;

            // Проверка, является ли ячейка соседней по горизонтали или вертикали
            int dist = Mathf.Abs(targetPos.x - _robotPos.x) + Mathf.Abs(targetPos.y - _robotPos.y);
            if (dist == 1)
            {
                // Проверка на стену
                if (_walls.Contains(targetPos))
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF5555>Препятствие! Робот не может пройти сквозь стену.</color>";
                    }
                    return;
                }

                // Перемещение на ячейку
                _robotPos = targetPos;
                _stepsTaken++;
                UpdateRobotVisual();
                UpdateUI();

                // Проверка ловушки
                if (_robotPos == _trapPos)
                {
                    _isTrapped = true;
                    _trapTimer = 1.0f;
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF4444>Внимание! Вы наступили в глитч-ловушку [X]! Робот перезагружается...</color>";
                    }
                }

                // Проверка финиша
                if (_robotPos == _goalPos)
                {
                    CompleteMechanic();
                    SetProgress(1f);
                    if (_instructionText != null)
                    {
                        _instructionText.text = $"<color=#00FF99>Цель достигнута за {_stepsTaken} шагов! Маршрут пройден!</color>";
                    }
                }
                else
                {
                    // Оценка прогресса (приближение к цели Манхэттенским расстоянием)
                    int remainingDist = Mathf.Abs(_robotPos.x - _goalPos.x) + Mathf.Abs(_robotPos.y - _goalPos.y);
                    int totalDist = 8;
                    float progress = Mathf.Clamp01(1f - (float)remainingDist / totalDist);
                    SetProgress(progress);
                }
            }
            else
            {
                if (_instructionText != null)
                {
                    _instructionText.text = "Нажимайте на соседнюю доступную ячейку (сверху, снизу, слева или справа).";
                }
            }
        }

        private void Update()
        {
            if (!_visualsRefreshed)
            {
                _visualsRefreshed = true;
                RefreshAllTilesVisuals();
            }

            if (_isTrapped)
            {
                _trapTimer -= Time.unscaledDeltaTime;
                if (_trapTimer <= 0f)
                {
                    _isTrapped = false;
                    if (_instructionText != null)
                    {
                        _instructionText.text = "Робот снова в строю! Продолжайте движение к флагу.";
                    }
                }
                return;
            }

            if (_isCompleted) return;

            // Управление клавишами (WASD и стрелки) для максимального удобства
            if (UnityEngine.Input.GetKeyDown(KeyCode.W) || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow)) TryMoveTo(new Vector2Int(_robotPos.x, _robotPos.y - 1));
            else if (UnityEngine.Input.GetKeyDown(KeyCode.S) || UnityEngine.Input.GetKeyDown(KeyCode.DownArrow)) TryMoveTo(new Vector2Int(_robotPos.x, _robotPos.y + 1));
            else if (UnityEngine.Input.GetKeyDown(KeyCode.A) || UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow)) TryMoveTo(new Vector2Int(_robotPos.x - 1, _robotPos.y));
            else if (UnityEngine.Input.GetKeyDown(KeyCode.D) || UnityEngine.Input.GetKeyDown(KeyCode.RightArrow)) TryMoveTo(new Vector2Int(_robotPos.x + 1, _robotPos.y));
        }

        private void UpdateRobotVisual()
        {
            if (_gridTiles == null || _robotAvatar == null) return;

            int index = _robotPos.y * _gridSize + _robotPos.x;
            if (index >= 0 && index < _gridTiles.Length && _gridTiles[index] != null)
            {
                _robotAvatar.anchoredPosition = _gridTiles[index].RectTransform.anchoredPosition;
            }
        }

        private void UpdateUI()
        {
            if (_stepText != null)
            {
                _stepText.text = $"Шаги: {_stepsTaken} | Позиция: ({_robotPos.x},{_robotPos.y})";
            }
        }
    }
}
