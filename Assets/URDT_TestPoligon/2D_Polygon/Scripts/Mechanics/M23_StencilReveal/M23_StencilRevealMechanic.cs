using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;
using Input = UnityEngine.Input;

namespace KBP.URDT.TestPoligon.Mechanics2D.M23_StencilReveal
{
    /// <summary>
    /// Механика #23: Пространственная фильтрация триггеров лучом/окном (Stencil Reveal).
    /// Задача: перемещать лупу по рабочей зоне, находить скрытые кристаллы и удерживать лупу 2 секунды над объектом для сбора.
    /// Мешающие факторы:
    /// 1. Ложный комок пыли [X] — при удержании выдает предупреждение о браке.
    /// 2. Ограниченный радиус линзы (65px).
    /// </summary>
    public class M23_StencilRevealMechanic : BaseMechanic2DModule, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Линза и рабочая область")]
        [SerializeField] private RectTransform _lensTransform = null;
        [SerializeField] private RectTransform _darkSearchArea = null;
        [SerializeField] private Image _lensProgressRing = null;

        [Header("Цели")]
        [SerializeField] private HiddenRevealTarget[] _targets = null;
        [SerializeField] private HiddenRevealTarget _junkDust = null;
        [SerializeField] private float _revealRadius = 65f;
        [SerializeField] private float _requiredHoldDuration = 2.0f;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _scoreText = null;
        [SerializeField] private TMP_Text _instructionText = null;

        private Canvas _canvas;
        private int _collectedCount = 0;
        private int _totalCrystals = 3;

        private bool _isDragging = false;
        private HiddenRevealTarget _focusedTarget = null;
        private float _holdTimer = 0f;
        private float _stageTimeLeft = 999f;

        private static readonly float[] StageHold = new float[] { 2.0f, 1.5f, 1.2f };
        private static readonly float[] StageTimeLimit = new float[] { 999f, 60f, 45f };

        public override int StageCount => 3;
        public int Collected => _collectedCount;
        public int TotalCrystals => _totalCrystals;
        public float StageTimeLeft => _stageTimeLeft;
        public float StageTimeLimit_S => StageTimeLimit[Mathf.Clamp(CurrentStage - 1, 0, StageTimeLimit.Length - 1)];

        protected override void Awake()
        {
            base.Awake();
            _canvas = GetComponentInParent<Canvas>();

            // Настраиваем захват лупы и клики по темной зоне
            SetupDragProxies();

            if (_targets != null)
            {
                foreach (var t in _targets)
                {
                    if (t != null) t.OnTargetCollected += HandleTargetCollected;
                }
            }
            if (_junkDust != null)
            {
                _junkDust.OnTargetCollected += HandleDustCollected;
            }
        }

        private void OnDestroy()
        {
            if (_targets != null)
            {
                foreach (var t in _targets)
                {
                    if (t != null) t.OnTargetCollected -= HandleTargetCollected;
                }
            }
            if (_junkDust != null)
            {
                _junkDust.OnTargetCollected -= HandleDustCollected;
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _collectedCount = 0;
            _isDragging = false;
            _focusedTarget = null;
            _holdTimer = 0f;

            int idx = Mathf.Clamp(CurrentStage - 1, 0, StageHold.Length - 1);
            _requiredHoldDuration = StageHold[idx];
            _stageTimeLeft = StageTimeLimit[idx];

            if (_lensTransform != null)
            {
                _lensTransform.anchoredPosition = Vector2.zero;
            }

            if (_lensProgressRing != null)
            {
                _lensProgressRing.fillAmount = 0f;
            }

            SetupDragProxies();

            if (_targets != null)
            {
                foreach (var t in _targets) if (t != null) t.ResetTarget();
            }
            if (_junkDust != null) _junkDust.ResetTarget();

            UpdateUI();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, StageHold.Length - 1);
            switch (stage)
            {
                case 1: return $"Этап 1/3. Перемещайте лупу и удерживайте её {StageHold[idx]:F1}с над кристаллами. Пыль [X] — только предупреждение.";
                case 2: return $"Этап 2/3. Удержание {StageHold[idx]:F1}с. Изучение пыли [X] до конца — сброс. Лимит времени: {StageTimeLimit[idx]:F0}с.";
                case 3: return $"Этап 3/3. Удержание {StageHold[idx]:F1}с, лимит времени {StageTimeLimit[idx]:F0}с, пыль [X] — мгновенный сброс.";
                default: return _instruction;
            }
        }

        private void SetupDragProxies()
        {
            if (_darkSearchArea != null)
            {
                var img = _darkSearchArea.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;

                var proxy = _darkSearchArea.GetComponent<M23DragProxy>() ?? _darkSearchArea.gameObject.AddComponent<M23DragProxy>();
                proxy.Initialize(this);
            }

            if (_lensTransform != null)
            {
                var img = _lensTransform.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;

                var proxy = _lensTransform.GetComponent<M23DragProxy>() ?? _lensTransform.gameObject.AddComponent<M23DragProxy>();
                proxy.Initialize(this);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnProxyPointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            OnProxyDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnProxyPointerUp(eventData);
        }

        public void OnProxyPointerDown(PointerEventData eventData)
        {
            if (_isCompleted) return;
            _isDragging = true;
            UpdateLensPosition(eventData.position);
        }

        public void OnProxyDrag(PointerEventData eventData)
        {
            if (_isCompleted) return;
            UpdateLensPosition(eventData.position);
        }

        public void OnProxyPointerUp(PointerEventData eventData)
        {
            _isDragging = false;
        }

        private void UpdateLensPosition(Vector2 screenPos)
        {
            if (_isCompleted || _lensTransform == null) return;

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            RectTransform parentRt = _lensTransform.parent as RectTransform;
            RectTransform targetRef = parentRt != null ? parentRt : (_darkSearchArea != null ? _darkSearchArea : (transform as RectTransform));

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetRef,
                    screenPos,
                    cam,
                    out Vector2 localPoint))
            {
                ClampLensPosition(ref localPoint);
                _lensTransform.anchoredPosition = localPoint;
            }
        }

        private void ClampLensPosition(ref Vector2 pos)
        {
            if (_darkSearchArea != null)
            {
                float halfW = (_darkSearchArea.rect.width - 70f) * 0.5f;
                float halfH = (_darkSearchArea.rect.height - 70f) * 0.5f;
                pos.x = Mathf.Clamp(pos.x, -halfW, halfW);
                pos.y = Mathf.Clamp(pos.y, -halfH, halfH);
            }
        }

        private void Update()
        {
            if (_isCompleted || _lensTransform == null || IsInTransition) return;

            // Таймер этапа (для 2-3)
            if (_stageTimeLeft < 900f)
            {
                _stageTimeLeft -= Time.unscaledDeltaTime;
                if (_stageTimeLeft <= 0f)
                {
                    FailStage("истекло время этапа");
                    return;
                }
            }

            // 1. Драг мышью
            if (_isDragging && UnityEngine.Input.GetMouseButton(0))
            {
                UpdateLensPosition(UnityEngine.Input.mousePosition);
            }
            else if (_isDragging && !UnityEngine.Input.GetMouseButton(0))
            {
                _isDragging = false;
            }

            // 2. Управление клавиатурой (стрелки / WASD)
            float h = UnityEngine.Input.GetAxisRaw("Horizontal");
            float v = UnityEngine.Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                Vector2 pos = _lensTransform.anchoredPosition;
                pos += new Vector2(h, v) * (240f * Time.unscaledDeltaTime);
                ClampLensPosition(ref pos);
                _lensTransform.anchoredPosition = pos;
            }

            Vector2 lensPos = _lensTransform.anchoredPosition;
            HiddenRevealTarget targetUnderLens = null;
            float closestDist = float.MaxValue;

            // 3. Проверяем кристаллы
            if (_targets != null)
            {
                foreach (var target in _targets)
                {
                    if (target == null || target.IsCollected) continue;
                    float dist = Vector2.Distance(lensPos, target.RectTransform.anchoredPosition);
                    bool revealed = dist <= _revealRadius;
                    target.SetRevealed(revealed);

                    if (revealed && dist < closestDist)
                    {
                        closestDist = dist;
                        targetUnderLens = target;
                    }
                }
            }

            // 4. Проверяем пыль/мусор [X]
            if (_junkDust != null && !_junkDust.IsCollected)
            {
                float dustDist = Vector2.Distance(lensPos, _junkDust.RectTransform.anchoredPosition);
                bool dustRevealed = dustDist <= _revealRadius;
                _junkDust.SetRevealed(dustRevealed);

                if (dustRevealed && dustDist < closestDist)
                {
                    closestDist = dustDist;
                    targetUnderLens = _junkDust;
                }
            }

            // 5. Логика удержания 2 секунды над объектом
            if (targetUnderLens != null)
            {
                if (_focusedTarget != targetUnderLens)
                {
                    if (_focusedTarget != null)
                    {
                        _focusedTarget.SetHoldProgress(0f);
                    }
                    _focusedTarget = targetUnderLens;
                    _holdTimer = 0f;
                }

                _holdTimer += Time.unscaledDeltaTime;
                float progress01 = Mathf.Clamp01(_holdTimer / _requiredHoldDuration);
                _focusedTarget.SetHoldProgress(progress01);

                if (_lensProgressRing != null)
                {
                    _lensProgressRing.fillAmount = progress01;
                }

                if (_instructionText != null)
                {
                    if (_focusedTarget.IsJunkDust)
                    {
                        _instructionText.text = $"<color=#FF8844>Подозрительный объект [X]... Удержание: {_holdTimer:F1} / {_requiredHoldDuration:F1} сек</color>";
                    }
                    else
                    {
                        _instructionText.text = $"<color=#00FFFF>Изучение кристалла... Удержание: {_holdTimer:F1} / {_requiredHoldDuration:F1} сек</color>";
                    }
                }

                if (_holdTimer >= _requiredHoldDuration)
                {
                    _focusedTarget.Collect();
                    _focusedTarget = null;
                    _holdTimer = 0f;
                    if (_lensProgressRing != null) _lensProgressRing.fillAmount = 0f;
                }
            }
            else
            {
                if (_focusedTarget != null)
                {
                    _focusedTarget.SetHoldProgress(0f);
                    _focusedTarget = null;
                }
                _holdTimer = 0f;
                if (_lensProgressRing != null) _lensProgressRing.fillAmount = 0f;

                if (_instructionText != null && !_isCompleted)
                {
                    _instructionText.text = "Перемещайте лупу по области, находите скрытые кристаллы и удерживайте 2 сек для сбора!";
                }
            }
        }

        private void HandleTargetCollected(HiddenRevealTarget target)
        {
            _collectedCount++;
            UpdateUI();

            float progress = Mathf.Clamp01((float)_collectedCount / _totalCrystals);
            SetProgress(progress);

            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#00FF99>Кристалл успешно изучен и собран! ({_collectedCount}/{_totalCrystals})</color>";
            }

            if (_collectedCount >= _totalCrystals)
            {
                CompleteMechanic();
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#00FF99>Все 3 скрытых кристалла обнаружены и собраны!</color>";
                }
            }
        }

        private void HandleDustCollected(HiddenRevealTarget dust)
        {
            if (CurrentStage >= 2)
            {
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#FF3333>Изучена пыль [X]! Этап сброшен.</color>";
                }
                FailStage("изучен ложный объект — пыль [X]");
                return;
            }
            if (_instructionText != null)
            {
                _instructionText.text = "<color=#FF5555>Внимание! Это ложное скопление пыли [X], а не кристалл!</color>";
            }
        }

        private void UpdateUI()
        {
            if (_scoreText != null)
            {
                _scoreText.text = $"Найдено: {_collectedCount} / {_totalCrystals}";
            }
        }
    }

    /// <summary>
    /// Прокси-компонент для перехвата событий мыши/тача на лупе и темной зоне.
    /// </summary>
    public class M23DragProxy : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private M23_StencilRevealMechanic _mechanic;

        public void Initialize(M23_StencilRevealMechanic mechanic)
        {
            _mechanic = mechanic;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_mechanic != null) _mechanic.OnProxyPointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_mechanic != null) _mechanic.OnProxyDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_mechanic != null) _mechanic.OnProxyPointerUp(eventData);
        }
    }
}
