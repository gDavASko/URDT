using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M18_GraphFlowClosure
{
    /// <summary>
    /// Механика #18: Дискретный поворот топологического узла (BFS Graph Flow Closure).
    /// Задача: кликами поворачивать фрагменты труб, чтобы соединить Источник (Source) и Приемник (Sink).
    /// Мешающие факторы:
    /// 1. Засоренная разбитая труба [X], не проводящая воду.
    /// 2. Необходимость правильного согласования взаимных входов соседних ячеек.
    /// </summary>
    public class M18_GraphFlowClosureMechanic : BaseMechanic2DModule
    {
        [Header("Сетка труб")]
        [SerializeField] private PipeTile[] _gridTiles = null;
        [SerializeField] private int _gridWidth = 3;
        [SerializeField] private int _gridHeight = 3;

        [Header("Спрайты труб")]
        [SerializeField] private Sprite _straightSprite = null;
        [SerializeField] private Sprite _cornerSprite = null;
        [SerializeField] private Sprite _sourceSprite = null;
        [SerializeField] private Sprite _sinkSprite = null;
        [SerializeField] private Sprite _junkSprite = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _statusText = null;
        [SerializeField] private TMP_Text _instructionText = null;

        private PipeTile _sourceTile;
        private PipeTile _sinkTile;
        private int _currentLayoutIndex = -1;

        // 5 гарантированно проходимых планировок (3x3 сетка)
        private static readonly PipeType[][] Layouts = new PipeType[][]
        {
            // Вариант 1: Обход центрального препятствия [X] по верху
            // Проходимый путь: (0,0)->(1,0)->(2,0)->(2,1)->(2,2)
            new PipeType[]
            {
                PipeType.Source,   PipeType.Straight,   PipeType.Corner,
                PipeType.Corner,   PipeType.BrokenJunk, PipeType.Straight,
                PipeType.Corner,   PipeType.Straight,   PipeType.Sink
            },

            // Вариант 2: Обход центрального препятствия [X] по низу
            // Проходимый путь: (0,0)->(0,1)->(0,2)->(1,2)->(2,2) (Источник направлен вниз)
            new PipeType[]
            {
                PipeType.Source,   PipeType.Straight,   PipeType.Corner,
                PipeType.Straight, PipeType.BrokenJunk, PipeType.Corner,
                PipeType.Corner,   PipeType.Straight,   PipeType.Sink
            },

            // Вариант 3: Змейка через центр (Препятствие [X] слева)
            // Проходимый путь: (0,0)->(1,0)->(2,0)->(2,1)->(1,1)->(1,2)->(2,2)
            new PipeType[]
            {
                PipeType.Source,     PipeType.Straight, PipeType.Corner,
                PipeType.BrokenJunk, PipeType.Corner,   PipeType.Corner,
                PipeType.Corner,     PipeType.Corner,   PipeType.Sink
            },

            // Вариант 4: Центральный коридор (Препятствие [X] сверху-справа)
            // Проходимый путь: (0,0)->(1,0)->(1,1)->(1,2)->(2,2)
            new PipeType[]
            {
                PipeType.Source,   PipeType.Corner,     PipeType.BrokenJunk,
                PipeType.Corner,   PipeType.Straight,   PipeType.Straight,
                PipeType.Straight, PipeType.Corner,     PipeType.Sink
            },

            // Вариант 5: Двойной зигзаг (Препятствие [X] снизу-по-центру)
            // Проходимый путь: (0,0)->(1,0)->(1,1)->(2,1)->(2,2)
            new PipeType[]
            {
                PipeType.Source,   PipeType.Corner,     PipeType.Corner,
                PipeType.Straight, PipeType.Corner,     PipeType.Corner,
                PipeType.Corner,   PipeType.BrokenJunk, PipeType.Sink
            }
        };

        // Базовый поворот источника для каждого варианта: 0 = вправо, 1 = вниз
        private static readonly int[] SourceRotations = new int[] { 0, 1, 0, 0, 0 };

        protected override void Awake()
        {
            CacheSpritesFromChildren();

            if (_gridTiles != null)
            {
                for (int i = 0; i < _gridTiles.Length; i++)
                {
                    if (_gridTiles[i] != null)
                    {
                        _gridTiles[i].GridX = i % _gridWidth;
                        _gridTiles[i].GridY = i / _gridWidth;
                        _gridTiles[i].OnTileRotated += HandleTileRotated;

                        if (_gridTiles[i].Type == PipeType.Source) _sourceTile = _gridTiles[i];
                        if (_gridTiles[i].Type == PipeType.Sink) _sinkTile = _gridTiles[i];
                    }
                }
            }
            base.Awake();
        }

        private void OnDestroy()
        {
            if (_gridTiles != null)
            {
                foreach (var tile in _gridTiles)
                {
                    if (tile != null) tile.OnTileRotated -= HandleTileRotated;
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            CacheSpritesFromChildren();

            // Выбираем один из 5 случайных вариантов
            int selectedLayout = Random.Range(0, Layouts.Length);
            ApplyLayout(selectedLayout);

            EvaluateFlow();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void CacheSpritesFromChildren()
        {
            if (_gridTiles == null) return;
            foreach (var tile in _gridTiles)
            {
                if (tile == null) continue;
                Sprite spr = tile.PipeImage != null ? tile.PipeImage.sprite : null;
                if (spr == null) continue;

                switch (tile.Type)
                {
                    case PipeType.Straight: if (_straightSprite == null) _straightSprite = spr; break;
                    case PipeType.Corner: if (_cornerSprite == null) _cornerSprite = spr; break;
                    case PipeType.Source: if (_sourceSprite == null) _sourceSprite = spr; break;
                    case PipeType.Sink: if (_sinkSprite == null) _sinkSprite = spr; break;
                    case PipeType.BrokenJunk: if (_junkSprite == null) _junkSprite = spr; break;
                }
            }
        }

        private Sprite GetSpriteForType(PipeType type)
        {
            switch (type)
            {
                case PipeType.Source: return _sourceSprite;
                case PipeType.Sink: return _sinkSprite;
                case PipeType.Corner: return _cornerSprite;
                case PipeType.BrokenJunk: return _junkSprite;
                default: return _straightSprite;
            }
        }

        /// <summary>
        /// Применяет конфигурацию труб для указанного варианта и случайно вращает интерактивные трубы.
        /// </summary>
        private void ApplyLayout(int layoutIndex)
        {
            if (_gridTiles == null || _gridTiles.Length != 9) return;

            _currentLayoutIndex = layoutIndex % Layouts.Length;
            PipeType[] types = Layouts[_currentLayoutIndex];
            int sourceRot = SourceRotations[_currentLayoutIndex];

            for (int i = 0; i < 9; i++)
            {
                PipeType type = types[i];
                Sprite spr = GetSpriteForType(type);
                int rot = (type == PipeType.Source) ? sourceRot : 0;

                _gridTiles[i].Setup(type, rot, spr);

                if (type == PipeType.Source) _sourceTile = _gridTiles[i];
                if (type == PipeType.Sink) _sinkTile = _gridTiles[i];
            }

            // Рандомизируем вращение всех подвижных труб (Straight, Corner)
            // И следим, чтобы головоломка не оказалась решённой изначально
            int attempts = 0;
            do
            {
                for (int i = 0; i < 9; i++)
                {
                    PipeTile tile = _gridTiles[i];
                    if (tile.Type == PipeType.Straight || tile.Type == PipeType.Corner)
                    {
                        int randomRot = Random.Range(0, 4);
                        tile.Setup(tile.Type, randomRot, GetSpriteForType(tile.Type));
                    }
                }
                attempts++;
            } while (attempts < 50 && CheckIsSolvedSimulated());
        }

        private void HandleTileRotated(PipeTile tile)
        {
            if (_isCompleted) return;
            EvaluateFlow();
        }

        /// <summary>
        /// Симуляция без изменения UI, проверяющая, замкнута ли цепь.
        /// </summary>
        private bool CheckIsSolvedSimulated()
        {
            if (_sourceTile == null || _sinkTile == null) return false;

            Queue<PipeTile> queue = new Queue<PipeTile>();
            HashSet<PipeTile> visited = new HashSet<PipeTile>();

            queue.Enqueue(_sourceTile);
            visited.Add(_sourceTile);

            // Направления в экранных координатах сетки (GridY=0 сверху, GridY=2 снизу):
            // 0: Вверх (dy=-1, mask=1, oppMask=4)
            // 1: Вправо (dx=1,  mask=2, oppMask=8)
            // 2: Вниз  (dy=1,  mask=4, oppMask=1)
            // 3: Влево (dx=-1, mask=8, oppMask=2)
            int[] dx = { 0, 1, 0, -1 };
            int[] dy = { -1, 0, 1, 0 };
            int[] currentMasks = { 1, 2, 4, 8 };
            int[] neighborMasks = { 4, 8, 1, 2 };

            while (queue.Count > 0)
            {
                PipeTile curr = queue.Dequeue();
                if (curr == _sinkTile) return true;

                int currOpen = curr.GetOpenConnections();
                for (int dir = 0; dir < 4; dir++)
                {
                    if ((currOpen & currentMasks[dir]) == 0) continue;

                    int nx = curr.GridX + dx[dir];
                    int ny = curr.GridY + dy[dir];

                    if (nx >= 0 && nx < _gridWidth && ny >= 0 && ny < _gridHeight)
                    {
                        PipeTile neighbor = GetTile(nx, ny);
                        if (neighbor != null && !visited.Contains(neighbor))
                        {
                            int neighborOpen = neighbor.GetOpenConnections();
                            if ((neighborOpen & neighborMasks[dir]) != 0)
                            {
                                visited.Add(neighbor);
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// BFS поиск непрерывного пути потока от источника к приемнику.
        /// </summary>
        private void EvaluateFlow()
        {
            if (_gridTiles == null || _gridTiles.Length == 0) return;

            // Сброс визуализации воды
            foreach (var tile in _gridTiles)
            {
                if (tile != null) tile.UpdateWaterVisual(false);
            }

            if (_sourceTile == null) return;

            Queue<PipeTile> queue = new Queue<PipeTile>();
            HashSet<PipeTile> visited = new HashSet<PipeTile>();

            queue.Enqueue(_sourceTile);
            visited.Add(_sourceTile);
            _sourceTile.UpdateWaterVisual(true);

            bool sinkReached = false;

            // Направления в координатах сетки (GridY=0 сверху, GridY=2 снизу):
            // 0: Вверх (dy=-1, mask=1, oppMask=4)
            // 1: Вправо (dx=1,  mask=2, oppMask=8)
            // 2: Вниз  (dy=1,  mask=4, oppMask=1)
            // 3: Влево (dx=-1, mask=8, oppMask=2)
            int[] dx = { 0, 1, 0, -1 };
            int[] dy = { -1, 0, 1, 0 };
            int[] currentMasks = { 1, 2, 4, 8 };
            int[] neighborMasks = { 4, 8, 1, 2 };

            while (queue.Count > 0)
            {
                PipeTile curr = queue.Dequeue();
                if (curr == _sinkTile)
                {
                    sinkReached = true;
                }

                int currOpen = curr.GetOpenConnections();

                for (int dir = 0; dir < 4; dir++)
                {
                    if ((currOpen & currentMasks[dir]) == 0) continue;

                    int nx = curr.GridX + dx[dir];
                    int ny = curr.GridY + dy[dir];

                    if (nx >= 0 && nx < _gridWidth && ny >= 0 && ny < _gridHeight)
                    {
                        PipeTile neighbor = GetTile(nx, ny);
                        if (neighbor != null && !visited.Contains(neighbor))
                        {
                            int neighborOpen = neighbor.GetOpenConnections();
                            // Проверяем взаимную стыковку труб
                            if ((neighborOpen & neighborMasks[dir]) != 0)
                            {
                                visited.Add(neighbor);
                                neighbor.UpdateWaterVisual(true);
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                }
            }

            float progress = visited.Count / (float)_gridTiles.Length;
            SetProgress(progress);

            if (sinkReached)
            {
                CompleteMechanic();
                SetProgress(1f);
                if (_statusText != null) _statusText.text = "<color=#00FF99>Цепь замкнута! Водопровод функционирует!</color>";
                if (_instructionText != null) _instructionText.text = "<color=#00FF99>Успех! Непрерывный маршрут подачи воды построен.</color>";
            }
            else
            {
                string variantLabel = _currentLayoutIndex >= 0 ? $" (Вариант #{_currentLayoutIndex + 1})" : "";
                if (_statusText != null) _statusText.text = $"Заполнено узлов: {visited.Count} / {_gridTiles.Length}{variantLabel}";
            }
        }

        private PipeTile GetTile(int x, int y)
        {
            int index = y * _gridWidth + x;
            if (index >= 0 && index < _gridTiles.Length) return _gridTiles[index];
            return null;
        }
    }
}
