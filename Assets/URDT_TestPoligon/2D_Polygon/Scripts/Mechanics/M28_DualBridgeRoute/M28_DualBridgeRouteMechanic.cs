using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M28_DualBridgeRoute
{
    /// <summary>
    /// Механика #28: Построение маршрута с мостиками для двух сущностей в 2 финиша (Dual Bridge Route).
    /// Задача: перетащить исправные мостики в два разрыва трассы и запустить двух персонажей к их финишам.
    /// Мешающий фактор: сломанный мостик [X], не выдерживающий нагрузки.
    /// </summary>
    public class M28_DualBridgeRouteMechanic : BaseMechanic2DModule
    {
        [Header("Сущности и финиши")]
        [SerializeField] private RectTransform _entityA = null; // Синий бот
        [SerializeField] private RectTransform _entityB = null; // Оранжевый бот
        [SerializeField] private RectTransform _goalA = null;
        [SerializeField] private RectTransform _goalB = null;

        [Header("Разрывы дорожек (Слоты мостов)")]
        [SerializeField] private RectTransform _slotA = null;
        [SerializeField] private RectTransform _slotB = null;
        [SerializeField] private float _snapRadius = 75f;

        [Header("Мостики")]
        [SerializeField] private BridgePlank[] _planks = null;

        [Header("Управление и UI")]
        [SerializeField] private Button _btnStart = null;
        [SerializeField] private TMP_Text _instructionText = null;
        [SerializeField] private Image _progressFill = null;

        private Vector2 _startPosA = new Vector2(-230f, 75f);
        private Vector2 _startPosB = new Vector2(-230f, 5f);
        private BridgePlank _slottedPlankA;
        private BridgePlank _slottedPlankB;
        private bool _isWalking = false;

        // Параметры по этапам: добавляем сломанные мостики-декои и вводим лимит времени на 3-м
        private static readonly int[]   STAGE_EXTRA_BROKEN = { 0, 1, 2 };
        private static readonly float[] STAGE_TIME_LIMIT   = { 0f, 0f, 25f };
        private static readonly bool[]  STAGE_BROKEN_FATAL = { false, true, true };

        private readonly List<BridgePlank> _spawnedPlanks = new List<BridgePlank>();
        private float _timeLeft = 0f;

        public override int StageCount => 3;

        /// <summary>Мостиков установлено (0..2).</summary>
        public int BridgesPlaced => (_slottedPlankA != null ? 1 : 0) + (_slottedPlankB != null ? 1 : 0);
        /// <summary>Оставшееся время (сек) или 0, если лимит не действует.</summary>
        public float TimeLeft => _timeLeft;

        protected override void Awake()
        {
            base.Awake();
            if (_entityA != null) _startPosA = _entityA.anchoredPosition;
            if (_entityB != null) _startPosB = _entityB.anchoredPosition;

            if (_btnStart != null)
            {
                _btnStart.onClick.AddListener(OnStartClicked);
            }

            BindPlankEvents();
        }

        private void Start()
        {
            if (_entityA != null && _startPosA == Vector2.zero) _startPosA = _entityA.anchoredPosition;
            if (_entityB != null && _startPosB == Vector2.zero) _startPosB = _entityB.anchoredPosition;
            UpdateStateUI();
        }

        private void Update()
        {
            if (_isCompleted || _isWalking || IsInTransition) return;

            // Обратный отсчёт времени на этапе с лимитом
            if (_timeLeft > 0f)
            {
                _timeLeft -= Time.deltaTime;
                if (_timeLeft <= 0f)
                {
                    _timeLeft = 0f;
                    FailStage("Время на установку мостов истекло");
                    return;
                }
            }

            bool ready = _slottedPlankA != null && _slottedPlankB != null;
            if (ready && (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return)))
            {
                OnStartClicked();
            }
        }

        private void BindPlankEvents()
        {
            if (_planks == null || _planks.Length == 0)
            {
                // Первый вызов — берём авторские мостики; клоны привязываем отдельно
                _planks = GetComponentsInChildren<BridgePlank>(true);
            }

            if (_planks != null)
            {
                foreach (var plank in _planks)
                {
                    if (plank != null)
                    {
                        plank.OnPlankEndDrag -= HandlePlankEndDrag;
                        plank.OnPlankEndDrag += HandlePlankEndDrag;
                    }
                }
            }

            foreach (var plank in _spawnedPlanks)
            {
                if (plank != null)
                {
                    plank.OnPlankEndDrag -= HandlePlankEndDrag;
                    plank.OnPlankEndDrag += HandlePlankEndDrag;
                }
            }
        }

        private void OnDestroy()
        {
            if (_btnStart != null) _btnStart.onClick.RemoveListener(OnStartClicked);

            if (_planks != null)
            {
                foreach (var plank in _planks)
                {
                    if (plank != null)
                    {
                        plank.OnPlankEndDrag -= HandlePlankEndDrag;
                        // A plank still parented outside this level (mid-drag on the canvas root) dies with it.
                        if (!plank.transform.IsChildOf(transform)) Destroy(plank.gameObject);
                    }
                }
            }

            foreach (var plank in _spawnedPlanks)
            {
                if (plank != null)
                {
                    plank.OnPlankEndDrag -= HandlePlankEndDrag;
                    if (!plank.transform.IsChildOf(transform)) Destroy(plank.gameObject);
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            StopAllCoroutines();
            _isWalking = false;
            _slottedPlankA = null;
            _slottedPlankB = null;

            // Уничтожаем клоны с прошлого запуска
            for (int i = 0; i < _spawnedPlanks.Count; i++)
            {
                if (_spawnedPlanks[i] != null) Destroy(_spawnedPlanks[i].gameObject);
            }
            _spawnedPlanks.Clear();

            if (_entityA != null) _entityA.anchoredPosition = _startPosA;
            if (_entityB != null) _entityB.anchoredPosition = _startPosB;

            if (_planks != null)
            {
                foreach (var plank in _planks)
                {
                    if (plank != null) plank.ResetPlank();
                }
            }

            SpawnExtraBrokenPlanks();
            BindPlankEvents();

            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_TIME_LIMIT.Length - 1);
            _timeLeft = STAGE_TIME_LIMIT[idx];

            UpdateStateUI();
            SetProgress(0f);
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, STAGE_EXTRA_BROKEN.Length - 1);
            bool fatal = STAGE_BROKEN_FATAL[idx];
            float limit = STAGE_TIME_LIMIT[idx];
            string t = limit > 0f ? $" Лимит времени: {limit:F0} сек." : "";
            string b = fatal
                ? " Попытка поставить сломанный мост [X] в разрыв — провал!"
                : " Сломанный мост [X] не подходит — используйте целый.";
            return $"Этап {stage}/3. Установите оба целых мостика в разрывы путей и нажмите ПУСК.{b}{t}";
        }

        private void SpawnExtraBrokenPlanks()
        {
            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_EXTRA_BROKEN.Length - 1);
            int extra = STAGE_EXTRA_BROKEN[idx];
            if (extra <= 0 || _planks == null || _planks.Length == 0) return;

            BridgePlank template = null;
            foreach (var p in _planks)
            {
                if (p != null && p.IsBroken) { template = p; break; }
            }
            if (template == null)
            {
                // Возьмем первый доступный целый — сделаем клон сломанным
                foreach (var p in _planks)
                {
                    if (p != null) { template = p; break; }
                }
            }
            if (template == null) return;

            int nextId = 90; // Явно вне диапазона авторских мостиков
            Vector2 basePos = template.InitialPosition;
            Transform parent = template.transform.parent;
            for (int i = 0; i < extra; i++)
            {
                BridgePlank clone = Instantiate(template, parent);
                clone.name = $"BridgePlank_Broken_Extra_{i + 1}";
                clone.SetBroken(true);
                clone.SetBridgeId(nextId + i);
                clone.SetInitialOrigin(parent, basePos + new Vector2(0f, -46f * (i + 1)));
                _spawnedPlanks.Add(clone);
            }
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void HandlePlankEndDrag(BridgePlank plank, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (plank == null) return;
            // While the bots walk (or after completion) a drop is ignored, but the plank must still leave the canvas
            // root it was lifted to in OnBeginDrag — otherwise it outlives this level instance as a ghost.
            if (_isWalking || _isCompleted)
            {
                if (_slottedPlankA != plank && _slottedPlankB != plank) plank.ReturnToOrigin();
                else
                {
                    plank.transform.SetParent(transform, true);
                    plank.RectTransform.anchoredPosition = plank == _slottedPlankA ? _slotA.anchoredPosition : _slotB.anchoredPosition;
                }
                return;
            }

            RectTransform rootRt = transform as RectTransform;
            Vector2 plankPosInRoot = rootRt.InverseTransformPoint(plank.transform.position);
            Vector2 slotAPosInRoot = _slotA != null ? (Vector2)rootRt.InverseTransformPoint(_slotA.position) : new Vector2(9999f, 9999f);
            Vector2 slotBPosInRoot = _slotB != null ? (Vector2)rootRt.InverseTransformPoint(_slotB.position) : new Vector2(9999f, 9999f);

            float distA = Vector2.Distance(plankPosInRoot, slotAPosInRoot);
            float distB = Vector2.Distance(plankPosInRoot, slotBPosInRoot);

            int stageIdx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_BROKEN_FATAL.Length - 1);
            bool brokenFatal = STAGE_BROKEN_FATAL[stageIdx];

            // Проверяем попадание в слот A
            if (distA <= _snapRadius && distA <= distB)
            {
                if (plank.IsBroken)
                {
                    ShowWarning("Сломанный мостик [X] рухнет под весом бота! Используйте целый мост.");
                    plank.ReturnToOrigin();
                    if (brokenFatal) FailStage("Сломанный мостик установлен в разрыв");
                    return;
                }

                if (_slottedPlankB == plank) _slottedPlankB = null;
                _slottedPlankA = plank;
                plank.transform.SetParent(rootRt, true);
                plank.RectTransform.anchoredPosition = _slotA.anchoredPosition;
                UpdateStateUI();
                return;
            }

            // Проверяем попадание в слот B
            if (distB <= _snapRadius)
            {
                if (plank.IsBroken)
                {
                    ShowWarning("Сломанный мостик [X] рухнет под весом бота! Используйте целый мост.");
                    plank.ReturnToOrigin();
                    if (brokenFatal) FailStage("Сломанный мостик установлен в разрыв");
                    return;
                }

                if (_slottedPlankA == plank) _slottedPlankA = null;
                _slottedPlankB = plank;
                plank.transform.SetParent(rootRt, true);
                plank.RectTransform.anchoredPosition = _slotB.anchoredPosition;
                UpdateStateUI();
                return;
            }

            // Если брошен мимо слотов — освобождаем слот и возвращаем на склад
            if (_slottedPlankA == plank) _slottedPlankA = null;
            if (_slottedPlankB == plank) _slottedPlankB = null;
            plank.ReturnToOrigin();
            UpdateStateUI();
        }

        private void UpdateStateUI()
        {
            int bridgesPlaced = (_slottedPlankA != null ? 1 : 0) + (_slottedPlankB != null ? 1 : 0);
            float progress = bridgesPlaced * 0.35f;
            SetProgress(progress);

            if (_progressFill != null)
            {
                _progressFill.fillAmount = progress;
            }

            bool ready = _slottedPlankA != null && _slottedPlankB != null;
            if (_btnStart != null)
            {
                _btnStart.interactable = ready && !_isWalking;
                Image btnImg = _btnStart.GetComponent<Image>();
                if (btnImg != null)
                {
                    btnImg.color = ready ? new Color(0f, 0.85f, 0.45f, 1f) : new Color(0.2f, 0.28f, 0.38f, 0.85f);
                }
            }

            if (_instructionText != null && !_isWalking && !_isCompleted)
            {
                string timeTail = _timeLeft > 0f ? $" Осталось {_timeLeft:F0} сек." : "";
                if (!ready)
                {
                    _instructionText.text = $"Этап {CurrentStage}/3. Установите оба мостика в разрывы путей ({bridgesPlaced}/2). Остерегайтесь сломанного [X]!{timeTail}";
                }
                else
                {
                    _instructionText.text = $"<color=#00FF99>Мостики установлены! Нажмите 'ПУСК >>' (или пробел).</color>{timeTail}";
                }
            }
        }

        private void ShowWarning(string msg)
        {
            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#FF4444>{msg}</color>";
            }
        }

        private void OnStartClicked()
        {
            if (_isWalking || _isCompleted) return;
            if (_slottedPlankA == null || _slottedPlankB == null) return;

            StartCoroutine(WalkEntitiesRoutine());
        }

        private IEnumerator WalkEntitiesRoutine()
        {
            _isWalking = true;
            if (_btnStart != null) _btnStart.interactable = false;

            Vector2 targetA = _goalA != null ? _goalA.anchoredPosition : _startPosA + new Vector2(430f, 0f);
            Vector2 targetB = _goalB != null ? _goalB.anchoredPosition : _startPosB + new Vector2(430f, 0f);

            float duration = 2.4f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Плавная анимация шагов (покачивание вверх-вниз)
                float stepBobA = Mathf.Sin(t * Mathf.PI * 10f) * 5f;
                float stepBobB = Mathf.Cos(t * Mathf.PI * 10f) * 5f;

                if (_entityA != null)
                {
                    Vector2 currentPos = Vector2.Lerp(_startPosA, targetA, t);
                    currentPos.y += stepBobA;
                    _entityA.anchoredPosition = currentPos;
                }

                if (_entityB != null)
                {
                    Vector2 currentPos = Vector2.Lerp(_startPosB, targetB, t);
                    currentPos.y += stepBobB;
                    _entityB.anchoredPosition = currentPos;
                }

                float totalProgress = 0.7f + (t * 0.3f);
                SetProgress(totalProgress);
                if (_progressFill != null) _progressFill.fillAmount = totalProgress;

                yield return null;
            }

            if (_entityA != null) _entityA.anchoredPosition = targetA;
            if (_entityB != null) _entityB.anchoredPosition = targetB;

            _isWalking = false;
            SetProgress(1f);
            if (_progressFill != null) _progressFill.fillAmount = 1f;

            CompleteMechanic();

            if (_instructionText != null)
            {
                _instructionText.text = "<color=#00FF99>УСПЕХ! Оба робота благополучно пересекли мосты и достигли финиша!</color>";
            }
        }
    }
}
