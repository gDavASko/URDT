using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M16_RhythmPhaseDetector
{
    /// <summary>
    /// Механика #16: Ритм-дорожки. Три вертикальные дорожки, кубики-ноты падают сверху,
    /// игрок нажимает кнопку дорожки в момент, когда кубик пересекает линию попадания.
    /// Три этапа с растущей скоростью, сужающимся окном и жёстким лимитом ошибок.
    /// ВНИМАНИЕ: имя файла, namespace и имя класса менять нельзя — префаб ссылается по GUID.
    /// </summary>
    public class M16_RhythmPhaseDetectorMechanic : BaseMechanic2DModule
    {
        // Геометрия поля (локальные координаты в rect'е доски).
        private const float BoardWidth = 700f;
        private const float BoardHeight = 560f;
        private const float NoteSize = 70f;
        private const float MinLaneSpacing = 90f; // мин. расстояние между двумя нотами одной дорожки при спавне
        private const float MinSpawnGap = 0.40f;  // мин. время между любыми спавнами
        private const float MaxSpawnGap = 0.95f;
        private const float ReadDelay = 1.2f;

        // Локальные X центров трёх дорожек и Y линии попадания (в системе координат доски, Y вверх).
        private static readonly float[] LaneOffsets = { -220f, 0f, 220f };
        private const float HitLineLocalY = -200f;
        private const float SpawnLocalY = 260f;
        private const float DespawnLocalY = -300f;

        // Параметры этапов.
        private static readonly int[]   _stageTargetHits  = { 12, 16, 20 };
        private static readonly float[] _stageNoteSpeed   = { 300f, 380f, 460f };
        private static readonly float[] _stageHitWindow   = { 48f, 40f, 34f };
        private static readonly int[]   _stageErrorBudget = { 3, 2, 1 };

        private static readonly Color BgColor        = new Color(0.09f, 0.10f, 0.14f, 1f);
        private static readonly Color BoardColor     = new Color(0.14f, 0.17f, 0.22f, 1f);
        private static readonly Color LaneAColor     = new Color(0.19f, 0.24f, 0.31f, 1f);
        private static readonly Color LaneBColor     = new Color(0.17f, 0.22f, 0.29f, 1f);
        private static readonly Color HitLineColor   = new Color(1f, 0.85f, 0.25f, 1f);
        private static readonly Color NoteColor      = new Color(0.30f, 0.85f, 1f, 1f);
        private static readonly Color[] LaneButtonColors =
        {
            new Color(0.90f, 0.35f, 0.35f, 1f),
            new Color(0.35f, 0.85f, 0.55f, 1f),
            new Color(0.40f, 0.55f, 0.95f, 1f),
        };
        private static readonly string[] LaneCaptions = { "ЛЕВО", "ЦЕНТР", "ПРАВО" };

        // Построенные объекты (пересобираются при каждом Initialize).
        private RectTransform _board;
        private RectTransform _hitLine;
        private TMP_Text _titleText;
        private TMP_Text _statusText;
        private readonly Button[] _laneButtons = new Button[3];

        // Живые ноты.
        private readonly List<Note> _notes = new List<Note>();
        private int _noteCounter;

        // Игровое состояние.
        private int _hits;
        private int _errors;
        private float _spawnTimer;
        private float _startTimer;

        // Публичные read-only свойства для AI/тестов.
        public override int StageCount => 3;
        public int Hits => _hits;
        public int TargetHits => _stageTargetHits[Mathf.Clamp(CurrentStage - 1, 0, 2)];
        public int Errors => _errors;
        public int ErrorBudget => _stageErrorBudget[Mathf.Clamp(CurrentStage - 1, 0, 2)];
        public float NoteSpeed => _stageNoteSpeed[Mathf.Clamp(CurrentStage - 1, 0, 2)];
        public float HitWindow => _stageHitWindow[Mathf.Clamp(CurrentStage - 1, 0, 2)];
        public float HitLineY => HitLineLocalY;
        public int LaneCount => 3;
        public string LaneX => string.Join(",", System.Array.ConvertAll(LaneOffsets, v => v.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)));

        public string NotesState
        {
            get
            {
                var live = new List<Note>(_notes);
                live.Sort((a, b) =>
                {
                    float da = Mathf.Abs(a.LocalY - HitLineLocalY);
                    float db = Mathf.Abs(b.LocalY - HitLineLocalY);
                    return da.CompareTo(db);
                });
                var sb = new StringBuilder();
                for (int i = 0; i < live.Count; i++)
                {
                    if (i > 0) sb.Append(';');
                    Note n = live[i];
                    sb.Append(n.Lane).Append(':').Append((n.LocalY - HitLineLocalY).ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, 2);
            int need = _stageTargetHits[idx];
            int budget = _stageErrorBudget[idx];
            return $"Этап {stage}/3: нажимайте кнопку дорожки, когда кубик на линии. {need} попаданий, не больше {budget} ошибок.";
        }

        public override void Initialize()
        {
            // Полностью зачищаем поле — префаб может нести старых авторских детей (рычаги, метку, бак…).
            var t = transform;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(t.GetChild(i).gameObject);
            }
            _notes.Clear();
            _noteCounter = 0;
            _hits = 0;
            _errors = 0;
            _spawnTimer = 0f;
            _startTimer = ReadDelay;

            BuildBoard();
            RefreshStatus();
            base.Initialize();
            SetProgress(0f);
        }

        public override void ResetMechanic() { Initialize(); }

        // ---------------- BUILD ----------------

        private void BuildBoard()
        {
            var rootRt = transform as RectTransform;
            if (rootRt == null)
            {
                rootRt = gameObject.AddComponent<RectTransform>();
            }

            // Фон корня — не ловит клики.
            CreateImage("Background", rootRt, BgColor, Vector2.zero, new Vector2(1100f, 600f), raycast: false);

            // Заголовок сверху.
            _titleText = CreateText("Instruction", rootRt, GetStageInstruction(CurrentStage),
                new Vector2(0f, 260f), new Vector2(1000f, 60f), fontSize: 28, color: Color.white);
            _titleText.alignment = TextAlignmentOptions.Center;

            // Строка статуса под заголовком.
            _statusText = CreateText("StatusLabel", rootRt, "",
                new Vector2(0f, 218f), new Vector2(1000f, 40f), fontSize: 22, color: new Color(0.9f, 0.9f, 0.9f, 1f));
            _statusText.alignment = TextAlignmentOptions.Center;

            // Доска (Field) — контейнер для дорожек и нот.
            var boardGo = new GameObject("Field", typeof(RectTransform), typeof(Image));
            _board = (RectTransform)boardGo.transform;
            _board.SetParent(rootRt, false);
            _board.anchorMin = _board.anchorMax = new Vector2(0.5f, 0.5f);
            _board.pivot = new Vector2(0.5f, 0.5f);
            _board.sizeDelta = new Vector2(BoardWidth, BoardHeight);
            _board.anchoredPosition = new Vector2(0f, -20f);
            var boardImg = boardGo.GetComponent<Image>();
            boardImg.color = BoardColor;
            boardImg.raycastTarget = false;

            // Три вертикальные полосы-дорожки (подложка).
            for (int i = 0; i < 3; i++)
            {
                var laneName = $"Lane_{i}";
                var laneCol = (i % 2 == 0) ? LaneAColor : LaneBColor;
                CreateImage(laneName, _board, laneCol,
                    new Vector2(LaneOffsets[i], 0f),
                    new Vector2(180f, BoardHeight - 8f), raycast: false);
            }

            // Линия попадания.
            var hitLineGo = new GameObject("HitLine", typeof(RectTransform), typeof(Image));
            _hitLine = (RectTransform)hitLineGo.transform;
            _hitLine.SetParent(_board, false);
            _hitLine.anchorMin = _hitLine.anchorMax = new Vector2(0.5f, 0.5f);
            _hitLine.pivot = new Vector2(0.5f, 0.5f);
            _hitLine.sizeDelta = new Vector2(BoardWidth - 20f, 6f);
            _hitLine.anchoredPosition = new Vector2(0f, HitLineLocalY);
            var lineImg = hitLineGo.GetComponent<Image>();
            lineImg.color = HitLineColor;
            lineImg.raycastTarget = false;

            // Кнопки дорожек под доской.
            for (int i = 0; i < 3; i++)
            {
                int laneIndex = i; // capture
                var btnGo = new GameObject($"BtnLane_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                var btnRt = (RectTransform)btnGo.transform;
                btnRt.SetParent(rootRt, false);
                btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0.5f);
                btnRt.pivot = new Vector2(0.5f, 0.5f);
                btnRt.sizeDelta = new Vector2(210f, 100f);
                // Кнопка сдвинута по X относительно центра, ниже доски.
                float bx = LaneOffsets[i] * (1100f / BoardWidth) * 0.55f;
                btnRt.anchoredPosition = new Vector2(LaneOffsets[i], -235f);

                var img = btnGo.GetComponent<Image>();
                img.color = LaneButtonColors[i];
                img.raycastTarget = true;

                var btn = btnGo.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => OnLanePressed(laneIndex));
                _laneButtons[i] = btn;

                var lbl = CreateText($"BtnLane_{i}_Label", btnRt, LaneCaptions[i],
                    Vector2.zero, new Vector2(200f, 90f), fontSize: 30, color: Color.white);
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.fontStyle = FontStyles.Bold;
            }
        }

        private static Image CreateImage(string name, RectTransform parent, Color color, Vector2 pos, Vector2 size, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, string text, Vector2 pos, Vector2 size, int fontSize, Color color)
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
            t.color = color;
            t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = true;
            return t;
        }

        // ---------------- LOOP ----------------

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;
            float dt = Time.deltaTime;

            // Пауза-«прочитайте» перед началом падения.
            if (_startTimer > 0f)
            {
                _startTimer -= dt;
                return;
            }

            // Движение нот вниз.
            float speed = NoteSpeed;
            float window = HitWindow;
            for (int i = _notes.Count - 1; i >= 0; i--)
            {
                var n = _notes[i];
                n.LocalY -= speed * dt;
                if (n.Rt != null) n.Rt.anchoredPosition = new Vector2(LaneOffsets[n.Lane], n.LocalY);

                // Пропуск: нота прошла ниже линии на HitWindow.
                if (n.LocalY < HitLineLocalY - window)
                {
                    _notes.RemoveAt(i);
                    if (n.Go != null) Destroy(n.Go);
                    RegisterError();
                    if (_isCompleted || IsInTransition) return;
                }
                else if (n.LocalY < DespawnLocalY)
                {
                    _notes.RemoveAt(i);
                    if (n.Go != null) Destroy(n.Go);
                }
            }

            // Спавн.
            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                TrySpawnNote();
            }

            RefreshStatus();
        }

        private void TrySpawnNote()
        {
            // Кандидаты — дорожки, у которых верхняя нота ниже (SpawnY - MinLaneSpacing).
            var candidates = new List<int>(3);
            for (int lane = 0; lane < 3; lane++)
            {
                float topY = float.NegativeInfinity;
                for (int j = 0; j < _notes.Count; j++)
                {
                    if (_notes[j].Lane == lane && _notes[j].LocalY > topY) topY = _notes[j].LocalY;
                }
                if (topY <= SpawnLocalY - MinLaneSpacing || topY == float.NegativeInfinity)
                {
                    candidates.Add(lane);
                }
            }
            if (candidates.Count == 0)
            {
                _spawnTimer = 0.1f; // подождать освобождения
                return;
            }
            int pick = candidates[Random.Range(0, candidates.Count)];
            SpawnNote(pick);
            _spawnTimer = Random.Range(MinSpawnGap, MaxSpawnGap);
        }

        private void SpawnNote(int lane)
        {
            var go = new GameObject($"Note_{_noteCounter}", typeof(RectTransform), typeof(Image));
            _noteCounter++;
            var rt = (RectTransform)go.transform;
            rt.SetParent(_board, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(NoteSize, NoteSize);
            rt.anchoredPosition = new Vector2(LaneOffsets[lane], SpawnLocalY);
            var img = go.GetComponent<Image>();
            img.color = NoteColor;
            img.raycastTarget = false;

            var noteComp = go.AddComponent<RhythmNote>();
            noteComp.Setup(this, lane);

            var n = new Note { Go = go, Rt = rt, Lane = lane, LocalY = SpawnLocalY, Component = noteComp };
            _notes.Add(n);
        }

        // ---------------- INPUT ----------------

        private void OnLanePressed(int lane)
        {
            if (_isCompleted || IsInTransition) return;
            if (_startTimer > 0f) return; // ноты ещё не пошли

            // Ищем самую нижнюю ноту этой дорожки в окне попадания.
            int bestIdx = -1;
            float bestY = float.PositiveInfinity;
            for (int i = 0; i < _notes.Count; i++)
            {
                if (_notes[i].Lane != lane) continue;
                float y = _notes[i].LocalY;
                if (y < bestY) { bestY = y; bestIdx = i; }
            }
            float win = HitWindow;
            if (bestIdx >= 0 && Mathf.Abs(bestY - HitLineLocalY) <= win)
            {
                var n = _notes[bestIdx];
                _notes.RemoveAt(bestIdx);
                if (n.Go != null) Destroy(n.Go);
                _hits++;
                int need = TargetHits;
                SetProgress(Mathf.Clamp01((float)_hits / need));
                RefreshStatus();
                if (_hits >= need) { CompleteMechanic(); return; }
            }
            else
            {
                RegisterError();
            }
        }

        private void RegisterError()
        {
            _errors++;
            RefreshStatus();
            if (_errors > ErrorBudget)
            {
                FailStage("Сбит ритм: слишком много промахов");
            }
        }

        private void RefreshStatus()
        {
            if (_statusText != null)
            {
                _statusText.text = $"Попадания: {_hits}/{TargetHits} · Ошибки: {_errors}/{ErrorBudget}";
            }
            if (_titleText != null)
            {
                _titleText.text = GetStageInstruction(CurrentStage);
            }
        }

        // Публичный доступ для компонента ноты (расчёт DistanceToHitLine на лету).
        internal float GetHitLineLocalY() => HitLineLocalY;

        private class Note
        {
            public GameObject Go;
            public RectTransform Rt;
            public int Lane;
            public float LocalY;
            public RhythmNote Component;
        }
    }

    /// <summary>
    /// Публичный компонент падающей ноты. AI видит поля Lane и DistanceToHitLine.
    /// </summary>
    public class RhythmNote : MonoBehaviour
    {
        private M16_RhythmPhaseDetectorMechanic _owner;
        private int _lane;

        public int Lane => _lane;

        public float DistanceToHitLine
        {
            get
            {
                var rt = transform as RectTransform;
                if (rt == null || _owner == null) return 0f;
                return rt.anchoredPosition.y - _owner.HitLineY;
            }
        }

        public void Setup(M16_RhythmPhaseDetectorMechanic owner, int lane)
        {
            _owner = owner;
            _lane = lane;
        }
    }
}
