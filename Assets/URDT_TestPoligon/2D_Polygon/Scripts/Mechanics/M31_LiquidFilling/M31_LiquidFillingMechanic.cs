using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M31_LiquidFilling
{
    /// <summary>
    /// Механика #31: Заполнение лунок раствором (3-Well Solution Dispensing).
    /// Задача: перемещать раствороподатчик над 3 лунками по очереди (1 -> 2 -> 3),
    /// удерживая подачу раствора до полного заполнения каждой лунки.
    /// Мешающий фактор: недопустимость нарушения последовательности (заливка не в ту лунку)
    /// и необходимость точного удержания дозатора в границах горловины.
    /// </summary>
    public class M31_LiquidFillingMechanic : BaseMechanic2DModule, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Раствороподатчик")]
        [SerializeField] private RectTransform _dispenserTool = null;
        [SerializeField] private RectTransform _nozzleTip = null;
        [SerializeField] private Image _streamVisual = null;
        [SerializeField] private RectTransform _wellsContainer = null;

        [Header("3 Лунки")]
        [SerializeField] private RectTransform[] _wellRoots = null;
        [SerializeField] private Image[] _liquidFills = null;
        [SerializeField] private Image[] _glowRings = null;
        [SerializeField] private TMP_Text[] _percentTexts = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _progressText = null;
        [SerializeField] private Image _progressFill = null;
        [SerializeField] private TMP_Text _instructionText = null;

        [Header("Параметры заполнения")]
        [SerializeField] private float _fillSpeed = 0.48f; // ~2.1 сек на лунку
        [SerializeField] private float _wellRadius = 55f;

        // Параметры по этапам: медленнее налив, уже горловина, ошибочная лунка на 2-3 этапе — провал
        private static readonly float[] STAGE_FILL_SPEED  = { 0.48f, 0.34f, 0.26f };
        private static readonly float[] STAGE_WELL_RADIUS = { 55f, 42f, 32f };
        private static readonly float[] STAGE_WRONG_MAX   = { 999f, 1.2f, 0.6f }; // сек над «чужой» лункой при подаче
        private static readonly float[] STAGE_TIME_LIMIT  = { 0f, 0f, 20f };

        public override int StageCount => 3;

        private float _wrongWellHoldTime = 0f;
        private float _timeLeft = 0f;

        /// <summary>Индекс активной лунки (0-based).</summary>
        public int CurrentWellIndex => _currentWellIndex;
        /// <summary>Заполненность каждой из 3 лунок (0..1).</summary>
        public float GetWellFill(int i) => (i >= 0 && i < 3) ? _wellFillAmounts[i] : 0f;
        /// <summary>Оставшееся время (сек) или 0, если лимит не действует.</summary>
        public float TimeLeft => _timeLeft;

        private int _currentWellIndex = 0;
        private readonly float[] _wellFillAmounts = new float[3];
        private bool _isPouring = false;
        private bool _isDragging = false;
        private float _pulseTimer = 0f;
        private Canvas _canvas = null;

        private readonly Color _liquidNormalColor = new Color(0f, 0.8f, 1f, 0.9f);
        private readonly Color _liquidCompleteColor = new Color(0f, 1f, 0.65f, 0.95f);
        private readonly Color _activeGlowColor = new Color(1f, 0.88f, 0.2f, 0.9f);
        private readonly Color _doneGlowColor = new Color(0f, 1f, 0.55f, 0.95f);
        private readonly Color _inactiveGlowColor = new Color(0.3f, 0.5f, 0.7f, 0.25f);

        protected override void Awake()
        {
            base.Awake();
            _canvas = GetComponentInParent<Canvas>();
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (_wellsContainer == null)
            {
                Transform wTr = transform.Find("WellsContainer");
                if (wTr != null) _wellsContainer = wTr as RectTransform;
            }

            if (_wellsContainer != null && (_wellRoots == null || _wellRoots.Length < 3 || _wellRoots[0] == null))
            {
                _wellRoots = new RectTransform[3];
                _liquidFills = new Image[3];
                _glowRings = new Image[3];
                _percentTexts = new TMP_Text[3];

                for (int i = 0; i < 3; i++)
                {
                    Transform w = _wellsContainer.Find($"Well_{i + 1}");
                    if (w != null)
                    {
                        _wellRoots[i] = w as RectTransform;
                        Transform lq = w.Find("LiquidFill");
                        if (lq != null) _liquidFills[i] = lq.GetComponent<Image>();
                        Transform ring = w.Find("GlowRing");
                        if (ring != null) _glowRings[i] = ring.GetComponent<Image>();
                        Transform pct = w.Find("PercentText");
                        if (pct != null) _percentTexts[i] = pct.GetComponent<TMP_Text>();
                    }
                }
            }

            if (_dispenserTool == null)
            {
                Transform dt = transform.Find("DispenserTool");
                if (dt != null) _dispenserTool = dt as RectTransform;
            }

            if (_dispenserTool != null)
            {
                if (_nozzleTip == null)
                {
                    Transform nt = _dispenserTool.Find("NozzleTip");
                    if (nt != null) _nozzleTip = nt as RectTransform;
                }
                if (_streamVisual == null)
                {
                    Transform st = _dispenserTool.Find("StreamVisual");
                    if (st != null) _streamVisual = st.GetComponent<Image>();
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            EnsureReferences();

            _currentWellIndex = 0;
            _isPouring = false;
            _isDragging = false;
            _pulseTimer = 0f;
            _wrongWellHoldTime = 0f;

            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_FILL_SPEED.Length - 1);
            _fillSpeed = STAGE_FILL_SPEED[idx];
            _wellRadius = STAGE_WELL_RADIUS[idx];
            _timeLeft = STAGE_TIME_LIMIT[idx];

            for (int i = 0; i < 3; i++)
            {
                _wellFillAmounts[i] = 0f;
                if (_liquidFills != null && i < _liquidFills.Length && _liquidFills[i] != null)
                {
                    _liquidFills[i].fillAmount = 0f;
                    _liquidFills[i].color = _liquidNormalColor;
                }
                if (_glowRings != null && i < _glowRings.Length && _glowRings[i] != null)
                {
                    _glowRings[i].color = (i == 0) ? _activeGlowColor : _inactiveGlowColor;
                }
                if (_percentTexts != null && i < _percentTexts.Length && _percentTexts[i] != null)
                {
                    _percentTexts[i].text = "0%";
                    _percentTexts[i].color = Color.white;
                }
            }

            if (_streamVisual != null)
            {
                _streamVisual.gameObject.SetActive(false);
            }

            // Устанавливаем раствороподатчик над первой лункой
            if (_dispenserTool != null)
            {
                if (_wellRoots != null && _wellRoots.Length > 0 && _wellRoots[0] != null)
                {
                    _dispenserTool.anchoredPosition = new Vector2(_wellRoots[0].anchoredPosition.x, 70f);
                }
                else
                {
                    _dispenserTool.anchoredPosition = new Vector2(-135f, 70f);
                }
            }

            if (_instructionText != null)
            {
                _instructionText.text = GetStageInstruction(CurrentStage);
            }

            UpdateUI();
            SetProgress(0f);
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, STAGE_FILL_SPEED.Length - 1);
            float wrong = STAGE_WRONG_MAX[idx];
            float lim = STAGE_TIME_LIMIT[idx];
            string wrongTxt = wrong < 5f ? $" Больше {wrong:F1} сек над чужой лункой при подаче — провал." : "";
            string limTxt = lim > 0f ? $" Лимит: {lim:F0} сек." : "";
            return $"Этап {stage}/3. Перемещайте раствороподатчик над лунками по очереди (1 → 2 → 3), удерживайте над активной.{wrongTxt}{limTxt}";
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;

            if (_timeLeft > 0f)
            {
                _timeLeft -= Time.deltaTime;
                if (_timeLeft <= 0f)
                {
                    _timeLeft = 0f;
                    FailStage("Время на заполнение истекло");
                    return;
                }
            }

            _pulseTimer += Time.deltaTime * 5f;

            // Пульсация контура текущей целевой лунки
            if (_glowRings != null && _currentWellIndex < _glowRings.Length && _glowRings[_currentWellIndex] != null)
            {
                float pulse = 0.65f + Mathf.Sin(_pulseTimer) * 0.35f;
                _glowRings[_currentWellIndex].color = new Color(_activeGlowColor.r, _activeGlowColor.g, _activeGlowColor.b, pulse);
            }

            // Налив раствора в текущую лунку
            if (_isPouring && _currentWellIndex < 3)
            {
                _wellFillAmounts[_currentWellIndex] += _fillSpeed * Time.deltaTime;
                _wellFillAmounts[_currentWellIndex] = Mathf.Clamp01(_wellFillAmounts[_currentWellIndex]);

                if (_liquidFills != null && _liquidFills[_currentWellIndex] != null)
                {
                    _liquidFills[_currentWellIndex].fillAmount = _wellFillAmounts[_currentWellIndex];
                }

                if (_percentTexts != null && _percentTexts[_currentWellIndex] != null)
                {
                    _percentTexts[_currentWellIndex].text = $"{(_wellFillAmounts[_currentWellIndex] * 100f):F0}%";
                }

                // Визуальная анимация струи (мерцание толщины и цвета)
                if (_streamVisual != null)
                {
                    float streamW = 6f + Mathf.Sin(_pulseTimer * 3f) * 1.5f;
                    _streamVisual.rectTransform.sizeDelta = new Vector2(streamW, _streamVisual.rectTransform.sizeDelta.y);
                }

                float totalProgress = (_currentWellIndex + _wellFillAmounts[_currentWellIndex]) / 3f;
                SetProgress(totalProgress);
                UpdateUI();

                // Лунка полностью заполнена
                if (_wellFillAmounts[_currentWellIndex] >= 1f)
                {
                    OnWellFilled(_currentWellIndex);
                }
            }
        }

        private void OnWellFilled(int wellIdx)
        {
            if (_liquidFills != null && _liquidFills[wellIdx] != null)
            {
                _liquidFills[wellIdx].color = _liquidCompleteColor;
            }

            if (_glowRings != null && _glowRings[wellIdx] != null)
            {
                _glowRings[wellIdx].color = _doneGlowColor;
            }

            if (_percentTexts != null && _percentTexts[wellIdx] != null)
            {
                _percentTexts[wellIdx].text = "100% ✓";
                _percentTexts[wellIdx].color = _doneGlowColor;
            }

            _currentWellIndex++;
            _isPouring = false;
            if (_streamVisual != null) _streamVisual.gameObject.SetActive(false);

            if (_currentWellIndex >= 3)
            {
                CompleteAllWells();
            }
            else
            {
                if (_glowRings != null && _glowRings[_currentWellIndex] != null)
                {
                    _glowRings[_currentWellIndex].color = _activeGlowColor;
                }

                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Лунка {wellIdx + 1} заполнена!</color> Переместите раствороподатчик на Лунку {_currentWellIndex + 1}.";
                }
            }
        }

        private void CompleteAllWells()
        {
            CompleteMechanic();
            SetProgress(1f);
            UpdateUI();

            if (_instructionText != null)
            {
                _instructionText.text = "<color=#00FF99>Отлично! Все 3 лунки успешно заполнены раствором!</color>";
            }

            if (_dispenserTool != null)
            {
                _dispenserTool.localScale = Vector3.one * 1.05f;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isCompleted || IsInTransition) return;
            _isDragging = true;
            ProcessDispenserPosition(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _isCompleted || IsInTransition) return;
            ProcessDispenserPosition(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isDragging = false;
            _isPouring = false;
            _wrongWellHoldTime = 0f;
            if (_streamVisual != null) _streamVisual.gameObject.SetActive(false);
        }

        private void ProcessDispenserPosition(Vector2 screenPos)
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            RectTransform parentRt = transform as RectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screenPos, cam, out Vector2 localPoint))
            {
                return;
            }

            // Ограничиваем перемещение раствороподатчика по горизонтали и вертикали
            float clampedX = Mathf.Clamp(localPoint.x, -260f, 260f);
            float clampedY = Mathf.Clamp(localPoint.y, -10f, 130f);
            Vector2 toolPos = new Vector2(clampedX, clampedY);

            if (_dispenserTool != null)
            {
                _dispenserTool.anchoredPosition = toolPos;
            }

            CheckPouringTarget(toolPos);
        }

        private void CheckPouringTarget(Vector2 toolPos)
        {
            if (_wellRoots == null || _currentWellIndex < 0 || _currentWellIndex >= _wellRoots.Length) return;

            // Проверяем расстояние по горизонтали от носика до активной лунки
            RectTransform activeWell = _wellRoots[_currentWellIndex];
            if (activeWell == null) return;

            float distToActive = Mathf.Abs(toolPos.x - activeWell.anchoredPosition.x);

            if (distToActive <= _wellRadius)
            {
                // Находимся точно над активной лункой -> подача раствора активна
                _isPouring = true;
                _wrongWellHoldTime = 0f;
                if (_streamVisual != null) _streamVisual.gameObject.SetActive(true);

                if (_instructionText != null)
                {
                    _instructionText.text = $"Заполнение Лунки {_currentWellIndex + 1}... Удерживайте раствороподатчик над лункой!";
                }
            }
            else
            {
                // Проверяем, не наведен ли податчик на другие (неактивные) лунки
                bool overWrongWell = false;
                for (int i = 0; i < _wellRoots.Length; i++)
                {
                    if (i != _currentWellIndex && _wellRoots[i] != null)
                    {
                        if (Mathf.Abs(toolPos.x - _wellRoots[i].anchoredPosition.x) <= _wellRadius)
                        {
                            overWrongWell = true;
                            if (i < _currentWellIndex)
                            {
                                if (_instructionText != null)
                                    _instructionText.text = $"<color=#88CCFF>Лунка {i + 1} уже полностью заполнена. Перейдите к Лунке {_currentWellIndex + 1}!</color>";
                            }
                            else
                            {
                                if (_instructionText != null)
                                    _instructionText.text = $"<color=#FF5555>Заполняйте лунки строго по очереди! Сначала заполните Лунку {_currentWellIndex + 1}.</color>";
                            }
                            break;
                        }
                    }
                }

                _isPouring = false;
                if (_streamVisual != null) _streamVisual.gameObject.SetActive(false);

                // На жёстких этапах — накопление времени над чужой лункой при активной подаче
                int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_WRONG_MAX.Length - 1);
                float wrongLimit = STAGE_WRONG_MAX[idx];
                if (overWrongWell && _isDragging && wrongLimit < 5f)
                {
                    _wrongWellHoldTime += Time.deltaTime;
                    if (_wrongWellHoldTime >= wrongLimit)
                    {
                        FailStage("Раствор подан не в ту лунку");
                        return;
                    }
                }
                else if (!overWrongWell)
                {
                    _wrongWellHoldTime = Mathf.Max(0f, _wrongWellHoldTime - Time.deltaTime * 0.5f);
                }

                if (!overWrongWell && _instructionText != null)
                {
                    _instructionText.text = $"Переместите раствороподатчик к Лунке {_currentWellIndex + 1}.";
                }
            }
        }

        private void UpdateUI()
        {
            if (_progressText != null)
            {
                _progressText.text = $"Заполнено лунок: {_currentWellIndex} / 3";
            }

            if (_progressFill != null)
            {
                float totalProgress = (_currentWellIndex + (_currentWellIndex < 3 ? _wellFillAmounts[_currentWellIndex] : 0f)) / 3f;
                _progressFill.fillAmount = Mathf.Clamp01(totalProgress);
            }
        }
    }
}
