using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M37_AlternateRunner
{
    /// <summary>
    /// Механика #37: «Бегун: левой-правой». Игрок ведёт бегуна к финишу, чередуя кнопки
    /// «ЛЕВОЙ» и «ПРАВОЙ» в темпе окна этапа. Красная кнопка «ПРЫЖОК [X]» — провал.
    /// Правила темпа взяты из M16, но реализованы как бег: каждый корректный шаг двигает
    /// фигурку на track/TargetSteps вперёд. Три этапа: удлиняется дистанция и сужается окно.
    /// UI собирается кодом целиком в <see cref="Initialize"/>.
    /// </summary>
    public class M37_AlternateRunnerMechanic : BaseMechanic2DModule
    {
        // Геометрия поля
        private const float FIELD_W = 1100f;
        private const float FIELD_H = 600f;
        private const float TRACK_Y = 120f;
        private const float TRACK_H = 90f;
        private const float TRACK_MARGIN = 60f;      // отступ от края поля до старта/финиша
        private const float RUNNER_BODY_W = 40f;
        private const float RUNNER_BODY_H = 70f;
        private const float LEG_W = 12f;
        private const float LEG_H = 40f;
        private const float BUTTON_Y = -170f;
        private const float BUTTON_W = 320f;
        private const float BUTTON_H = 130f;
        private const float FALL_SECONDS = 0.5f;

        // Параметры по этапам: цель шагов и окно интервала.
        private static readonly int[]   STAGE_TARGET_STEPS = { 8, 10, 12 };
        private static readonly float[] STAGE_MIN_INTERVAL = { 0.25f, 0.32f, 0.38f };
        private static readonly float[] STAGE_MAX_INTERVAL = { 1.15f, 0.90f, 0.72f };

        // Построенный UI
        private RectTransform _field;
        private RectTransform _track;
        private RectTransform _finishFlag;
        private RectTransform _runner;
        private RectTransform _runnerBody;
        private RectTransform _runnerLegLeft;
        private RectTransform _runnerLegRight;
        private Button _btnLeft;
        private Button _btnRight;
        private Button _btnJunk;
        private TMP_Text _instructionText;
        private TMP_Text _statusText;

        // Состояние
        private int _steps;
        private int _lastFoot;    // 0 = ни один, 1 = левая, 2 = правая
        private float _lastStepTime;
        private float _runnerX;
        private float _runnerStartX;
        private float _runnerFinishX;
        private bool _isFallen;
        private float _fallTimer;
        private float _legPhase; // для анимации ног

        // Публичные read-only свойства (AI видит их как game.*)
        public override int StageCount => 3;
        public int Steps => _steps;
        public int TargetSteps => STAGE_TARGET_STEPS[Mathf.Clamp(CurrentStage - 1, 0, STAGE_TARGET_STEPS.Length - 1)];
        public float MinInterval => STAGE_MIN_INTERVAL[Mathf.Clamp(CurrentStage - 1, 0, STAGE_MIN_INTERVAL.Length - 1)];
        public float MaxInterval => STAGE_MAX_INTERVAL[Mathf.Clamp(CurrentStage - 1, 0, STAGE_MAX_INTERVAL.Length - 1)];
        public string NextFoot => _lastFoot == 0 ? "any" : (_lastFoot == 1 ? "right" : "left");
        public string LastFoot => _lastFoot == 0 ? "none" : (_lastFoot == 1 ? "left" : "right");
        public float RunnerX => _runnerX;
        public bool IsFallen => _isFallen;
        public float TimeSinceLastStep => _lastStepTime < 0f ? -1f : Mathf.Max(0f, Time.unscaledTime - _lastStepTime);

        // ------------ Построение / Reset ------------

        public override void Initialize()
        {
            BuildBoard();
            base.Initialize();

            _steps = 0;
            _lastFoot = 0;
            _lastStepTime = -1f;
            _isFallen = false;
            _fallTimer = 0f;
            _legPhase = 0f;

            _runnerStartX = -FIELD_W * 0.5f + TRACK_MARGIN + RUNNER_BODY_W * 0.5f;
            _runnerFinishX = FIELD_W * 0.5f - TRACK_MARGIN - RUNNER_BODY_W * 0.5f;
            _runnerX = _runnerStartX;
            ApplyRunnerTransform();

            if (_instructionText != null) _instructionText.text = GetStageInstruction(CurrentStage);
            UpdateStatusText();
            SetProgress(0f);
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, STAGE_TARGET_STEPS.Length - 1);
            int need = STAGE_TARGET_STEPS[idx];
            float lo = STAGE_MIN_INTERVAL[idx];
            float hi = STAGE_MAX_INTERVAL[idx];
            switch (stage)
            {
                case 1: return $"Этап 1/3: жмите ЛЕВОЙ и ПРАВОЙ по очереди в ровном темпе ({lo:F2}–{hi:F2} с). Не жмите ПРЫЖОК [X]! Нужно {need} шагов.";
                case 2: return $"Этап 2/3: окно уже {lo:F2}–{hi:F2} с, дистанция {need} шагов. Держите чередование ЛЕВОЙ ↔ ПРАВОЙ.";
                default: return $"Этап 3/3: жёсткий темп {lo:F2}–{hi:F2} с, {need} шагов до финиша. Один сбой — падение.";
            }
        }

        private void BuildBoard()
        {
            var root = (RectTransform)transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(root.GetChild(i).gameObject);
            }

            _field = CreateRect("Field", root, new Vector2(FIELD_W, FIELD_H), Vector2.zero);
            var fieldBg = _field.gameObject.AddComponent<Image>();
            fieldBg.color = new Color(0.10f, 0.14f, 0.20f, 1f);
            fieldBg.raycastTarget = false;

            // Инструкция сверху
            _instructionText = CreateText("InstructionText", root, new Vector2(FIELD_W, 60f),
                new Vector2(0f, FIELD_H * 0.5f + 40f), 24, TextAlignmentOptions.Center);
            _instructionText.color = Color.white;

            // Беговая дорожка
            _track = CreateRect("Track", _field, new Vector2(FIELD_W - TRACK_MARGIN * 0.5f, TRACK_H),
                new Vector2(0f, TRACK_Y));
            var trackImg = _track.gameObject.AddComponent<Image>();
            trackImg.color = new Color(0.35f, 0.28f, 0.20f, 1f);
            trackImg.raycastTarget = false;

            // Линия старта
            var startLine = CreateRect("StartLine", _track, new Vector2(6f, TRACK_H),
                new Vector2(-FIELD_W * 0.5f + TRACK_MARGIN, 0f));
            var slImg = startLine.gameObject.AddComponent<Image>();
            slImg.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
            slImg.raycastTarget = false;

            // Финишный флаг
            _finishFlag = CreateRect("FinishFlag", _track, new Vector2(80f, TRACK_H + 60f),
                new Vector2(FIELD_W * 0.5f - TRACK_MARGIN, 30f));
            var ffImg = _finishFlag.gameObject.AddComponent<Image>();
            ffImg.color = new Color(0.95f, 0.35f, 0.35f, 1f);
            ffImg.raycastTarget = false;

            // Бегун (корень) — сам двигается, ноги и тело — дети
            _runner = CreateRect("Runner", _track, new Vector2(RUNNER_BODY_W, RUNNER_BODY_H + LEG_H),
                new Vector2(-FIELD_W * 0.5f + TRACK_MARGIN + RUNNER_BODY_W * 0.5f, 0f));
            var rimg = _runner.gameObject.AddComponent<Image>();
            rimg.color = new Color(0f, 0f, 0f, 0f); // прозрачный корень
            rimg.raycastTarget = false;

            _runnerBody = CreateRect("RunnerBody", _runner, new Vector2(RUNNER_BODY_W, RUNNER_BODY_H),
                new Vector2(0f, LEG_H * 0.5f));
            var bimg = _runnerBody.gameObject.AddComponent<Image>();
            bimg.color = new Color(0.35f, 0.85f, 1f, 1f);
            bimg.raycastTarget = false;

            _runnerLegLeft = CreateRect("RunnerLegLeft", _runner, new Vector2(LEG_W, LEG_H),
                new Vector2(-RUNNER_BODY_W * 0.25f, -RUNNER_BODY_H * 0.25f));
            var llImg = _runnerLegLeft.gameObject.AddComponent<Image>();
            llImg.color = new Color(0.2f, 0.6f, 0.85f, 1f);
            llImg.raycastTarget = false;

            _runnerLegRight = CreateRect("RunnerLegRight", _runner, new Vector2(LEG_W, LEG_H),
                new Vector2(RUNNER_BODY_W * 0.25f, -RUNNER_BODY_H * 0.25f));
            var lrImg = _runnerLegRight.gameObject.AddComponent<Image>();
            lrImg.color = new Color(0.2f, 0.6f, 0.85f, 1f);
            lrImg.raycastTarget = false;

            // Кнопки
            _btnLeft = CreateButton("BtnLeftFoot", _field, new Vector2(BUTTON_W, BUTTON_H),
                new Vector2(-FIELD_W * 0.5f + TRACK_MARGIN + BUTTON_W * 0.5f, BUTTON_Y),
                "ЛЕВОЙ", new Color(0.35f, 0.75f, 0.95f, 1f));
            _btnLeft.onClick.AddListener(() => OnFootPressed(1));

            _btnRight = CreateButton("BtnRightFoot", _field, new Vector2(BUTTON_W, BUTTON_H),
                new Vector2(FIELD_W * 0.5f - TRACK_MARGIN - BUTTON_W * 0.5f, BUTTON_Y),
                "ПРАВОЙ", new Color(0.95f, 0.85f, 0.35f, 1f));
            _btnRight.onClick.AddListener(() => OnFootPressed(2));

            _btnJunk = CreateButton("BtnJump_Junk", _field, new Vector2(BUTTON_W * 0.7f, BUTTON_H * 0.75f),
                new Vector2(0f, BUTTON_Y),
                "ПРЫЖОК [X]", new Color(0.9f, 0.25f, 0.25f, 1f));
            _btnJunk.onClick.AddListener(OnJunkPressed);

            // Статус
            _statusText = CreateText("StatusText", _field, new Vector2(FIELD_W - 40f, 40f),
                new Vector2(0f, FIELD_H * 0.5f - 30f), 22, TextAlignmentOptions.Center);
            _statusText.color = new Color(0.85f, 0.9f, 1f, 1f);
        }

        // ------------ Ввод ------------

        private void OnFootPressed(int foot)
        {
            if (_isCompleted || IsInTransition || _isFallen) return;

            float now = Time.unscaledTime;
            int need = TargetSteps;

            // Первый шаг любого этапа: без проверки интервала.
            if (_lastFoot == 0)
            {
                _lastFoot = foot;
                _lastStepTime = now;
                _steps = 1;
                AdvanceRunner();
                AnimateLegs(foot);
                UpdateStatusText();
                SetProgress((float)_steps / need);
                if (_instructionText != null)
                {
                    _instructionText.text = $"Побежали! Теперь чередуйте: {(foot == 1 ? "ПРАВОЙ" : "ЛЕВОЙ")}. {_steps}/{need}.";
                }
                if (_steps >= need)
                {
                    CompleteMechanic();
                }
                return;
            }

            if (foot == _lastFoot)
            {
                TriggerFall("та же нога дважды подряд");
                return;
            }

            float interval = now - _lastStepTime;
            if (interval < MinInterval)
            {
                TriggerFall($"слишком быстро ({interval:F2} с)");
                return;
            }
            if (interval > MaxInterval)
            {
                TriggerFall($"слишком медленно ({interval:F2} с)");
                return;
            }

            _lastFoot = foot;
            _lastStepTime = now;
            _steps++;
            AdvanceRunner();
            AnimateLegs(foot);
            UpdateStatusText();
            SetProgress(Mathf.Clamp01((float)_steps / need));

            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#00FF99>Шаг {_steps}/{need} ({interval:F2} с). Теперь {(foot == 1 ? "ПРАВОЙ" : "ЛЕВОЙ")}.</color>";
            }

            if (_steps >= need)
            {
                CompleteMechanic();
                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Этап {CurrentStage}/3: финиш взят!</color>";
                }
            }
        }

        private void OnJunkPressed()
        {
            if (_isCompleted || IsInTransition || _isFallen) return;
            TriggerFall("нажата кнопка ПРЫЖОК [X]");
        }

        // ------------ Движение и анимация ------------

        private void AdvanceRunner()
        {
            int need = TargetSteps;
            float total = _runnerFinishX - _runnerStartX;
            float step = total / need;
            _runnerX = Mathf.Min(_runnerFinishX, _runnerStartX + step * _steps);
            ApplyRunnerTransform();
        }

        private void ApplyRunnerTransform()
        {
            if (_runner == null) return;
            _runner.anchoredPosition = new Vector2(_runnerX, 0f);
            _runner.localRotation = Quaternion.identity;
            if (_runnerLegLeft != null) _runnerLegLeft.localRotation = Quaternion.identity;
            if (_runnerLegRight != null) _runnerLegRight.localRotation = Quaternion.identity;
        }

        private void AnimateLegs(int foot)
        {
            // Мгновенный «шаг»: одна нога вперёд, другая назад; в Update() возвращается к нулю.
            _legPhase = foot == 1 ? 1f : -1f;
        }

        private void TriggerFall(string reason)
        {
            _isFallen = true;
            _fallTimer = 0f;
            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#FF5555>Бегун упал: {reason}.</color>";
            }
        }

        private void Update()
        {
            if (_isCompleted) return;

            // Анимация ног (плавно возвращаем в нейтраль)
            if (!_isFallen && _runnerLegLeft != null && _runnerLegRight != null)
            {
                float angle = _legPhase * 25f;
                _runnerLegLeft.localRotation = Quaternion.Euler(0f, 0f, angle);
                _runnerLegRight.localRotation = Quaternion.Euler(0f, 0f, -angle);
                _legPhase = Mathf.MoveTowards(_legPhase, 0f, Time.unscaledDeltaTime * 4f);
            }

            if (_isFallen && !IsInTransition)
            {
                _fallTimer += Time.unscaledDeltaTime;
                if (_runner != null)
                {
                    float t = Mathf.Clamp01(_fallTimer / FALL_SECONDS);
                    _runner.localRotation = Quaternion.Euler(0f, 0f, -85f * t);
                }
                if (_fallTimer >= FALL_SECONDS)
                {
                    FailStage($"Бегун упал: {(_instructionText != null ? _instructionText.text : "сбой темпа")}");
                    _isFallen = false; // база всё равно пересоберёт поле
                }
            }
        }

        private void UpdateStatusText()
        {
            if (_statusText == null) return;
            _statusText.text = $"Шаги: {_steps}/{TargetSteps}";
        }

        // ------------ Хелперы построения ------------

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one;
            return rt;
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

        private static Button CreateButton(string name, Transform parent, Vector2 size, Vector2 pos, string label, Color color)
        {
            var rt = CreateRect(name, parent, size, pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            var labelRt = CreateRect(name + "_Label", rt, size, Vector2.zero);
            var t = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = 36;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.black;
            t.text = label;
            t.raycastTarget = false;
            return btn;
        }
    }
}
