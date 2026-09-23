using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M35_Merge2
{
    /// <summary>
    /// Механика #35: аркадный «Мердж-2» на поле 4×4.
    /// Игрок кнопкой «Создать» размещает предметы уровня 1 в свободных клетках; перетаскиванием
    /// объединяет два одинаковых предмета в один следующего уровня. Задача этапа — получить
    /// предмет заданного уровня, не исчерпав запас созданий и не заблокировав поле.
    /// UI полностью строится в коде в Initialize() (перевызывается на старт, смену этапа, провал).
    /// </summary>
    public class M35_Merge2Mechanic : BaseMechanic2DModule
    {
        private const int GRID_COLS = 4;
        private const int GRID_ROWS = 4;
        private const float CELL_SIZE = 130f;
        private const float CELL_GAP = 10f;
        private const float BOARD_HALF = (GRID_COLS * CELL_SIZE + (GRID_COLS - 1) * CELL_GAP) * 0.5f; // 275

        // Параметры по этапам: целевой уровень и запас созданий.
        private static readonly int[] STAGE_TARGET_LEVEL = { 4, 5 };
        private static readonly int[] STAGE_SPAWNS      = { 20, 34 };

        // Цвета по уровню (индекс = level-1). Отчётливо разные тона.
        private static readonly Color[] LEVEL_COLORS = new Color[]
        {
            new Color(0.85f, 0.85f, 0.90f), // 1 — светло-серый
            new Color(0.55f, 0.85f, 0.55f), // 2 — зелёный
            new Color(0.35f, 0.70f, 1.00f), // 3 — голубой
            new Color(0.95f, 0.85f, 0.30f), // 4 — жёлтый
            new Color(1.00f, 0.55f, 0.20f), // 5 — оранжевый
            new Color(0.95f, 0.35f, 0.35f), // 6 — красный
            new Color(0.75f, 0.35f, 0.90f), // 7 — фиолетовый
            new Color(0.35f, 0.30f, 0.85f), // 8 — синий
            new Color(0.20f, 0.75f, 0.75f), // 9 — бирюзовый
            new Color(1.00f, 0.85f, 0.65f), // 10 — телесный
        };

        public override int StageCount => 2;

        // ---------------- Публичное состояние (видимое ИИ через beacons) ----------------
        public int MaxLevel => _maxLevel;
        public int TargetLevel => _targetLevel;
        public int SpawnsLeft => _spawnsLeft;
        public int ItemCount => _items.Count;
        public int EmptyCells => GRID_COLS * GRID_ROWS - _items.Count;

        // ---------------- Внутреннее состояние ----------------
        private RectTransform _root;
        private RectTransform _boardContainer;
        private RectTransform _rightPanel;
        private TextMeshProUGUI _instructionText;
        private TextMeshProUGUI _statusText;
        private Button _btnSpawn;
        private Canvas _canvas;

        private readonly MergeItem[,] _grid = new MergeItem[GRID_ROWS, GRID_COLS];
        private readonly List<MergeItem> _items = new List<MergeItem>();
        private int _targetLevel = 4;
        private int _spawnsLeft = 20;
        private int _maxLevel = 0;
        private int _nextItemId = 1;

        public Canvas Canvas
        {
            get
            {
                if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
                return _canvas;
            }
        }

        public override void Initialize()
        {
            BuildBoard();

            // База создаёт beacons ПОСЛЕ построения поля.
            base.Initialize();

            _maxLevel = 0;
            _nextItemId = 1;
            for (int r = 0; r < GRID_ROWS; r++)
                for (int c = 0; c < GRID_COLS; c++)
                    _grid[r, c] = null;
            _items.Clear();

            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_TARGET_LEVEL.Length - 1);
            _targetLevel = STAGE_TARGET_LEVEL[idx];
            _spawnsLeft = STAGE_SPAWNS[idx];

            UpdateStatusText();
            SetProgress(0f);
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, STAGE_TARGET_LEVEL.Length - 1);
            int tgt = STAGE_TARGET_LEVEL[idx];
            int spw = STAGE_SPAWNS[idx];
            return $"Этап {stage}/2. Кнопкой «Создать» ставьте предметы 1-го уровня и объединяйте одинаковые перетаскиванием. Соберите уровень {tgt}. Запас созданий: {spw}.";
        }

        // -----------------------------------------------------------------------------
        // Построение UI
        // -----------------------------------------------------------------------------
        private void BuildBoard()
        {
            _root = transform as RectTransform;
            if (_root == null)
            {
                Debug.LogError("[M35_Merge2] Root must be a RectTransform.");
                return;
            }

            // Полный сброс детей корня (гарантированно без «призраков» между этапами).
            for (int i = _root.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_root.GetChild(i).gameObject);
            }

            // Фон поля (визуальный, кликов не ловит).
            var fieldBg = CreateRect("Field", _root);
            SetAnchorsCenter(fieldBg, 1100f, 600f, Vector2.zero);
            var fieldImg = fieldBg.gameObject.AddComponent<Image>();
            fieldImg.color = new Color(0.10f, 0.11f, 0.14f);
            fieldImg.raycastTarget = false;

            // Верхняя инструкция.
            _instructionText = CreateText("InstructionText", _root,
                GetStageInstruction(CurrentStage), 22, TextAlignmentOptions.Center);
            SetAnchorsCenter((RectTransform)_instructionText.transform, 1050f, 60f, new Vector2(0f, 250f));
            _instructionText.raycastTarget = false;

            // Контейнер поля 4x4 (слева).
            _boardContainer = CreateRect("BoardBg", _root);
            SetAnchorsCenter(_boardContainer, GRID_COLS * CELL_SIZE + (GRID_COLS - 1) * CELL_GAP,
                GRID_ROWS * CELL_SIZE + (GRID_ROWS - 1) * CELL_GAP, new Vector2(-220f, -20f));
            var boardImg = _boardContainer.gameObject.AddComponent<Image>();
            boardImg.color = new Color(0.16f, 0.17f, 0.22f);
            boardImg.raycastTarget = false;

            // Клетки Cell_<row>_<col>.
            for (int r = 0; r < GRID_ROWS; r++)
            {
                for (int c = 0; c < GRID_COLS; c++)
                {
                    var cell = CreateRect($"Cell_{r}_{c}", _boardContainer);
                    Vector2 pos = CellAnchoredPos(r, c);
                    SetAnchorsCenter(cell, CELL_SIZE, CELL_SIZE, pos);
                    var img = cell.gameObject.AddComponent<Image>();
                    img.color = new Color(0.22f, 0.24f, 0.30f);
                    img.raycastTarget = false;
                }
            }

            // Правая панель со счётчиками и кнопкой.
            _rightPanel = CreateRect("RightPanel", _root);
            SetAnchorsCenter(_rightPanel, 380f, 560f, new Vector2(360f, -20f));
            var rpImg = _rightPanel.gameObject.AddComponent<Image>();
            rpImg.color = new Color(0.14f, 0.15f, 0.20f);
            rpImg.raycastTarget = false;

            _statusText = CreateText("StatusText", _rightPanel,
                "", 22, TextAlignmentOptions.Center);
            SetAnchorsCenter((RectTransform)_statusText.transform, 340f, 220f, new Vector2(0f, 160f));
            _statusText.raycastTarget = false;

            // Кнопка «Создать».
            var btnRect = CreateRect("BtnSpawn", _rightPanel);
            SetAnchorsCenter(btnRect, 260f, 120f, new Vector2(0f, -140f));
            var btnImg = btnRect.gameObject.AddComponent<Image>();
            btnImg.color = new Color(0.35f, 0.65f, 0.35f);
            btnImg.raycastTarget = true;
            _btnSpawn = btnRect.gameObject.AddComponent<Button>();
            _btnSpawn.targetGraphic = btnImg;
            _btnSpawn.onClick.AddListener(OnSpawnClicked);

            var btnLabel = CreateText("Label", btnRect, "Создать", 28, TextAlignmentOptions.Center);
            SetAnchorsStretch((RectTransform)btnLabel.transform, Vector2.zero, Vector2.zero);
            btnLabel.raycastTarget = false;
        }

        // -----------------------------------------------------------------------------
        // Спавн, слияние, проверка блокировки
        // -----------------------------------------------------------------------------
        private void OnSpawnClicked()
        {
            if (IsCompleted || IsInTransition) return;
            if (_spawnsLeft <= 0) { CheckDeadlock(); return; }

            // Собираем список свободных клеток.
            List<Vector2Int> free = new List<Vector2Int>();
            for (int r = 0; r < GRID_ROWS; r++)
                for (int c = 0; c < GRID_COLS; c++)
                    if (_grid[r, c] == null) free.Add(new Vector2Int(c, r));
            if (free.Count == 0) { CheckDeadlock(); return; }

            var pick = free[Random.Range(0, free.Count)];
            SpawnItemAt(1, pick.y, pick.x);
            _spawnsLeft--;
            UpdateStatusText();
            CheckDeadlock();
        }

        private MergeItem SpawnItemAt(int level, int row, int col)
        {
            var itemRect = CreateRect($"MergeItem_{_nextItemId}", _boardContainer);
            _nextItemId++;
            SetAnchorsCenter(itemRect, CELL_SIZE - 12f, CELL_SIZE - 12f, CellAnchoredPos(row, col));
            var img = itemRect.gameObject.AddComponent<Image>();
            img.raycastTarget = true;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(itemRect, false);
            var labelRect = (RectTransform)labelGo.transform;
            SetAnchorsStretch(labelRect, Vector2.zero, Vector2.zero);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 42f;
            label.color = Color.black;
            label.raycastTarget = false;

            var item = itemRect.gameObject.AddComponent<MergeItem>();
            item.Setup(this, img, label, level, row, col);
            _grid[row, col] = item;
            _items.Add(item);
            if (level > _maxLevel) _maxLevel = level;
            return item;
        }

        /// <summary>Вызывается предметом при завершении перетаскивания.</summary>
        public void HandleItemDropped(MergeItem dragged, Vector2 localDropPos)
        {
            if (IsCompleted || IsInTransition) { dragged.ReturnToOrigin(); return; }

            // Определяем клетку под точкой сброса.
            int col = Mathf.FloorToInt((localDropPos.x + BOARD_HALF) / (CELL_SIZE + CELL_GAP));
            int row = Mathf.FloorToInt((BOARD_HALF - localDropPos.y) / (CELL_SIZE + CELL_GAP));

            if (row < 0 || row >= GRID_ROWS || col < 0 || col >= GRID_COLS)
            {
                dragged.ReturnToOrigin();
                return;
            }

            // Сброс в исходную клетку — возврат.
            if (row == dragged.Row && col == dragged.Col)
            {
                dragged.ReturnToOrigin();
                return;
            }

            var target = _grid[row, col];
            if (target == null)
            {
                // Перемещение в пустую клетку.
                _grid[dragged.Row, dragged.Col] = null;
                dragged.PlaceAt(row, col, CellAnchoredPos(row, col));
                _grid[row, col] = dragged;
            }
            else if (target.Level == dragged.Level)
            {
                // Слияние.
                int newLevel = dragged.Level + 1;
                _grid[dragged.Row, dragged.Col] = null;
                _grid[row, col] = null;
                _items.Remove(dragged);
                _items.Remove(target);
                DestroyImmediate(dragged.gameObject);
                DestroyImmediate(target.gameObject);
                var merged = SpawnItemAt(newLevel, row, col);

                if (merged.Level >= _targetLevel)
                {
                    UpdateStatusText();
                    SetProgress(1f);
                    CompleteMechanic();
                    return;
                }
            }
            else
            {
                // Другой уровень — возврат.
                dragged.ReturnToOrigin();
                return;
            }

            UpdateStatusText();
            UpdateProgress();
            CheckDeadlock();
        }

        private void UpdateProgress()
        {
            if (_targetLevel <= 1) { SetProgress(1f); return; }
            float local = Mathf.Clamp01((float)(_maxLevel - 1) / (_targetLevel - 1));
            SetProgress(local);
        }

        private void CheckDeadlock()
        {
            if (IsCompleted || IsInTransition) return;

            bool boardFull = _items.Count >= GRID_COLS * GRID_ROWS;
            bool hasPair = HasEqualPair();

            // Провал: запас исчерпан и никакого слияния сделать нельзя (нет одинаковых пар).
            if (_spawnsLeft <= 0 && !hasPair)
            {
                FailStage("Нет ходов");
                return;
            }
            // Провал: поле забито и одинаковых пар нет.
            if (boardFull && !hasPair)
            {
                FailStage("Нет ходов");
                return;
            }
        }

        private bool HasEqualPair()
        {
            // Достаточно двух одинаковых уровней в любом месте поля, чтобы теоретически
            // сдвинуть один к другому и объединить (все клетки достижимы перемещениями).
            var seen = new HashSet<int>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null) continue;
                if (!seen.Add(_items[i].Level)) return true;
            }
            return false;
        }

        private void UpdateStatusText()
        {
            if (_statusText == null) return;
            _statusText.text =
                $"Цель: уровень {_targetLevel}\n" +
                $"Максимум: {_maxLevel}\n" +
                $"Осталось создать: {_spawnsLeft}\n" +
                $"Предметов на поле: {_items.Count}/{GRID_COLS * GRID_ROWS}";
        }

        // -----------------------------------------------------------------------------
        // Утилиты
        // -----------------------------------------------------------------------------
        public static Color ColorForLevel(int level)
        {
            int idx = Mathf.Clamp(level - 1, 0, LEVEL_COLORS.Length - 1);
            return LEVEL_COLORS[idx];
        }

        public static Vector2 CellAnchoredPos(int row, int col)
        {
            float x = -BOARD_HALF + CELL_SIZE * 0.5f + col * (CELL_SIZE + CELL_GAP);
            float y =  BOARD_HALF - CELL_SIZE * 0.5f - row * (CELL_SIZE + CELL_GAP);
            return new Vector2(x, y);
        }

        public RectTransform BoardContainer => _boardContainer;

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = new Color(0.95f, 0.95f, 0.95f);
            return t;
        }

        private static void SetAnchorsCenter(RectTransform rt, float w, float h, Vector2 anchoredPos)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = anchoredPos;
        }

        private static void SetAnchorsStretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }

    /// <summary>Предмет мержа: перетаскивается, объединяется с равным по уровню.</summary>
    public class MergeItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int Level { get; private set; }
        public int Row { get; private set; }
        public int Col { get; private set; }

        private M35_Merge2Mechanic _mechanic;
        private Image _image;
        private TextMeshProUGUI _label;
        private RectTransform _rt;
        private Vector2 _originAnchored;
        private bool _dragging;

        public void Setup(M35_Merge2Mechanic mechanic, Image image, TextMeshProUGUI label, int level, int row, int col)
        {
            _mechanic = mechanic;
            _image = image;
            _label = label;
            _rt = (RectTransform)transform;
            SetLevel(level);
            PlaceAt(row, col, M35_Merge2Mechanic.CellAnchoredPos(row, col));
        }

        public void SetLevel(int level)
        {
            Level = level;
            if (_image != null) _image.color = M35_Merge2Mechanic.ColorForLevel(level);
            if (_label != null) _label.text = level.ToString();
        }

        public void PlaceAt(int row, int col, Vector2 anchoredPos)
        {
            Row = row; Col = col;
            _originAnchored = anchoredPos;
            if (_rt != null) _rt.anchoredPosition = anchoredPos;
        }

        public void ReturnToOrigin()
        {
            if (_rt != null) _rt.anchoredPosition = _originAnchored;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_mechanic == null || _mechanic.IsCompleted || _mechanic.IsInTransition) return;
            _dragging = true;
            _originAnchored = _rt.anchoredPosition;
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _mechanic == null) return;
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;
            var canvas = _mechanic.Canvas;
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, cam, out Vector2 local))
            {
                _rt.anchoredPosition = local;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging || _mechanic == null) return;
            _dragging = false;
            var parent = _rt.parent as RectTransform;
            var canvas = _mechanic.Canvas;
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, cam, out Vector2 local))
            {
                _mechanic.HandleItemDropped(this, local);
            }
            else
            {
                ReturnToOrigin();
            }
        }
    }
}
