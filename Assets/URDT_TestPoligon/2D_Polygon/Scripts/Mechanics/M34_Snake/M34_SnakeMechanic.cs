using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M34_Snake
{
    /// <summary>
    /// Механика #34: Змейка на сетке 14×9. Змейка движется по тикам в текущем направлении;
    /// игрок задаёт направление кнопками (Вверх/Вниз/Влево/Вправо) или свайпом по полю.
    /// Съеденная еда увеличивает длину змейки на 1. Столкновение со стеной или собственным
    /// телом — провал этапа.
    /// </summary>
    public class M34_SnakeMechanic : BaseMechanic2DModule
    {
        // ---- Константы поля ----
        private const int GRID_COLS = 14;
        private const int GRID_ROWS = 9;
        private const float CELL = 44f;
        private const float FIELD_W = GRID_COLS * CELL; // 616
        private const float FIELD_H = GRID_ROWS * CELL; // 396

        // Параметры по этапам
        private static readonly float[] StageTick = new float[] { 0.30f, 0.22f };
        private static readonly int[] StageTarget = new int[] { 5, 7 };

        // ---- Ссылки на создаваемые UI объекты ----
        private RectTransform _fieldRt;
        private RectTransform _foodRt;
        private RectTransform _headRt;
        private readonly List<RectTransform> _segRts = new List<RectTransform>();
        private TMP_Text _instructionText;
        private TMP_Text _hudText;
        private Button _btnUp, _btnDown, _btnLeft, _btnRight;

        // ---- Состояние игры ----
        private readonly List<Vector2Int> _body = new List<Vector2Int>(); // [0] = голова, (col,row) row0=верх
        private Vector2Int _dir = new Vector2Int(1, 0);
        private Vector2Int _pendingDir = new Vector2Int(1, 0);
        private Vector2Int _food = new Vector2Int(-1, -1);
        private bool _started;
        private float _tickTimer;
        private float _tickSeconds = 0.30f;
        private int _targetFood = 5;
        private int _collected;

        // ---- Публичные свойства для AI ----
        public int HeadCol => _body.Count > 0 ? _body[0].x : 0;
        public int HeadRow => _body.Count > 0 ? _body[0].y : 0;
        public string Direction
        {
            get
            {
                if (_dir.x > 0) return "right";
                if (_dir.x < 0) return "left";
                if (_dir.y > 0) return "down";
                return "up";
            }
        }
        public int FoodCol => _food.x;
        public int FoodRow => _food.y;
        public int Length => _body.Count;
        public int Collected => _collected;
        public int TargetFood => _targetFood;
        public int GridCols => GRID_COLS;
        public int GridRows => GRID_ROWS;
        public float TickSeconds => _tickSeconds;
        public string BodyCells
        {
            get
            {
                var sb = new StringBuilder();
                for (int i = 0; i < _body.Count; i++)
                {
                    if (i > 0) sb.Append(';');
                    sb.Append(_body[i].x); sb.Append(','); sb.Append(_body[i].y);
                }
                return sb.ToString();
            }
        }

        public override int StageCount => 2;

        protected override string GetStageInstruction(int stage)
        {
            switch (stage)
            {
                case 1: return "Этап 1/2. Собери 5 плодов. Управляй кнопками или свайпом по полю. Не врежься в стену и в собственный хвост.";
                case 2: return "Этап 2/2. Змейка быстрее — собери 7 плодов, не столкнувшись со стеной или своим телом.";
                default: return _instruction;
            }
        }

        public override void Initialize()
        {
            // Полностью пересобираем поле, чтобы после сброса не оставалось «призраков» с тем же именем в этом же кадре
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            _segRts.Clear();
            _headRt = null;
            _foodRt = null;

            BuildUI();

            int idx = Mathf.Clamp(CurrentStage - 1, 0, StageTick.Length - 1);
            _tickSeconds = StageTick[idx];
            _targetFood = StageTarget[idx];

            _body.Clear();
            _body.Add(new Vector2Int(5, 4)); // голова
            _body.Add(new Vector2Int(4, 4));
            _body.Add(new Vector2Int(3, 4));
            _dir = new Vector2Int(1, 0);
            _pendingDir = _dir;
            _started = false;
            _tickTimer = 0f;
            _collected = 0;

            RebuildSnakeVisuals();
            SpawnFood();
            UpdateHud();

            base.Initialize();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        // ---------- Построение UI ----------

        private void BuildUI()
        {
            RectTransform root = (RectTransform)transform;

            // Инструкция сверху
            _instructionText = CreateText("InstructionText", root, new Vector2(0f, 258f), new Vector2(1000f, 44f),
                GetStageInstruction(CurrentStage), 22, TextAlignmentOptions.Center, new Color(0.92f, 0.92f, 0.92f));

            // HUD (счётчик)
            _hudText = CreateText("HudText", root, new Vector2(0f, 224f), new Vector2(600f, 32f),
                "", 22, TextAlignmentOptions.Center, new Color(1f, 0.95f, 0.6f));

            // Поле
            GameObject fieldGo = new GameObject("Field", typeof(RectTransform), typeof(Image));
            fieldGo.transform.SetParent(root, false);
            _fieldRt = (RectTransform)fieldGo.transform;
            _fieldRt.anchorMin = _fieldRt.anchorMax = new Vector2(0.5f, 0.5f);
            _fieldRt.pivot = new Vector2(0.5f, 0.5f);
            _fieldRt.sizeDelta = new Vector2(FIELD_W, FIELD_H);
            _fieldRt.anchoredPosition = new Vector2(0f, 60f);
            var fieldImg = fieldGo.GetComponent<Image>();
            fieldImg.color = new Color(0.10f, 0.14f, 0.10f);
            fieldImg.raycastTarget = true; // поле принимает свайпы
            var swipe = fieldGo.AddComponent<SnakeSwipeInput>();
            swipe.Owner = this;

            // Сетка (только визуально) — тонкими линиями
            DrawGrid();

            // Кнопки — «крестовина» под полем
            _btnUp = CreateButton("BtnUp", root, new Vector2(0f, -170f), new Vector2(84f, 60f), "▲");
            _btnDown = CreateButton("BtnDown", root, new Vector2(0f, -262f), new Vector2(84f, 60f), "▼");
            _btnLeft = CreateButton("BtnLeft", root, new Vector2(-100f, -216f), new Vector2(84f, 60f), "◀");
            _btnRight = CreateButton("BtnRight", root, new Vector2(100f, -216f), new Vector2(84f, 60f), "▶");
            _btnUp.onClick.AddListener(() => TrySetDirection(new Vector2Int(0, -1)));
            _btnDown.onClick.AddListener(() => TrySetDirection(new Vector2Int(0, 1)));
            _btnLeft.onClick.AddListener(() => TrySetDirection(new Vector2Int(-1, 0)));
            _btnRight.onClick.AddListener(() => TrySetDirection(new Vector2Int(1, 0)));
        }

        private void DrawGrid()
        {
            // Лёгкая рамка ячеек; сами клетки — просто фон, не интерактивные.
            GameObject holder = new GameObject("GridLines", typeof(RectTransform));
            var rt = (RectTransform)holder.transform;
            rt.SetParent(_fieldRt, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(FIELD_W, FIELD_H);
            rt.anchoredPosition = Vector2.zero;
            Color lineColor = new Color(1f, 1f, 1f, 0.06f);
            for (int c = 1; c < GRID_COLS; c++)
            {
                var line = new GameObject("VLine_" + c, typeof(RectTransform), typeof(Image));
                var lrt = (RectTransform)line.transform;
                lrt.SetParent(rt, false);
                lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(1f, FIELD_H);
                lrt.anchoredPosition = new Vector2(-FIELD_W * 0.5f + c * CELL, 0f);
                var im = line.GetComponent<Image>(); im.color = lineColor; im.raycastTarget = false;
            }
            for (int r = 1; r < GRID_ROWS; r++)
            {
                var line = new GameObject("HLine_" + r, typeof(RectTransform), typeof(Image));
                var lrt = (RectTransform)line.transform;
                lrt.SetParent(rt, false);
                lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(FIELD_W, 1f);
                lrt.anchoredPosition = new Vector2(0f, FIELD_H * 0.5f - r * CELL);
                var im = line.GetComponent<Image>(); im.color = lineColor; im.raycastTarget = false;
            }
        }

        private TMP_Text CreateText(string name, RectTransform parent, Vector2 pos, Vector2 size,
                                    string text, int fontSize, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        private Button CreateButton(string name, RectTransform parent, Vector2 pos, Vector2 size, string caption)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.22f, 0.35f, 0.55f);
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            var lblGo = new GameObject("Label", typeof(RectTransform));
            var lrt = (RectTransform)lblGo.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = caption;
            lbl.fontSize = 30;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.color = Color.white;
            lbl.raycastTarget = false;
            return btn;
        }

        // ---------- Отрисовка змейки ----------

        private void RebuildSnakeVisuals()
        {
            // Очистить прежние сегменты (голова + сегменты создаются заново)
            if (_headRt != null) { DestroyImmediate(_headRt.gameObject); _headRt = null; }
            for (int i = 0; i < _segRts.Count; i++)
            {
                if (_segRts[i] != null) DestroyImmediate(_segRts[i].gameObject);
            }
            _segRts.Clear();

            // Голова
            _headRt = CreateCellSprite("SnakeHead", _fieldRt, new Color(0.35f, 0.90f, 0.35f), 38f);
            // Сегменты
            for (int i = 1; i < _body.Count; i++)
            {
                var seg = CreateCellSprite("SnakeSeg_" + i, _fieldRt, new Color(0.20f, 0.65f, 0.25f), 34f);
                _segRts.Add(seg);
            }
            UpdateSnakePositions();
        }

        private RectTransform CreateCellSprite(string name, RectTransform parent, Color color, float sizePx)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(sizePx, sizePx);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private void UpdateSnakePositions()
        {
            if (_body.Count == 0) return;
            if (_headRt != null) _headRt.anchoredPosition = CellToLocal(_body[0]);
            for (int i = 1; i < _body.Count; i++)
            {
                int segIdx = i - 1;
                if (segIdx < _segRts.Count && _segRts[segIdx] != null)
                {
                    _segRts[segIdx].anchoredPosition = CellToLocal(_body[i]);
                }
            }
        }

        private Vector2 CellToLocal(Vector2Int cell)
        {
            float x = -FIELD_W * 0.5f + CELL * 0.5f + cell.x * CELL;
            float y = FIELD_H * 0.5f - CELL * 0.5f - cell.y * CELL;
            return new Vector2(x, y);
        }

        // ---------- Игровой цикл ----------

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;
            if (!_started) return;

            _tickTimer += Time.deltaTime;
            if (_tickTimer < _tickSeconds) return;
            _tickTimer -= _tickSeconds;
            StepTick();
        }

        private void StepTick()
        {
            // Применяем отложенное направление, если оно не противоположно текущему
            if (!IsOpposite(_pendingDir, _dir) || _body.Count <= 1)
            {
                _dir = _pendingDir;
            }

            Vector2Int head = _body[0];
            Vector2Int next = new Vector2Int(head.x + _dir.x, head.y + _dir.y);

            // Стена
            if (next.x < 0 || next.x >= GRID_COLS || next.y < 0 || next.y >= GRID_ROWS)
            {
                FailStage("Змейка врезалась");
                return;
            }

            bool eating = (next == _food);

            // Столкновение с телом: если не едим — хвост уйдёт, поэтому его можно занимать.
            int selfLimit = eating ? _body.Count : _body.Count - 1;
            for (int i = 0; i < selfLimit; i++)
            {
                if (_body[i] == next)
                {
                    FailStage("Змейка врезалась");
                    return;
                }
            }

            // Движение: сдвигаем тело
            _body.Insert(0, next);
            if (!eating)
            {
                _body.RemoveAt(_body.Count - 1);
            }
            else
            {
                _collected++;
                // Новый сегмент визуально
                var seg = CreateCellSprite("SnakeSeg_" + (_body.Count - 1), _fieldRt, new Color(0.20f, 0.65f, 0.25f), 34f);
                _segRts.Add(seg);
                SpawnFood();
            }

            UpdateSnakePositions();
            UpdateHud();
            SetProgress(_targetFood > 0 ? (float)_collected / _targetFood : 0f);

            if (_collected >= _targetFood)
            {
                CompleteMechanic();
            }
        }

        private static bool IsOpposite(Vector2Int a, Vector2Int b)
        {
            return a.x == -b.x && a.y == -b.y && (a.x != 0 || a.y != 0);
        }

        private void SpawnFood()
        {
            // Собираем список свободных ячеек
            var occupied = new HashSet<int>();
            for (int i = 0; i < _body.Count; i++) occupied.Add(_body[i].y * GRID_COLS + _body[i].x);

            int total = GRID_COLS * GRID_ROWS;
            int freeCount = total - occupied.Count;
            if (freeCount <= 0)
            {
                _food = new Vector2Int(-1, -1);
                if (_foodRt != null) { DestroyImmediate(_foodRt.gameObject); _foodRt = null; }
                return;
            }
            int pick = Random.Range(0, freeCount);
            int foundIdx = -1;
            for (int i = 0, seen = 0; i < total; i++)
            {
                if (occupied.Contains(i)) continue;
                if (seen == pick) { foundIdx = i; break; }
                seen++;
            }
            _food = new Vector2Int(foundIdx % GRID_COLS, foundIdx / GRID_COLS);

            if (_foodRt == null)
            {
                _foodRt = CreateCellSprite("Food", _fieldRt, new Color(0.95f, 0.30f, 0.30f), 30f);
            }
            _foodRt.anchoredPosition = CellToLocal(_food);
        }

        private void UpdateHud()
        {
            if (_hudText != null)
            {
                _hudText.text = $"Плоды: {_collected} / {_targetFood}   Длина: {_body.Count}";
            }
            if (_instructionText != null)
            {
                _instructionText.text = GetStageInstruction(CurrentStage);
            }
        }

        // ---------- Управление ----------

        public void TrySetDirection(Vector2Int d)
        {
            if (_isCompleted || IsInTransition) return;
            if (d.x == 0 && d.y == 0) return;
            // Разворот в себя игнорируется
            if (_body.Count > 1 && IsOpposite(d, _dir)) return;
            _pendingDir = d;
            if (!_started)
            {
                _started = true;
                _tickTimer = 0f;
            }
        }

        internal void HandleSwipe(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) < 8f && Mathf.Abs(delta.y) < 8f) return;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                TrySetDirection(new Vector2Int(delta.x > 0 ? 1 : -1, 0));
            }
            else
            {
                // экранное +y = вверх, но в сетке row0 сверху => "up" = (0,-1)
                TrySetDirection(new Vector2Int(0, delta.y > 0 ? -1 : 1));
            }
        }
    }

    /// <summary>Приёмник свайпов по полю. Отдаёт вектор смещения от начала к концу драга владельцу.</summary>
    public class SnakeSwipeInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public M34_SnakeMechanic Owner;
        private Vector2 _startLocal;
        private Vector2 _lastLocal;
        private bool _dragging;

        public void OnBeginDrag(PointerEventData e)
        {
            if (Owner == null || Owner.IsCompleted || Owner.IsInTransition) return;
            var rt = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, cam, out _startLocal);
            _lastLocal = _startLocal;
            _dragging = true;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging || Owner == null) return;
            var rt = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, cam, out _lastLocal);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging || Owner == null) { _dragging = false; return; }
            _dragging = false;
            Owner.HandleSwipe(_lastLocal - _startLocal);
        }
    }
}
