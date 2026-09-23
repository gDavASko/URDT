using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M29_ContourCutting
{
    /// <summary>
    /// Механика #29: Вырезание по контуру (Contour Scissor Cutting).
    /// Задача: провести инструмент (ножницы/пилу) вдоль пунктирного контура звезды, не сходя с линии допуска.
    /// Предотвращены зависания: используется непрерывная проекция на отрезок траектории.
    /// </summary>
    public class M29_ContourCuttingMechanic : BaseMechanic2DModule, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Визуальные элементы")]
        [SerializeField] private RectTransform _scissorTool = null;
        [SerializeField] private RectTransform _contourContainer = null;
        [SerializeField] private RectTransform _cutShapeVisual = null;
        [SerializeField] private GameObject _startBadge = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _progressText = null;
        [SerializeField] private Image _progressFill = null;
        [SerializeField] private TMP_Text _instructionText = null;

        [Header("Параметры резки")]
        [SerializeField] private float _toleranceRadius = 65f;
        [SerializeField] private float _stepAdvanceThreshold = 24f;

        // Параметры по этапам: уменьшаем допуск и вводим лимит времени вне линии
        private static readonly float[] STAGE_TOLERANCE     = { 65f, 48f, 34f };
        private static readonly float[] STAGE_OFFTRACK_MAX  = { 0f, 1.2f, 0.75f };
        private static readonly float[] STAGE_TIME_LIMIT    = { 0f, 0f, 22f };

        public override int StageCount => 3;

        private float _offTrackTime = 0f;
        private float _timeLeft = 0f;

        /// <summary>Индекс текущего сегмента (0..15).</summary>
        public int CurrentSegment => _currentSegmentIndex;
        /// <summary>Общее количество сегментов контура.</summary>
        public int TotalSegments => _totalWaypoints;
        /// <summary>Оставшееся время (сек) или 0, если лимит не действует.</summary>
        public float TimeLeft => _timeLeft;
        /// <summary>Резец сейчас вне полосы допуска.</summary>
        public bool IsOffTrack => _isOffTrack;

        private readonly List<Vector2> _contourPoints = new List<Vector2>();
        private readonly List<Image> _cutSegmentVisuals = new List<Image>();
        private readonly List<Image> _vertexDotVisuals = new List<Image>();

        private int _currentSegmentIndex = 0;
        private int _totalWaypoints = 16;
        private bool _isStarted = false;
        private bool _isDragging = false;
        private bool _isOffTrack = false;
        private float _pulseTimer = 0f;
        private Canvas _canvas = null;
        private Image _scissorImage = null;

        private readonly Color _normalToolColor = new Color(1f, 0.85f, 0.2f, 1f);
        private readonly Color _warningToolColor = new Color(1f, 0.35f, 0.35f, 1f);
        private readonly Color _cutDoneColor = new Color(0.1f, 1f, 0.55f, 1f);
        private readonly Color _uncutColor = new Color(0.35f, 0.65f, 0.95f, 0.45f);

        protected override void Awake()
        {
            base.Awake();
            _canvas = GetComponentInParent<Canvas>();
            if (_scissorTool != null)
            {
                _scissorImage = _scissorTool.GetComponent<Image>();
            }
            GenerateContourPoints();
            EnsureVisualLists();
        }

        private void EnsureVisualLists()
        {
            if (_contourContainer == null) return;

            if (_cutSegmentVisuals.Count == 0 || _vertexDotVisuals.Count == 0)
            {
                _cutSegmentVisuals.Clear();
                _vertexDotVisuals.Clear();
                for (int i = 0; i < _totalWaypoints; i++)
                {
                    Transform seg = _contourContainer.Find($"Segment_{i}");
                    if (seg != null && seg.TryGetComponent<Image>(out var sImg))
                    {
                        _cutSegmentVisuals.Add(sImg);
                    }
                    Transform pt = _contourContainer.Find($"Point_{i}");
                    if (pt != null && pt.TryGetComponent<Image>(out var pImg))
                    {
                        _vertexDotVisuals.Add(pImg);
                    }
                }
            }
        }

        private void GenerateContourPoints()
        {
            _contourPoints.Clear();
            // Формируем контур правильной 8-конечной звезды (16 вершин)
            int pointsCount = 16;
            float outerR = 120f;
            float innerR = 70f;

            for (int i = 0; i < pointsCount; i++)
            {
                float angle = (i / (float)pointsCount) * Mathf.PI * 2f;
                float r = (i % 2 == 0) ? outerR : innerR;
                _contourPoints.Add(new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r));
            }

            _totalWaypoints = _contourPoints.Count;
        }

        public override void Initialize()
        {
            base.Initialize();
            _currentSegmentIndex = 0;
            _isStarted = false;
            _isDragging = false;
            _isOffTrack = false;
            _pulseTimer = 0f;
            _offTrackTime = 0f;

            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_TOLERANCE.Length - 1);
            _toleranceRadius = STAGE_TOLERANCE[idx];
            _timeLeft = STAGE_TIME_LIMIT[idx];

            if (_contourPoints.Count == 0)
            {
                GenerateContourPoints();
            }

            EnsureVisualLists();

            if (_scissorTool != null && _contourPoints.Count > 0)
            {
                _scissorTool.anchoredPosition = _contourPoints[0];
                Vector2 nextPt = _contourPoints[1];
                Vector2 dir = nextPt - _contourPoints[0];
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                _scissorTool.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }

            if (_scissorImage != null)
            {
                _scissorImage.color = _normalToolColor;
            }

            if (_cutShapeVisual != null)
            {
                _cutShapeVisual.localScale = Vector3.one;
                _cutShapeVisual.localRotation = Quaternion.identity;
            }

            if (_startBadge != null)
            {
                _startBadge.SetActive(true);
            }

            ResetSegmentVisuals();
            UpdateUI();
            SetProgress(0f);

            if (_instructionText != null)
            {
                _instructionText.text = GetStageInstruction(CurrentStage);
            }
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, STAGE_TOLERANCE.Length - 1);
            float tol = STAGE_TOLERANCE[idx];
            float off = STAGE_OFFTRACK_MAX[idx];
            float lim = STAGE_TIME_LIMIT[idx];
            string tolTxt = idx == 0 ? "широкая" : idx == 1 ? "средняя" : "узкая";
            string offTxt = off > 0f ? $" Дольше {off:F1} сек вне полосы — провал." : "";
            string limTxt = lim > 0f ? $" Лимит: {lim:F0} сек." : "";
            return $"Этап {stage}/3. Полоса допуска {tolTxt}. Зажмите инструмент в точке СТАРТ и ведите вдоль пунктира.{offTxt}{limTxt}";
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        public void RegisterSegmentVisual(Image img)
        {
            if (img != null && !_cutSegmentVisuals.Contains(img))
            {
                _cutSegmentVisuals.Add(img);
            }
        }

        public void RegisterVertexVisual(Image img)
        {
            if (img != null && !_vertexDotVisuals.Contains(img))
            {
                _vertexDotVisuals.Add(img);
            }
        }

        private void ResetSegmentVisuals()
        {
            EnsureVisualLists();

            for (int i = 0; i < _cutSegmentVisuals.Count; i++)
            {
                var img = _cutSegmentVisuals[i];
                if (img != null)
                {
                    img.color = _uncutColor;
                    img.rectTransform.sizeDelta = new Vector2(img.rectTransform.sizeDelta.x, 4f);
                }
            }

            for (int i = 0; i < _vertexDotVisuals.Count; i++)
            {
                var dot = _vertexDotVisuals[i];
                if (dot != null)
                {
                    dot.color = (i == 0) ? new Color(0.1f, 1f, 0.55f, 0.95f) : new Color(0.5f, 0.8f, 1f, 0.7f);
                    dot.transform.localScale = (i == 0) ? Vector3.one * 1.3f : Vector3.one;
                }
            }
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;

            // Лимит по времени этапа
            if (_timeLeft > 0f)
            {
                _timeLeft -= Time.deltaTime;
                if (_timeLeft <= 0f)
                {
                    _timeLeft = 0f;
                    FailStage("Время на вырезание истекло");
                    return;
                }
            }

            // Накопление времени вне полосы допуска (если для этапа задан лимит)
            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_OFFTRACK_MAX.Length - 1);
            float offMax = STAGE_OFFTRACK_MAX[idx];
            if (offMax > 0f && _isStarted && _isOffTrack)
            {
                _offTrackTime += Time.deltaTime;
                if (_offTrackTime >= offMax)
                {
                    FailStage("Долгий сход с линии — резак срезал контур");
                    return;
                }
            }
            else if (!_isOffTrack)
            {
                _offTrackTime = Mathf.Max(0f, _offTrackTime - Time.deltaTime * 0.5f);
            }

            _pulseTimer += Time.deltaTime * 5f;

            // Подсветка и пульсация следующей целевой вершины
            int nextTargetVertex = (_currentSegmentIndex + 1) % _totalWaypoints;
            if (_vertexDotVisuals.Count > nextTargetVertex && _vertexDotVisuals[nextTargetVertex] != null)
            {
                float targetPulse = 1.15f + Mathf.Sin(_pulseTimer * 1.5f) * 0.25f;
                _vertexDotVisuals[nextTargetVertex].transform.localScale = Vector3.one * targetPulse;
                _vertexDotVisuals[nextTargetVertex].color = new Color(1f, 0.88f, 0.2f, 1f);
            }

            // Пульсация текущего целевого сегмента для подсказки игроку
            if (_cutSegmentVisuals.Count > 0 && _currentSegmentIndex < _cutSegmentVisuals.Count)
            {
                Image activeImg = _cutSegmentVisuals[_currentSegmentIndex];
                if (activeImg != null)
                {
                    float pulse = 0.55f + Mathf.Sin(_pulseTimer) * 0.35f;
                    if (_isOffTrack)
                    {
                        activeImg.color = new Color(1f, 0.3f, 0.3f, pulse);
                        activeImg.rectTransform.sizeDelta = new Vector2(activeImg.rectTransform.sizeDelta.x, 5f);
                    }
                    else
                    {
                        activeImg.color = new Color(0.2f, 0.95f, 1f, pulse);
                        activeImg.rectTransform.sizeDelta = new Vector2(activeImg.rectTransform.sizeDelta.x, 6f);
                    }
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isCompleted || IsInTransition) return;
            _isDragging = true;
            ProcessScissorMove(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _isCompleted || IsInTransition) return;
            ProcessScissorMove(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isDragging = false;
        }

        private void ProcessScissorMove(Vector2 screenPos)
        {
            if (_contourContainer == null || _contourPoints.Count == 0) return;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_contourContainer, screenPos, cam, out Vector2 localPoint))
            {
                return;
            }

            // Инструмент ВСЕГДА следует за курсором, чтобы не возникало блокировки и зависания
            if (_scissorTool != null)
            {
                _scissorTool.anchoredPosition = localPoint;
            }

            // Если резка еще не начата, проверяем активацию возле точки СТАРТ (точка 0)
            if (!_isStarted)
            {
                float distToStart = Vector2.Distance(localPoint, _contourPoints[0]);
                if (distToStart <= _toleranceRadius)
                {
                    _isStarted = true;
                    _currentSegmentIndex = 0;
                    if (_startBadge != null) _startBadge.SetActive(false);
                    if (_instructionText != null)
                    {
                        _instructionText.text = "Отлично! Продолжайте вести инструмент вдоль пунктира.";
                    }
                }
                else
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FFDD55>Начните с точки СТАРТ (зелёная метка справа)!</color>";
                    }
                    return;
                }
            }

            // Расчет проекции на текущий отрезок контура (между точкой A и точкой B)
            Vector2 ptA = _contourPoints[_currentSegmentIndex];
            Vector2 ptB = _contourPoints[(_currentSegmentIndex + 1) % _totalWaypoints];

            Vector2 segVec = ptB - ptA;
            float segLenSq = segVec.sqrMagnitude;
            float t = segLenSq > 0.001f ? Mathf.Clamp01(Vector2.Dot(localPoint - ptA, segVec) / segLenSq) : 0f;
            Vector2 projPoint = ptA + segVec * t;

            float distToSegment = Vector2.Distance(localPoint, projPoint);

            // Ориентация инструмента по направлению текущего отрезка
            if (_scissorTool != null && segLenSq > 0.1f)
            {
                float angle = Mathf.Atan2(segVec.y, segVec.x) * Mathf.Rad2Deg;
                _scissorTool.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }

            // Проверка отклонения от траектории
            if (distToSegment > _toleranceRadius)
            {
                _isOffTrack = true;
                if (_scissorImage != null) _scissorImage.color = _warningToolColor;
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#FF5555>Сход с линии! Верните инструмент ближе к пунктиру.</color>";
                }
                return;
            }

            // В пределах допуска: резка активна
            _isOffTrack = false;
            if (_scissorImage != null) _scissorImage.color = _normalToolColor;

            // Продвижение по текущему отрезку
            float distToEndOfSegment = Vector2.Distance(localPoint, ptB);
            if (t >= 0.75f || distToEndOfSegment <= _stepAdvanceThreshold * 1.4f)
            {
                // Завершение реза текущего сегмента: ярко окрашиваем в неоновый зеленый и утолщаем
                if (_currentSegmentIndex < _cutSegmentVisuals.Count && _cutSegmentVisuals[_currentSegmentIndex] != null)
                {
                    _cutSegmentVisuals[_currentSegmentIndex].color = _cutDoneColor;
                    _cutSegmentVisuals[_currentSegmentIndex].rectTransform.sizeDelta = new Vector2(_cutSegmentVisuals[_currentSegmentIndex].rectTransform.sizeDelta.x, 8f);
                }

                // Зажигаем пройденную и достигнутую вершины
                if (_currentSegmentIndex < _vertexDotVisuals.Count && _vertexDotVisuals[_currentSegmentIndex] != null)
                {
                    _vertexDotVisuals[_currentSegmentIndex].color = _cutDoneColor;
                    _vertexDotVisuals[_currentSegmentIndex].transform.localScale = Vector3.one * 1.4f;
                }

                int nextV = (_currentSegmentIndex + 1) % _totalWaypoints;
                if (nextV < _vertexDotVisuals.Count && _vertexDotVisuals[nextV] != null)
                {
                    _vertexDotVisuals[nextV].color = _cutDoneColor;
                    _vertexDotVisuals[nextV].transform.localScale = Vector3.one * 1.4f;
                }

                _currentSegmentIndex++;
                float progress = Mathf.Clamp01((float)_currentSegmentIndex / _totalWaypoints);
                SetProgress(progress);
                UpdateUI();

                if (_currentSegmentIndex >= _totalWaypoints)
                {
                    CompleteCut();
                }
            }
        }

        private void CompleteCut()
        {
            CompleteMechanic();

            if (_scissorImage != null) _scissorImage.color = _normalToolColor;

            if (_instructionText != null)
            {
                _instructionText.text = "<color=#00FF99>Идеально! Звезда аккуратно вырезана точно по контуру!</color>";
            }

            // Зажигаем все вершины и сегменты
            for (int i = 0; i < _cutSegmentVisuals.Count; i++)
            {
                if (_cutSegmentVisuals[i] != null)
                {
                    _cutSegmentVisuals[i].color = _cutDoneColor;
                    _cutSegmentVisuals[i].rectTransform.sizeDelta = new Vector2(_cutSegmentVisuals[i].rectTransform.sizeDelta.x, 8f);
                }
            }
            for (int i = 0; i < _vertexDotVisuals.Count; i++)
            {
                if (_vertexDotVisuals[i] != null)
                {
                    _vertexDotVisuals[i].color = _cutDoneColor;
                    _vertexDotVisuals[i].transform.localScale = Vector3.one * 1.4f;
                }
            }

            // Анимация отделения вырезанной звезды
            if (_cutShapeVisual != null)
            {
                _cutShapeVisual.localScale = Vector3.one * 1.15f;
                _cutShapeVisual.localRotation = Quaternion.Euler(0f, 0f, 5f);
            }
        }

        private void UpdateUI()
        {
            float pct = Mathf.Clamp01((float)_currentSegmentIndex / _totalWaypoints) * 100f;
            if (_progressText != null)
            {
                _progressText.text = $"Вырезано: {pct:F0}%";
            }

            if (_progressFill != null)
            {
                _progressFill.fillAmount = Mathf.Clamp01((float)_currentSegmentIndex / _totalWaypoints);
            }
        }
    }
}
