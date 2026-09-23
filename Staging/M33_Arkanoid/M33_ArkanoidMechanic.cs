using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M33_Arkanoid
{
    /// <summary>
    /// Механика #33: Аркада «Арканоид». Ракетка внизу, мяч, стенка кирпичей вверху.
    /// Управление: тяните пальцем/курсором по полю — ракетка следует за X. Пока мяч лежит на ракетке,
    /// клик по полю запускает его вверх с небольшим углом. Отражение от ракетки зависит от точки удара.
    /// Провал этапа — мяч упал ниже линии ракетки.
    /// UI собирается кодом целиком в <see cref="Initialize"/>.
    /// </summary>
    public class M33_ArkanoidMechanic : BaseMechanic2DModule
    {
        // Геометрия поля и объектов
        private const float FIELD_W = 1100f;
        private const float FIELD_H = 600f;
        private const float PADDLE_H = 18f;
        private const float PADDLE_Y = -240f;
        private const float BALL_RADIUS = 14f;
        private const int BRICK_COLS = 4;
        private const float BRICK_W = 180f;
        private const float BRICK_H = 40f;
        private const float BRICK_GAP = 12f;
        private const float BRICK_TOP = 240f;

        // Параметры по этапам
        private static readonly int[]   STAGE_ROWS         = { 2, 3 };
        private static readonly float[] STAGE_BALL_SPEED   = { 380f, 460f };
        private static readonly float[] STAGE_PADDLE_WIDTH = { 180f, 144f };

        // Построенный UI
        private RectTransform _field;
        private RectTransform _paddle;
        private RectTransform _ball;
        private RectTransform _bricksRoot;
        private Image _paddleImage;
        private TMP_Text _instructionText;
        private TMP_Text _statusText;
        private readonly List<M33_Brick> _bricks = new List<M33_Brick>();

        // Ввод / камера
        private Canvas _canvas;

        // Состояние
        private float _paddleWidth = 180f;
        private float _ballSpeed = 380f;
        private bool _ballLaunched;
        private Vector2 _ballPos;
        private Vector2 _ballVel;
        private float _paddleX;
        private int _bricksLeft;
        private int _bricksTotal;

        // Публичные read-only свойства (видит AI как game.*)
        public int BricksLeft => _bricksLeft;
        public int BricksTotal => _bricksTotal;
        public bool BallLaunched => _ballLaunched;
        public float BallX => _ballPos.x;
        public float BallY => _ballPos.y;
        public float BallVX => _ballVel.x;
        public float BallVY => _ballVel.y;
        public float PaddleX => _paddleX;
        public float PaddleWidth => _paddleWidth;
        public float FieldWidth => FIELD_W;
        public float FieldHeight => FIELD_H;
        public float PaddleY => PADDLE_Y;

        public override int StageCount => 2;

        public override void Initialize()
        {
            BuildBoard();
            base.Initialize();

            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_ROWS.Length - 1);
            _paddleWidth = STAGE_PADDLE_WIDTH[idx];
            _ballSpeed = STAGE_BALL_SPEED[idx];

            if (_paddle != null)
            {
                _paddle.sizeDelta = new Vector2(_paddleWidth, PADDLE_H);
            }

            _paddleX = 0f;
            if (_paddle != null) _paddle.anchoredPosition = new Vector2(_paddleX, PADDLE_Y);

            _ballLaunched = false;
            _ballVel = Vector2.zero;
            _ballPos = new Vector2(_paddleX, PADDLE_Y + PADDLE_H * 0.5f + BALL_RADIUS);
            if (_ball != null) _ball.anchoredPosition = _ballPos;

            if (_instructionText != null) _instructionText.text = GetStageInstruction(CurrentStage);
            UpdateStatusText();
            SetProgress(0f);
        }

        protected override string GetStageInstruction(int stage)
        {
            switch (stage)
            {
                case 1: return "Этап 1/2. Тяните пальцем по полю — ракетка едет за курсором. Клик — запуск мяча. Разбейте 8 кирпичей.";
                case 2: return "Этап 2/2. Ракетка стала уже, мяч быстрее. Разбейте 12 кирпичей и не упустите мяч!";
                default: return _instruction;
            }
        }

        // -------- Построение UI --------

        private void BuildBoard()
        {
            _canvas = GetComponentInParent<Canvas>();

            // Уничтожаем всех детей, чтобы не остались «призраки» с прежними именами в этом же кадре.
            var root = (RectTransform)transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(root.GetChild(i).gameObject);
            }
            _bricks.Clear();

            // Поле (Field) — ловит указатель для управления ракеткой и запуска мяча.
            _field = CreateRect("Field", root, new Vector2(FIELD_W, FIELD_H), Vector2.zero);
            var fieldBg = _field.gameObject.AddComponent<Image>();
            fieldBg.color = new Color(0.09f, 0.11f, 0.18f, 1f);
            fieldBg.raycastTarget = true;
            var input = _field.gameObject.AddComponent<M33_FieldInput>();
            input.Setup(this, _field);

            // Тонкие рамки стен (визуал), клики не ловят
            CreateBorder("Wall_Left",   _field, new Vector2(6f, FIELD_H),      new Vector2(-FIELD_W * 0.5f + 3f, 0f));
            CreateBorder("Wall_Right",  _field, new Vector2(6f, FIELD_H),      new Vector2(FIELD_W * 0.5f - 3f, 0f));
            CreateBorder("Wall_Top",    _field, new Vector2(FIELD_W, 6f),      new Vector2(0f, FIELD_H * 0.5f - 3f));
            CreateBorder("Line_Paddle", _field, new Vector2(FIELD_W, 2f),      new Vector2(0f, PADDLE_Y - PADDLE_H * 0.5f - 6f));

            // Кирпичи
            _bricksRoot = CreateRect("Bricks", _field, new Vector2(FIELD_W, FIELD_H), Vector2.zero);
            var bg = _bricksRoot.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = false;

            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_ROWS.Length - 1);
            int rows = STAGE_ROWS[idx];
            float totalW = BRICK_COLS * BRICK_W + (BRICK_COLS - 1) * BRICK_GAP;
            float startX = -totalW * 0.5f + BRICK_W * 0.5f;
            Color[] rowColors = { new Color(0.95f, 0.35f, 0.35f), new Color(0.35f, 0.75f, 0.95f), new Color(0.95f, 0.85f, 0.35f) };
            _bricksTotal = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < BRICK_COLS; c++)
                {
                    string n = $"Brick_{r}_{c}";
                    var rt = CreateRect(n, _bricksRoot, new Vector2(BRICK_W, BRICK_H),
                        new Vector2(startX + c * (BRICK_W + BRICK_GAP), BRICK_TOP - r * (BRICK_H + BRICK_GAP)));
                    var im = rt.gameObject.AddComponent<Image>();
                    im.color = rowColors[r % rowColors.Length];
                    im.raycastTarget = false;
                    var brick = rt.gameObject.AddComponent<M33_Brick>();
                    brick.Setup(r, c);
                    _bricks.Add(brick);
                    _bricksTotal++;
                }
            }
            _bricksLeft = _bricksTotal;

            // Ракетка
            _paddle = CreateRect("Paddle", _field, new Vector2(STAGE_PADDLE_WIDTH[idx], PADDLE_H), new Vector2(0f, PADDLE_Y));
            _paddleImage = _paddle.gameObject.AddComponent<Image>();
            _paddleImage.color = new Color(0.85f, 0.9f, 1f, 1f);
            _paddleImage.raycastTarget = false;

            // Мяч
            _ball = CreateRect("Ball", _field, new Vector2(BALL_RADIUS * 2f, BALL_RADIUS * 2f),
                new Vector2(0f, PADDLE_Y + PADDLE_H * 0.5f + BALL_RADIUS));
            var ballImage = _ball.gameObject.AddComponent<Image>();
            ballImage.color = new Color(1f, 0.95f, 0.55f, 1f);
            ballImage.raycastTarget = false;

            // Заголовок / инструкция сверху
            _instructionText = CreateText("InstructionText", root, new Vector2(FIELD_W, 60f), new Vector2(0f, FIELD_H * 0.5f + 40f), 26, TextAlignmentOptions.Center);
            _instructionText.color = Color.white;

            // Статус (счёт кирпичей)
            _statusText = CreateText("StatusText", _field, new Vector2(FIELD_W - 20f, 40f),
                new Vector2(0f, FIELD_H * 0.5f - 24f), 22, TextAlignmentOptions.Left);
            _statusText.color = new Color(0.85f, 0.9f, 1f, 1f);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            rt.localScale = Vector3.one;
            return rt;
        }

        private static void CreateBorder(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var rt = CreateRect(name, parent, size, pos);
            var im = rt.gameObject.AddComponent<Image>();
            im.color = new Color(0.55f, 0.6f, 0.75f, 0.9f);
            im.raycastTarget = false;
        }

        private static TMP_Text CreateText(string name, Transform parent, Vector2 size, Vector2 pos, int fontSize, TextAlignmentOptions align)
        {
            var rt = CreateRect(name, parent, size, pos);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            t.text = string.Empty;
            return t;
        }

        // -------- Ввод от Field --------

        internal void OnFieldPointerLocal(Vector2 local, bool isClick)
        {
            if (_isCompleted || IsInTransition) return;
            // Ракетка следует за X указателя, зажатая внутри поля.
            float half = _paddleWidth * 0.5f;
            _paddleX = Mathf.Clamp(local.x, -FIELD_W * 0.5f + half, FIELD_W * 0.5f - half);
            if (_paddle != null) _paddle.anchoredPosition = new Vector2(_paddleX, PADDLE_Y);
            if (!_ballLaunched)
            {
                _ballPos = new Vector2(_paddleX, PADDLE_Y + PADDLE_H * 0.5f + BALL_RADIUS);
                if (_ball != null) _ball.anchoredPosition = _ballPos;
                if (isClick)
                {
                    LaunchBall();
                }
            }
        }

        private void LaunchBall()
        {
            _ballLaunched = true;
            // Небольшой угол от вертикали (~15°), сторона по знаку paddleX.
            float sign = _paddleX >= 0f ? 1f : -1f;
            float ang = 15f * Mathf.Deg2Rad;
            _ballVel = new Vector2(Mathf.Sin(ang) * sign, Mathf.Cos(ang)) * _ballSpeed;
        }

        // -------- Кинематика мяча --------

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;

            if (!_ballLaunched)
            {
                _ballPos = new Vector2(_paddleX, PADDLE_Y + PADDLE_H * 0.5f + BALL_RADIUS);
                if (_ball != null) _ball.anchoredPosition = _ballPos;
                return;
            }

            float dt = Time.deltaTime;
            // Субшаги, чтобы не «прошивать» кирпичи и стены
            float stepMax = BALL_RADIUS * 0.6f;
            float travel = _ballVel.magnitude * dt;
            int steps = Mathf.Max(1, Mathf.CeilToInt(travel / stepMax));
            float subDt = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                StepBall(subDt);
                if (_isCompleted || IsInTransition) return;
            }

            if (_ball != null) _ball.anchoredPosition = _ballPos;
            UpdateStatusText();
        }

        private void StepBall(float dt)
        {
            _ballPos += _ballVel * dt;

            // Стенки
            float xMin = -FIELD_W * 0.5f + BALL_RADIUS;
            float xMax = FIELD_W * 0.5f - BALL_RADIUS;
            float yMax = FIELD_H * 0.5f - BALL_RADIUS;
            if (_ballPos.x < xMin) { _ballPos.x = xMin; if (_ballVel.x < 0f) _ballVel.x = -_ballVel.x; }
            if (_ballPos.x > xMax) { _ballPos.x = xMax; if (_ballVel.x > 0f) _ballVel.x = -_ballVel.x; }
            if (_ballPos.y > yMax) { _ballPos.y = yMax; if (_ballVel.y > 0f) _ballVel.y = -_ballVel.y; }

            // Ракетка (только сверху, при падении)
            if (_ballVel.y < 0f)
            {
                float paddleTop = PADDLE_Y + PADDLE_H * 0.5f;
                float paddleBottom = PADDLE_Y - PADDLE_H * 0.5f;
                float half = _paddleWidth * 0.5f;
                if (_ballPos.y - BALL_RADIUS <= paddleTop && _ballPos.y >= paddleBottom
                    && _ballPos.x >= _paddleX - half - BALL_RADIUS && _ballPos.x <= _paddleX + half + BALL_RADIUS)
                {
                    // Отскок с зависящим от смещения углом (до ±60°)
                    float offset = Mathf.Clamp((_ballPos.x - _paddleX) / half, -1f, 1f);
                    float ang = offset * 60f * Mathf.Deg2Rad;
                    float speed = _ballVel.magnitude;
                    if (speed < 1f) speed = _ballSpeed;
                    _ballVel = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * speed;
                    _ballPos.y = paddleTop + BALL_RADIUS + 0.5f;
                }
            }

            // Кирпичи (AABB против круга)
            for (int i = 0; i < _bricks.Count; i++)
            {
                var b = _bricks[i];
                if (b == null || b.IsDestroyed) continue;
                var rt = (RectTransform)b.transform;
                Vector2 p = rt.anchoredPosition;
                float bw = BRICK_W * 0.5f;
                float bh = BRICK_H * 0.5f;
                float dx = _ballPos.x - Mathf.Clamp(_ballPos.x, p.x - bw, p.x + bw);
                float dy = _ballPos.y - Mathf.Clamp(_ballPos.y, p.y - bh, p.y + bh);
                if (dx * dx + dy * dy <= BALL_RADIUS * BALL_RADIUS)
                {
                    // Определяем, по какой оси меньше проникновение
                    float overlapX = (bw + BALL_RADIUS) - Mathf.Abs(_ballPos.x - p.x);
                    float overlapY = (bh + BALL_RADIUS) - Mathf.Abs(_ballPos.y - p.y);
                    if (overlapX < overlapY)
                    {
                        _ballVel.x = -_ballVel.x;
                        _ballPos.x += (_ballPos.x > p.x ? 1f : -1f) * overlapX;
                    }
                    else
                    {
                        _ballVel.y = -_ballVel.y;
                        _ballPos.y += (_ballPos.y > p.y ? 1f : -1f) * overlapY;
                    }
                    b.Destroy();
                    _bricksLeft--;
                    SetProgress(_bricksTotal > 0 ? 1f - (float)_bricksLeft / _bricksTotal : 1f);
                    UpdateStatusText();
                    if (_bricksLeft <= 0)
                    {
                        CompleteMechanic();
                        return;
                    }
                    break; // не более одного кирпича за субшаг
                }
            }

            // Падение ниже линии ракетки
            if (_ballPos.y < PADDLE_Y - PADDLE_H * 0.5f - 4f)
            {
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#FF5555>Мяч упущен! Этап перезапустится.</color>";
                }
                FailStage("Мяч упущен");
            }
        }

        private void UpdateStatusText()
        {
            if (_statusText == null) return;
            _statusText.text = $"Кирпичи: {_bricksTotal - _bricksLeft} / {_bricksTotal}";
        }
    }

    /// <summary>Компонент кирпича: хранит координаты и состояние «разрушен».</summary>
    public class M33_Brick : MonoBehaviour
    {
        [SerializeField] private int _row;
        [SerializeField] private int _col;
        [SerializeField] private bool _destroyed;

        public int Row => _row;
        public int Col => _col;
        public bool IsDestroyed => _destroyed;

        public void Setup(int r, int c)
        {
            _row = r;
            _col = c;
            _destroyed = false;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Destroy()
        {
            _destroyed = true;
            gameObject.SetActive(false);
        }
    }

    /// <summary>Прокси ввода на поле: перехватывает клики и перетаскивания, пересылает механике.</summary>
    public class M33_FieldInput : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerClickHandler, IBeginDragHandler
    {
        private M33_ArkanoidMechanic _mechanic;
        private RectTransform _rect;
        private Canvas _canvas;

        public void Setup(M33_ArkanoidMechanic mechanic, RectTransform rect)
        {
            _mechanic = mechanic;
            _rect = rect;
            _canvas = mechanic != null ? mechanic.GetComponentInParent<Canvas>() : null;
        }

        private bool ToLocal(Vector2 screen, out Vector2 local)
        {
            if (_canvas == null && _mechanic != null) _canvas = _mechanic.GetComponentInParent<Canvas>();
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, cam, out local);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_mechanic == null) return;
            if (ToLocal(e.position, out var local)) _mechanic.OnFieldPointerLocal(local, false);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (_mechanic == null) return;
            if (ToLocal(e.position, out var local)) _mechanic.OnFieldPointerLocal(local, false);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_mechanic == null) return;
            if (ToLocal(e.position, out var local)) _mechanic.OnFieldPointerLocal(local, false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (_mechanic == null) return;
            if (ToLocal(e.position, out var local)) _mechanic.OnFieldPointerLocal(local, true);
        }
    }
}
