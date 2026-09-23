using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M13_LaneSwitcherRunner
{
    /// <summary>
    /// Режим управления раннером: свайпы, тапы по половинам или прямое вождение приёмника.
    /// </summary>
    public enum RunnerControlMode
    {
        Swipe,            // Свайпы влево / вправо
        HalfScreenTap,    // Тапы по левой / правой половине экрана
        DirectDrag        // Прямое удержание и вождение мышкой
    }

    /// <summary>
    /// Элемент раннера: монетка или барьер.
    /// </summary>
    public enum RunnerItemType
    {
        Coin,
        BarrierHazard
    }

    public class RunnerItem
    {
        public RectTransform Transform;
        public RunnerItemType Type;
        public int Lane;
        public bool IsCollected;
    }

    /// <summary>
    /// Механика #13 (и дубликаты #25, #26): Переключение полос в раннере (Lane Switcher Runner).
    /// Поддерживает три схемы управления: свайпы (Swipe), тапы по половинам экрана (HalfScreenTap) и прямое вождение приёмника (DirectDrag).
    /// </summary>
    public class M13_LaneSwitcherRunnerMechanic : BaseMechanic2DModule, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Режим управления")]
        [SerializeField] private RunnerControlMode _controlMode = RunnerControlMode.Swipe;

        [Header("Полосы движения")]
        [SerializeField] private float[] _laneXPositions = new float[] { -140f, 0f, 140f };
        [SerializeField] private RectTransform _playerAvatar = null;
        [SerializeField] private RectTransform _spawnContainer = null;

        [Header("Спрайты")]
        [SerializeField] private Sprite _coinSprite = null;
        [SerializeField] private Sprite _barrierSprite = null;

        [Header("UI Кнопки управления (скрываются в жестовых режимах)")]
        [SerializeField] private Button _btnLeft = null;
        [SerializeField] private Button _btnRight = null;
        [SerializeField] private TMP_Text _scoreText = null;
        [SerializeField] private TMP_Text _instructionText = null;

        [Header("Параметры механики")]
        [SerializeField] private int _targetCoins = 5;
        [SerializeField] private float _scrollSpeed = 160f;
        [SerializeField] private float _spawnInterval = 1.6f;

        // Параметры по этапам: 1 — базовый, 2 — быстрее и больше монет, 3 — самый жёсткий
        private static readonly int[]   STAGE_TARGET_COINS   = { 5, 7, 10 };
        private static readonly float[] STAGE_SCROLL_SPEED   = { 160f, 215f, 265f };
        private static readonly float[] STAGE_SPAWN_INTERVAL = { 1.6f, 1.35f, 1.1f };
        private static readonly float[] STAGE_BARRIER_CHANCE = { 0.65f, 0.85f, 1.0f };
        private static readonly bool[]  STAGE_BARRIER_FATAL  = { false, true, true };

        public override int StageCount => 3;

        private int _currentLane = 1; // 0, 1, 2
        private int _collectedCoins = 0;
        private float _spawnTimer = 0f;
        private List<RunnerItem> _activeItems = new List<RunnerItem>();
        private List<GameObject> _spawnedObjects = new List<GameObject>();

        /// <summary>Сколько монет уже собрано на текущем этапе.</summary>
        public int CollectedCoins => _collectedCoins;
        /// <summary>Сколько монет нужно собрать на текущем этапе.</summary>
        public int StageTargetCoins => _targetCoins;
        /// <summary>Столкновение с барьером на этом этапе провальное (true) или мягкий штраф (false).</summary>
        public bool BarrierIsFatal => STAGE_BARRIER_FATAL[Mathf.Clamp(CurrentStage - 1, 0, STAGE_BARRIER_FATAL.Length - 1)];

        // Жестовый ввод
        private Vector2 _pointerDownPos;
        private bool _isPointerDown = false;
        private bool _swipeTriggered = false;
        private bool _isDraggingDirect = false;
        private Canvas _canvas;

        public RunnerControlMode ControlMode
        {
            get => _controlMode;
            set
            {
                _controlMode = value;
                UpdateControlSchemeVisuals();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _canvas = GetComponentInParent<Canvas>();

            // Гарантируем прозрачный Image на корне, чтобы ловить клики и жесты по всей рабочей области
            Image rootImg = GetComponent<Image>();
            if (rootImg == null)
            {
                rootImg = gameObject.AddComponent<Image>();
            }
            rootImg.color = Color.clear;
            rootImg.raycastTarget = true;

            if (_btnLeft != null) _btnLeft.onClick.AddListener(MoveLeft);
            if (_btnRight != null) _btnRight.onClick.AddListener(MoveRight);
        }

        private void OnDestroy()
        {
            if (_btnLeft != null) _btnLeft.onClick.RemoveListener(MoveLeft);
            if (_btnRight != null) _btnRight.onClick.RemoveListener(MoveRight);
            ClearAllSpawned();
        }

        public override void Initialize()
        {
            base.Initialize();

            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_TARGET_COINS.Length - 1);
            _targetCoins = STAGE_TARGET_COINS[idx];
            _scrollSpeed = STAGE_SCROLL_SPEED[idx];
            _spawnInterval = STAGE_SPAWN_INTERVAL[idx];

            _currentLane = 1;
            _collectedCoins = 0;
            _spawnTimer = 0.5f;
            _isPointerDown = false;
            _swipeTriggered = false;
            _isDraggingDirect = false;

            ClearAllSpawned();
            UpdatePlayerPosition(true);
            UpdateUI();
            UpdateControlSchemeVisuals();
            SetProgress(0f);
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, STAGE_TARGET_COINS.Length - 1);
            int coins = STAGE_TARGET_COINS[idx];
            bool fatal = STAGE_BARRIER_FATAL[idx];
            string ctrl;
            switch (_controlMode)
            {
                case RunnerControlMode.Swipe:         ctrl = "Свайпайте влево / вправо по экрану для смены полосы."; break;
                case RunnerControlMode.HalfScreenTap: ctrl = "Нажимайте на левую или правую половину экрана для смены полосы."; break;
                default:                              ctrl = "Зажмите приёмник и ведите его влево-вправо мышкой."; break;
            }
            string tail = fatal
                ? $"Соберите {coins} монет. Столкновение с барьером [X] — провал этапа!"
                : $"Соберите {coins} монет. Барьер [X] снимает 1 монету — обходите его.";
            return $"Этап {stage}/3. {ctrl} {tail}";
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void UpdateControlSchemeVisuals()
        {
            // Прячем экранные кнопки влево/вправо в жестовых режимах
            if (_btnLeft != null) _btnLeft.gameObject.SetActive(false);
            if (_btnRight != null) _btnRight.gameObject.SetActive(false);

            _instruction = GetStageInstruction(CurrentStage);

            if (_instructionText != null)
            {
                _instructionText.text = _instruction;
            }
        }

        public void MoveLeft()
        {
            if (_isCompleted || IsInTransition) return;
            if (_currentLane > 0)
            {
                _currentLane--;
                UpdatePlayerPosition(false);
            }
        }

        public void MoveRight()
        {
            if (_isCompleted || IsInTransition) return;
            if (_currentLane < _laneXPositions.Length - 1)
            {
                _currentLane++;
                UpdatePlayerPosition(false);
            }
        }

        #region EventSystem Pointer Handlers
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isCompleted || IsInTransition) return;
            _isPointerDown = true;
            _pointerDownPos = eventData.position;
            _swipeTriggered = false;

            if (_controlMode == RunnerControlMode.HalfScreenTap)
            {
                HandleHalfScreenTap(eventData);
            }
            else if (_controlMode == RunnerControlMode.DirectDrag)
            {
                _isDraggingDirect = true;
                HandleDirectDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isCompleted || IsInTransition || !_isPointerDown) return;

            if (_controlMode == RunnerControlMode.Swipe)
            {
                float deltaX = eventData.position.x - _pointerDownPos.x;
                float threshold = 38f;

                if (!_swipeTriggered && Mathf.Abs(deltaX) >= threshold)
                {
                    if (deltaX > 0f) MoveRight();
                    else MoveLeft();

                    _swipeTriggered = true;
                    _pointerDownPos = eventData.position;
                }
                else if (_swipeTriggered && Mathf.Abs(deltaX) >= threshold)
                {
                    // Позволяет делать повторные свайпы без отпускания пальца/мыши
                    if (deltaX > 0f) MoveRight();
                    else MoveLeft();
                    _pointerDownPos = eventData.position;
                }
            }
            else if (_controlMode == RunnerControlMode.DirectDrag)
            {
                HandleDirectDrag(eventData);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isCompleted) return;

            if (_controlMode == RunnerControlMode.Swipe && !_swipeTriggered && _isPointerDown)
            {
                float deltaX = eventData.position.x - _pointerDownPos.x;
                float threshold = 25f;
                if (Mathf.Abs(deltaX) >= threshold)
                {
                    if (deltaX > 0f) MoveRight();
                    else MoveLeft();
                }
            }

            if (_controlMode == RunnerControlMode.DirectDrag && _isDraggingDirect)
            {
                // При отпускании притягиваемся к ближайшей полосе
                if (_playerAvatar != null)
                {
                    _currentLane = GetNearestLane(_playerAvatar.anchoredPosition.x);
                }
            }

            _isPointerDown = false;
            _swipeTriggered = false;
            _isDraggingDirect = false;
        }

        private void HandleHalfScreenTap(PointerEventData eventData)
        {
            RectTransform rt = transform as RectTransform;
            if (rt == null) return;

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (_canvas.worldCamera != null ? _canvas.worldCamera : Camera.main)
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, cam, out Vector2 localPoint))
            {
                if (localPoint.x < 0f)
                {
                    MoveLeft();
                }
                else
                {
                    MoveRight();
                }
            }
        }

        private void HandleDirectDrag(PointerEventData eventData)
        {
            if (_playerAvatar == null) return;
            RectTransform parentRt = _playerAvatar.parent as RectTransform;
            if (parentRt == null) parentRt = transform as RectTransform;

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (_canvas.worldCamera != null ? _canvas.worldCamera : Camera.main)
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, eventData.position, cam, out Vector2 localPoint))
            {
                float minX = _laneXPositions[0] - 25f;
                float maxX = _laneXPositions[_laneXPositions.Length - 1] + 25f;
                float clampedX = Mathf.Clamp(localPoint.x, minX, maxX);

                Vector2 pos = _playerAvatar.anchoredPosition;
                pos.x = clampedX;
                _playerAvatar.anchoredPosition = pos;

                _currentLane = GetNearestLane(clampedX);
            }
        }

        private int GetNearestLane(float x)
        {
            int bestLane = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _laneXPositions.Length; i++)
            {
                float d = Mathf.Abs(_laneXPositions[i] - x);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestLane = i;
                }
            }
            return bestLane;
        }
        #endregion

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;

            // Клавиатурный ввод A / D или Left / Right (дополнительно к жестам)
            if (UnityEngine.Input.GetKeyDown(KeyCode.A) || UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow)) MoveLeft();
            if (UnityEngine.Input.GetKeyDown(KeyCode.D) || UnityEngine.Input.GetKeyDown(KeyCode.RightArrow)) MoveRight();

            // Плавное движение игрока к целевой полосе (если сейчас не идёт прямое вождение мышкой)
            if (_playerAvatar != null && (!_isDraggingDirect || _controlMode != RunnerControlMode.DirectDrag))
            {
                float targetX = _laneXPositions[_currentLane];
                Vector2 currentPos = _playerAvatar.anchoredPosition;
                currentPos.x = Mathf.Lerp(currentPos.x, targetX, Time.unscaledDeltaTime * 18f);
                _playerAvatar.anchoredPosition = currentPos;
            }

            // Спавн элементов
            _spawnTimer -= Time.unscaledDeltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = _spawnInterval;
                SpawnRow();
            }

            // Перемещение и проверка столкновений
            float playerY = _playerAvatar != null ? _playerAvatar.anchoredPosition.y : -100f;
            float playerX = _playerAvatar != null ? _playerAvatar.anchoredPosition.x : _laneXPositions[_currentLane];
            float hitThresholdY = 35f;

            for (int i = _activeItems.Count - 1; i >= 0; i--)
            {
                var item = _activeItems[i];
                if (item.Transform == null)
                {
                    _activeItems.RemoveAt(i);
                    continue;
                }

                Vector2 pos = item.Transform.anchoredPosition;
                pos.y -= _scrollSpeed * Time.unscaledDeltaTime;
                item.Transform.anchoredPosition = pos;

                // Проверка попадания в игрока:
                // в DirectDrag — по непрерывному расстоянию X, в Swipe / HalfScreenTap — по полосе
                bool isColliding = false;
                if (!item.IsCollected && Mathf.Abs(pos.y - playerY) < hitThresholdY)
                {
                    if (_controlMode == RunnerControlMode.DirectDrag)
                    {
                        isColliding = Mathf.Abs(pos.x - playerX) < 45f;
                    }
                    else
                    {
                        isColliding = item.Lane == _currentLane;
                    }
                }

                if (isColliding)
                {
                    item.IsCollected = true;
                    if (item.Type == RunnerItemType.Coin)
                    {
                        _collectedCoins++;
                        item.Transform.gameObject.SetActive(false);
                        UpdateUI();

                        float progress = Mathf.Clamp01((float)_collectedCoins / _targetCoins);
                        SetProgress(progress);

                        if (_collectedCoins >= _targetCoins)
                        {
                            CompleteMechanic();
                            if (_instructionText != null)
                            {
                                _instructionText.text = "<color=#00FF99>Отличная реакция! Все монеты собраны, финиш пройден!</color>";
                            }
                            return;
                        }
                    }
                    else if (item.Type == RunnerItemType.BarrierHazard)
                    {
                        item.Transform.gameObject.SetActive(false);
                        if (BarrierIsFatal)
                        {
                            if (_instructionText != null)
                            {
                                _instructionText.text = "<color=#FF4444>Столкновение с барьером [X]! Этап провален.</color>";
                            }
                            FailStage("Столкновение с барьером");
                            return;
                        }
                        else
                        {
                            if (_collectedCoins > 0) _collectedCoins--;
                            UpdateUI();
                            if (_instructionText != null)
                            {
                                _instructionText.text = "<color=#FF4444>Удар о шипастый барьер [X]! Штраф -1 монета!</color>";
                            }
                        }
                    }
                }

                // Удаление ушедших за пределы экрана объектов
                if (pos.y < -200f)
                {
                    if (item.Transform != null) Destroy(item.Transform.gameObject);
                    _activeItems.RemoveAt(i);
                }
            }
        }

        private void SpawnRow()
        {
            if (_spawnContainer == null) return;

            // Выбираем случайную полосу для монеты
            int coinLane = UnityEngine.Random.Range(0, 3);
            SpawnItem(RunnerItemType.Coin, coinLane, 160f);

            // Иногда на другой полосе спавним барьер (частота растёт с этапом)
            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_BARRIER_CHANCE.Length - 1);
            if (UnityEngine.Random.value < STAGE_BARRIER_CHANCE[idx])
            {
                int barrierLane = (coinLane + UnityEngine.Random.Range(1, 3)) % 3;
                SpawnItem(RunnerItemType.BarrierHazard, barrierLane, 160f);
            }
        }

        private void SpawnItem(RunnerItemType type, int lane, float startY)
        {
            GameObject obj = new GameObject(type == RunnerItemType.Coin ? "Coin" : "Barrier", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(_spawnContainer, false);
            _spawnedObjects.Add(obj);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(_laneXPositions[lane], startY);
            rt.sizeDelta = type == RunnerItemType.Coin ? new Vector2(40f, 40f) : new Vector2(70f, 30f);

            Image img = obj.GetComponent<Image>();
            img.sprite = type == RunnerItemType.Coin ? _coinSprite : _barrierSprite;
            img.color = Color.white;
            img.raycastTarget = false;

            RunnerItem item = new RunnerItem
            {
                Transform = rt,
                Type = type,
                Lane = lane,
                IsCollected = false
            };
            _activeItems.Add(item);
        }

        private void UpdatePlayerPosition(bool immediate)
        {
            if (_playerAvatar == null) return;
            float targetX = _laneXPositions[_currentLane];
            if (immediate)
            {
                _playerAvatar.anchoredPosition = new Vector2(targetX, _playerAvatar.anchoredPosition.y);
            }
        }

        private void UpdateUI()
        {
            if (_scoreText != null)
            {
                _scoreText.text = $"Собрано: {_collectedCoins} / {_targetCoins}";
            }
        }

        private void ClearAllSpawned()
        {
            _activeItems.Clear();
            foreach (var go in _spawnedObjects)
            {
                if (go != null) Destroy(go);
            }
            _spawnedObjects.Clear();
        }
    }
}
