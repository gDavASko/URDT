using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M32_AirplaneStarGlider
{
    /// <summary>
    /// Механика #32: Полет самолетика со сбором звездочек (Airplane Flight & Star Glider).
    /// Задача: управляя высотой планирующего самолетика, поймать 5 золотых звездочек и избегать грозовых туч [X].
    /// Управление: ведение пальцем/курсором по экрану (или клавиши W/S / стрелки Вверх/Вниз).
    /// </summary>
    public class M32_AirplaneStarGliderMechanic : BaseMechanic2DModule, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Визуальные элементы полета")]
        [SerializeField] private RectTransform _airplaneRoot = null;
        [SerializeField] private RectTransform _flightContainer = null;
        [SerializeField] private RectTransform _starsContainer = null;
        [SerializeField] private RectTransform _cloudsContainer = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _starsCountText = null;
        [SerializeField] private Image _progressFill = null;
        [SerializeField] private TMP_Text _instructionText = null;

        [Header("Параметры полета")]
        [SerializeField] private float _minY = -120f;
        [SerializeField] private float _maxY = 120f;
        [SerializeField] private float _smoothSpeed = 8f;
        [SerializeField] private float _scrollSpeed = 160f;
        [SerializeField] private int _targetStars = 5;

        // Параметры по этапам: больше звёзд, быстрее прокрутка, больше туч
        private static readonly int[]   STAGE_TARGET_STARS  = { 5, 7, 10 };
        private static readonly float[] STAGE_SCROLL_SPEED  = { 160f, 210f, 265f };
        private static readonly int[]   STAGE_EXTRA_STARS   = { 0, 3, 6 };
        private static readonly int[]   STAGE_EXTRA_CLOUDS  = { 0, 1, 2 };

        public override int StageCount => 3;

        private readonly List<RectTransform> _spawnedStars = new List<RectTransform>();
        private readonly List<RectTransform> _spawnedClouds = new List<RectTransform>();

        /// <summary>Сколько звёзд собрано на текущем этапе.</summary>
        public int CollectedStars => _collectedStars;
        /// <summary>Сколько звёзд нужно на текущем этапе.</summary>
        public int StageTargetStars => _targetStars;

        private float _targetY = 0f;
        private float _currentY = 0f;
        private int _collectedStars = 0;
        private bool _isSteering = false;
        private Canvas _canvas;

        private List<RectTransform> _activeStars = new List<RectTransform>();
        private List<RectTransform> _activeClouds = new List<RectTransform>();

        protected override void Awake()
        {
            base.Awake();
            _canvas = GetComponentInParent<Canvas>();
        }

        public override void Initialize()
        {
            base.Initialize();
            _collectedStars = 0;
            _targetY = 0f;
            _currentY = 0f;
            _isSteering = false;

            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_TARGET_STARS.Length - 1);
            _targetStars = STAGE_TARGET_STARS[idx];
            _scrollSpeed = STAGE_SCROLL_SPEED[idx];

            if (_airplaneRoot != null)
            {
                _airplaneRoot.anchoredPosition = new Vector2(-180f, 0f);
                _airplaneRoot.localRotation = Quaternion.identity;
            }

            // Уничтожаем клоны предыдущего этапа немедленно, чтобы они не попали в SetupFlightItems.
            for (int i = 0; i < _spawnedStars.Count; i++) if (_spawnedStars[i] != null) DestroyImmediate(_spawnedStars[i].gameObject);
            _spawnedStars.Clear();
            for (int i = 0; i < _spawnedClouds.Count; i++) if (_spawnedClouds[i] != null) DestroyImmediate(_spawnedClouds[i].gameObject);
            _spawnedClouds.Clear();

            SetupFlightItems();
            SpawnExtraFlightItems();

            UpdateUI();
            SetProgress(0f);

            if (_instructionText != null)
            {
                _instructionText.text = GetStageInstruction(CurrentStage);
            }
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, STAGE_TARGET_STARS.Length - 1);
            int stars = STAGE_TARGET_STARS[idx];
            return $"Этап {stage}/3. Соберите {stars} звёзд, избегая грозовых туч [X]. Удар о тучу — провал этапа!";
        }

        private void SpawnExtraFlightItems()
        {
            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_EXTRA_STARS.Length - 1);
            int extraStars = STAGE_EXTRA_STARS[idx];
            int extraClouds = STAGE_EXTRA_CLOUDS[idx];

            if (extraStars > 0 && _starsContainer != null && _activeStars.Count > 0)
            {
                RectTransform template = _activeStars[0];
                for (int i = 0; i < extraStars; i++)
                {
                    RectTransform clone = Instantiate(template.gameObject, _starsContainer).GetComponent<RectTransform>();
                    clone.name = $"Star_Extra_{i + 1}";
                    clone.anchoredPosition = new Vector2(80f + i * 110f, Mathf.Sin(i * 1.7f) * 90f);
                    clone.gameObject.SetActive(true);
                    _activeStars.Add(clone);
                    _spawnedStars.Add(clone);
                }
            }

            if (extraClouds > 0 && _cloudsContainer != null && _activeClouds.Count > 0)
            {
                RectTransform template = _activeClouds[0];
                for (int i = 0; i < extraClouds; i++)
                {
                    RectTransform clone = Instantiate(template.gameObject, _cloudsContainer).GetComponent<RectTransform>();
                    clone.name = $"Cloud_Extra_{i + 1}";
                    clone.anchoredPosition = new Vector2(140f + i * 130f, Mathf.Cos(i * 2.1f) * 80f);
                    clone.gameObject.SetActive(true);
                    _activeClouds.Add(clone);
                    _spawnedClouds.Add(clone);
                }
            }
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void SetupFlightItems()
        {
            _activeStars.Clear();
            if (_starsContainer != null)
            {
                for (int i = 0; i < _starsContainer.childCount; i++)
                {
                    var child = _starsContainer.GetChild(i) as RectTransform;
                    if (child != null)
                    {
                        child.gameObject.SetActive(true);
                        _activeStars.Add(child);
                    }
                }
            }

            _activeClouds.Clear();
            if (_cloudsContainer != null)
            {
                for (int i = 0; i < _cloudsContainer.childCount; i++)
                {
                    var child = _cloudsContainer.GetChild(i) as RectTransform;
                    if (child != null)
                    {
                        child.gameObject.SetActive(true);
                        _activeClouds.Add(child);
                    }
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isCompleted || IsInTransition) return;
            _isSteering = true;
            UpdateTargetFromPointer(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isSteering || _isCompleted || IsInTransition) return;
            UpdateTargetFromPointer(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isSteering = false;
        }

        private void UpdateTargetFromPointer(Vector2 screenPos)
        {
            if (_flightContainer == null) return;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_flightContainer, screenPos, cam, out Vector2 localPoint))
            {
                _targetY = Mathf.Clamp(localPoint.y, _minY, _maxY);
            }
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;

            // Клавиатура W/S или Стрелки
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) _targetY += 180f * Time.deltaTime;
            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) _targetY -= 180f * Time.deltaTime;
            _targetY = Mathf.Clamp(_targetY, _minY, _maxY);

            // Плавное следование самолетика
            float prevY = _currentY;
            _currentY = Mathf.Lerp(_currentY, _targetY, Time.deltaTime * _smoothSpeed);
            float velY = (_currentY - prevY) / Mathf.Max(0.0001f, Time.deltaTime);

            if (_airplaneRoot != null)
            {
                _airplaneRoot.anchoredPosition = new Vector2(-180f, _currentY);
                // Тангаж (наклон носа) по скорости подъема/спуска
                float pitch = Mathf.Clamp(velY * 0.15f, -30f, 30f);
                _airplaneRoot.localRotation = Quaternion.Euler(0f, 0f, pitch);
            }

            // Скроллинг звезд и облаков навстречу
            float dt = Time.deltaTime;
            ScrollAndCheckCollisions(dt);
        }

        private void ScrollAndCheckCollisions(float dt)
        {
            Vector2 planePos = _airplaneRoot != null ? _airplaneRoot.anchoredPosition : new Vector2(-180f, _currentY);
            float catchRadius = 38f;

            // Звездочки
            for (int i = _activeStars.Count - 1; i >= 0; i--)
            {
                var star = _activeStars[i];
                if (star == null || !star.gameObject.activeSelf) continue;

                Vector2 pos = star.anchoredPosition;
                pos.x -= _scrollSpeed * dt;

                // Зацикливание если улетела влево
                if (pos.x < -320f)
                {
                    pos.x = 320f;
                }
                star.anchoredPosition = pos;

                // Проверка столкновения с самолетиком
                if (Vector2.Distance(pos, planePos) <= catchRadius)
                {
                    star.gameObject.SetActive(false);
                    _collectedStars++;

                    float progress = Mathf.Clamp01((float)_collectedStars / _targetStars);
                    SetProgress(progress);
                    UpdateUI();

                    if (_collectedStars >= _targetStars)
                    {
                        CompleteFlight();
                        return;
                    }
                }
            }

            // Препятствия (грозовые тучи [X])
            for (int i = 0; i < _activeClouds.Count; i++)
            {
                var cloud = _activeClouds[i];
                if (cloud == null) continue;

                Vector2 pos = cloud.anchoredPosition;
                pos.x -= (_scrollSpeed * 0.85f) * dt;
                if (pos.x < -320f)
                {
                    pos.x = 340f;
                }
                cloud.anchoredPosition = pos;

                if (Vector2.Distance(pos, planePos) <= 42f)
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF5555>Столкновение с грозовой тучей [X]! Этап провален.</color>";
                    }
                    FailStage("Столкновение с грозовой тучей");
                    return;
                }
            }
        }

        private void CompleteFlight()
        {
            CompleteMechanic();

            if (_instructionText != null)
            {
                _instructionText.text = "<color=#00FF99>Великолепно! Все 5 звезд собраны, полет успешно завершен!</color>";
            }

            // Победный полет самолетика вперед
            if (_airplaneRoot != null)
            {
                _airplaneRoot.anchoredPosition = new Vector2(250f, _currentY);
                _airplaneRoot.localRotation = Quaternion.Euler(0f, 0f, 15f);
            }
        }

        private void UpdateUI()
        {
            if (_starsCountText != null)
            {
                _starsCountText.text = $"Звезды: {_collectedStars} / {_targetStars}";
            }

            if (_progressFill != null)
            {
                _progressFill.fillAmount = Mathf.Clamp01((float)_collectedStars / _targetStars);
            }
        }
    }
}
