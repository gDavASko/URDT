using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M36_Match3
{
    /// <summary>
    /// Механика #36: Три-в-ряд (Match-3). Поле 6x6 самоцветов пяти цветов; игрок меняет местами
    /// соседние камни, чтобы собрать линию из 3+ одинаковых. Комбинация засчитывается один раз за ход
    /// (каскадные срабатывания того же хода в счёт не идут). Лимит ходов; провал по исчерпанию ходов.
    /// UI собирается кодом в <see cref="Initialize"/>: сам камень существует всегда (Gem_row_col),
    /// меняются только его цвет и буква — стабильные имена важны для AI-игрока.
    /// </summary>
    public class M36_Match3Mechanic : BaseMechanic2DModule
    {
        private const int Cols = 6;
        private const int Rows = 6;
        private const float CellSize = 72f;
        private const float DragThreshold = 30f;

        private static readonly int[] STAGE_TARGET = { 4, 6 };
        private static readonly int[] STAGE_MOVES  = { 15, 14 };

        // Буквы (для дальтоников) и цвета для пяти видов камней.
        private static readonly char[] COLOR_CHARS = { 'A', 'B', 'C', 'D', 'E' };
        private static readonly Color[] COLOR_HUES =
        {
            new Color(0.92f, 0.25f, 0.28f, 1f), // A - красный
            new Color(0.30f, 0.78f, 0.36f, 1f), // B - зелёный
            new Color(0.28f, 0.55f, 0.95f, 1f), // C - синий
            new Color(0.98f, 0.82f, 0.22f, 1f), // D - жёлтый
            new Color(0.72f, 0.38f, 0.88f, 1f), // E - фиолетовый
        };

        public override int StageCount => 2;

        // Публичное только-чтение состояние (виден AI через beacon как game.*).
        public int Combos => _combos;
        public int TargetCombos => _targetCombos;
        public int MovesLeft => _movesLeft;
        public string BoardColors => BuildBoardColorsString();
        public int BoardRows => Rows;
        public int BoardCols => Cols;

        private readonly int[,] _grid = new int[Rows, Cols];
        private readonly Match3Gem[,] _gems = new Match3Gem[Rows, Cols];
        private int _combos;
        private int _targetCombos;
        private int _movesLeft;
        private Canvas _canvas;
        private bool _resolving;

        private RectTransform _boardRect;
        private TMP_Text _instructionText;
        private TMP_Text _statusText;
        private readonly System.Random _rng = new System.Random();

        public override void Initialize()
        {
            // Полная перестройка UI: убираем всё, что построили в прошлый раз (в т.ч. кэшированные компоненты).
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++) _gems[r, c] = null;

            _combos = 0;
            _resolving = false;
            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_TARGET.Length - 1);
            _targetCombos = STAGE_TARGET[idx];
            _movesLeft = STAGE_MOVES[idx];

            BuildUI();
            GenerateBoard();
            BuildGemObjects();

            _canvas = GetComponentInParent<Canvas>();
            UpdateGemVisuals();
            UpdateStatusText();
            if (_instructionText != null) _instructionText.text = GetStageInstruction(CurrentStage);

            base.Initialize();
            SetProgress(0f);
        }

        public override void ResetMechanic() { Initialize(); }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, STAGE_TARGET.Length - 1);
            return $"Этап {stage}/2. Меняйте местами соседние камни, чтобы собрать линию из 3+ одного цвета. Цель: {STAGE_TARGET[idx]} комбинаций за {STAGE_MOVES[idx]} ходов.";
        }

        // ---------- Построение UI ----------

        private void BuildUI()
        {
            var rt = transform as RectTransform;

            var bg = CreateChild("Field", rt);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            bgImg.raycastTarget = false;
            SetSize(bg, 1100f, 600f);
            bg.anchoredPosition = Vector2.zero;

            var titleRt = CreateChild("InstructionLabel", rt);
            SetSize(titleRt, 900f, 60f);
            titleRt.anchoredPosition = new Vector2(0f, 260f);
            _instructionText = titleRt.gameObject.AddComponent<TextMeshProUGUI>();
            _instructionText.text = GetStageInstruction(CurrentStage);
            _instructionText.fontSize = 22f;
            _instructionText.alignment = TextAlignmentOptions.Center;
            _instructionText.color = Color.white;
            _instructionText.raycastTarget = false;

            var stRt = CreateChild("StatusLabel", rt);
            SetSize(stRt, 900f, 40f);
            stRt.anchoredPosition = new Vector2(0f, 210f);
            _statusText = stRt.gameObject.AddComponent<TextMeshProUGUI>();
            _statusText.fontSize = 20f;
            _statusText.alignment = TextAlignmentOptions.Center;
            _statusText.color = new Color(0.85f, 0.9f, 1f);
            _statusText.raycastTarget = false;

            _boardRect = CreateChild("Board", rt);
            SetSize(_boardRect, Cols * CellSize + 8f, Rows * CellSize + 8f);
            _boardRect.anchoredPosition = new Vector2(0f, -40f);
            var boardImg = _boardRect.gameObject.AddComponent<Image>();
            boardImg.color = new Color(0.06f, 0.08f, 0.12f, 1f);
            boardImg.raycastTarget = false;
        }

        private static RectTransform CreateChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            return r;
        }

        private static void SetSize(RectTransform r, float w, float h) { r.sizeDelta = new Vector2(w, h); }

        private void BuildGemObjects()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    var rt = CreateChild($"Gem_{r}_{c}", _boardRect);
                    SetSize(rt, CellSize - 8f, CellSize - 8f);
                    rt.anchoredPosition = CellToLocal(r, c);
                    var img = rt.gameObject.AddComponent<Image>();
                    img.raycastTarget = true;
                    var gem = rt.gameObject.AddComponent<Match3Gem>();
                    gem.Bind(this, r, c);
                    _gems[r, c] = gem;

                    var lblRt = CreateChild("Label", rt);
                    SetSize(lblRt, CellSize - 8f, CellSize - 8f);
                    lblRt.anchoredPosition = Vector2.zero;
                    var lbl = lblRt.gameObject.AddComponent<TextMeshProUGUI>();
                    lbl.fontSize = 30f;
                    lbl.alignment = TextAlignmentOptions.Center;
                    lbl.color = new Color(0f, 0f, 0f, 0.75f);
                    lbl.raycastTarget = false;
                    gem.SetVisuals(lbl, img);
                }
            }
        }

        private static Vector2 CellToLocal(int r, int c)
        {
            float x = (c - (Cols - 1) * 0.5f) * CellSize;
            // строка 0 — сверху: чем больше r, тем ниже
            float y = ((Rows - 1) * 0.5f - r) * CellSize;
            return new Vector2(x, y);
        }

        private void UpdateGemVisuals()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    var g = _gems[r, c];
                    if (g == null) continue;
                    int col = _grid[r, c];
                    if (col < 0) col = 0;
                    g.ApplyColor(col, COLOR_HUES[col], COLOR_CHARS[col]);
                    g.SetCell(r, c);
                    var rt = (RectTransform)g.transform;
                    rt.anchoredPosition = CellToLocal(r, c);
                    if (rt.name != $"Gem_{r}_{c}") rt.name = $"Gem_{r}_{c}";
                }
            }
        }

        // ---------- Генерация поля ----------

        private void GenerateBoard()
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++) _grid[r, c] = -1;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int color;
                    int guard = 0;
                    do
                    {
                        color = _rng.Next(0, COLOR_CHARS.Length);
                        guard++;
                    } while (guard < 60 && MakesImmediateMatch(r, c, color));
                    _grid[r, c] = color;
                }
            }

            // На всякий случай — гасим возможные оставшиеся совпадения без начисления комбинаций.
            CleanupInitialMatches();

            int reshuffleGuard = 0;
            while (!HasAnyPossibleMove() && reshuffleGuard < 30)
            {
                ShuffleGridColors();
                CleanupInitialMatches();
                reshuffleGuard++;
            }
        }

        private bool MakesImmediateMatch(int r, int c, int color)
        {
            int cnt = 1;
            for (int cc = c - 1; cc >= 0 && _grid[r, cc] == color; cc--) cnt++;
            for (int cc = c + 1; cc < Cols && _grid[r, cc] == color; cc++) cnt++;
            if (cnt >= 3) return true;
            cnt = 1;
            for (int rr = r - 1; rr >= 0 && _grid[rr, c] == color; rr--) cnt++;
            for (int rr = r + 1; rr < Rows && _grid[rr, c] == color; rr++) cnt++;
            return cnt >= 3;
        }

        private void ShuffleGridColors()
        {
            int total = Rows * Cols;
            int[] flat = new int[total];
            for (int i = 0; i < total; i++) flat[i] = _grid[i / Cols, i % Cols];
            for (int i = total - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (flat[i], flat[j]) = (flat[j], flat[i]);
            }
            for (int i = 0; i < total; i++) _grid[i / Cols, i % Cols] = flat[i];

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int guard = 0;
                    while (MakesImmediateMatch(r, c, _grid[r, c]) && guard < 10)
                    {
                        _grid[r, c] = (_grid[r, c] + 1) % COLOR_CHARS.Length;
                        guard++;
                    }
                }
            }
        }

        private bool HasAnyPossibleMove()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    if (c + 1 < Cols && SwapCreatesMatch(r, c, r, c + 1)) return true;
                    if (r + 1 < Rows && SwapCreatesMatch(r, c, r + 1, c)) return true;
                }
            }
            return false;
        }

        private bool SwapCreatesMatch(int r1, int c1, int r2, int c2)
        {
            (_grid[r1, c1], _grid[r2, c2]) = (_grid[r2, c2], _grid[r1, c1]);
            bool m = HasMatchAt(r1, c1) || HasMatchAt(r2, c2);
            (_grid[r1, c1], _grid[r2, c2]) = (_grid[r2, c2], _grid[r1, c1]);
            return m;
        }

        private bool HasMatchAt(int r, int c)
        {
            int color = _grid[r, c];
            if (color < 0) return false;
            int cnt = 1;
            for (int cc = c - 1; cc >= 0 && _grid[r, cc] == color; cc--) cnt++;
            for (int cc = c + 1; cc < Cols && _grid[r, cc] == color; cc++) cnt++;
            if (cnt >= 3) return true;
            cnt = 1;
            for (int rr = r - 1; rr >= 0 && _grid[rr, c] == color; rr--) cnt++;
            for (int rr = r + 1; rr < Rows && _grid[rr, c] == color; rr++) cnt++;
            return cnt >= 3;
        }

        // ---------- Ход игрока ----------

        internal Canvas Canvas
        {
            get
            {
                if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
                return _canvas;
            }
        }

        internal RectTransform BoardRect => _boardRect;

        internal void TryPlayerSwap(Match3Gem gem, Vector2 dragDeltaLocal)
        {
            if (_isCompleted || IsInTransition || _resolving) return;
            if (gem == null) return;
            if (dragDeltaLocal.magnitude < DragThreshold) return;

            int dr = 0, dc = 0;
            if (Mathf.Abs(dragDeltaLocal.x) >= Mathf.Abs(dragDeltaLocal.y))
            {
                dc = dragDeltaLocal.x > 0 ? 1 : -1;
            }
            else
            {
                // локальная ось Y растёт вверх; строка 0 сверху -> вверх это dr = -1
                dr = dragDeltaLocal.y > 0 ? -1 : 1;
            }

            int nr = gem.Row + dr;
            int nc = gem.Col + dc;
            if (nr < 0 || nr >= Rows || nc < 0 || nc >= Cols) return;

            StartCoroutine(ResolveSwap(gem.Row, gem.Col, nr, nc));
        }

        private IEnumerator ResolveSwap(int r1, int c1, int r2, int c2)
        {
            _resolving = true;

            (_grid[r1, c1], _grid[r2, c2]) = (_grid[r2, c2], _grid[r1, c1]);
            UpdateGemVisuals();

            var cleared = new bool[Rows, Cols];
            bool anyMatch = FindAllMatches(cleared);

            _movesLeft = Mathf.Max(0, _movesLeft - 1);

            if (!anyMatch)
            {
                // Нет совпадения — обмен откатывается, но ход потрачен.
                yield return new WaitForSeconds(0.12f);
                (_grid[r1, c1], _grid[r2, c2]) = (_grid[r2, c2], _grid[r1, c1]);
                UpdateGemVisuals();
                UpdateStatusText();
                CheckLossOrProgress();
                _resolving = false;
                yield break;
            }

            _combos++;

            do
            {
                for (int r = 0; r < Rows; r++)
                    for (int c = 0; c < Cols; c++)
                        if (cleared[r, c]) _grid[r, c] = -1;

                DropAndRefill();
                UpdateGemVisuals();
                yield return new WaitForSeconds(0.15f);

                cleared = new bool[Rows, Cols];
                anyMatch = FindAllMatches(cleared);
            } while (anyMatch);

            int reshuffleGuard = 0;
            while (!HasAnyPossibleMove() && reshuffleGuard < 30)
            {
                ShuffleGridColors();
                CleanupInitialMatches();
                reshuffleGuard++;
                UpdateGemVisuals();
            }

            UpdateStatusText();
            SetProgress(Mathf.Clamp01((float)_combos / Mathf.Max(1, _targetCombos)));

            if (_combos >= _targetCombos)
            {
                _resolving = false;
                CompleteMechanic();
                yield break;
            }

            CheckLossOrProgress();
            _resolving = false;
        }

        private void CheckLossOrProgress()
        {
            SetProgress(Mathf.Clamp01((float)_combos / Mathf.Max(1, _targetCombos)));
            if (_movesLeft <= 0 && _combos < _targetCombos)
            {
                if (_statusText != null) _statusText.text = "<color=#FF6666>Ходы закончились.</color>";
                FailStage("Ходы закончились");
            }
        }

        private void CleanupInitialMatches()
        {
            int guard = 0;
            var cleared = new bool[Rows, Cols];
            while (FindAllMatches(cleared) && guard < 20)
            {
                for (int r = 0; r < Rows; r++)
                    for (int c = 0; c < Cols; c++)
                        if (cleared[r, c]) _grid[r, c] = -1;
                DropAndRefill();
                cleared = new bool[Rows, Cols];
                guard++;
            }
        }

        private void DropAndRefill()
        {
            for (int c = 0; c < Cols; c++)
            {
                int write = Rows - 1;
                for (int r = Rows - 1; r >= 0; r--)
                {
                    if (_grid[r, c] >= 0)
                    {
                        int v = _grid[r, c];
                        _grid[r, c] = -1;
                        _grid[write, c] = v;
                        write--;
                    }
                }
                for (int r = write; r >= 0; r--)
                {
                    int color;
                    int guard = 0;
                    do
                    {
                        color = _rng.Next(0, COLOR_CHARS.Length);
                        guard++;
                    } while (guard < 60 && MakesImmediateMatch(r, c, color));
                    _grid[r, c] = color;
                }
            }
        }

        private bool FindAllMatches(bool[,] cleared)
        {
            bool any = false;
            for (int r = 0; r < Rows; r++)
            {
                int runStart = 0;
                for (int c = 1; c <= Cols; c++)
                {
                    bool boundary = c == Cols || _grid[r, c] != _grid[r, runStart] || _grid[r, runStart] < 0;
                    if (boundary)
                    {
                        int len = c - runStart;
                        if (len >= 3 && _grid[r, runStart] >= 0)
                        {
                            for (int k = runStart; k < c; k++) { cleared[r, k] = true; any = true; }
                        }
                        runStart = c;
                    }
                }
            }
            for (int c = 0; c < Cols; c++)
            {
                int runStart = 0;
                for (int r = 1; r <= Rows; r++)
                {
                    bool boundary = r == Rows || _grid[r, c] != _grid[runStart, c] || _grid[runStart, c] < 0;
                    if (boundary)
                    {
                        int len = r - runStart;
                        if (len >= 3 && _grid[runStart, c] >= 0)
                        {
                            for (int k = runStart; k < r; k++) { cleared[k, c] = true; any = true; }
                        }
                        runStart = r;
                    }
                }
            }
            return any;
        }

        private void UpdateStatusText()
        {
            if (_statusText != null)
            {
                _statusText.text = $"Комбинации: {_combos}/{_targetCombos}    Ходов осталось: {_movesLeft}";
            }
        }

        private string BuildBoardColorsString()
        {
            var sb = new StringBuilder(Rows * (Cols + 1));
            for (int r = 0; r < Rows; r++)
            {
                if (r > 0) sb.Append('/');
                for (int c = 0; c < Cols; c++)
                {
                    int col = _grid[r, c];
                    sb.Append(col >= 0 && col < COLOR_CHARS.Length ? COLOR_CHARS[col] : '?');
                }
            }
            return sb.ToString();
        }
    }
}
