using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M21_ReactionProbe
{
    public enum FishingState
    {
        IdleWaiting,
        BiteTriggered,
        ResultDisplay
    }

    /// <summary>
    /// Механика #21: Реакция на фазовый триггер поклевки (Reaction Time Probe).
    /// Задача: вовремя нажать «ПОДСЕЧЬ» в момент поклевки (знак «!» и погружение поплавка).
    /// Мешающие факторы:
    /// 1. Ложные покачивания воды и мусорный сапог [X] в проруби.
    /// 2. Преждевременное нажатие распугивает рыбу, а задержка свыше 0.7с приводит к сходу.
    /// </summary>
    public class M21_ReactionProbeMechanic : BaseMechanic2DModule
    {
        [Header("Визуальные компоненты")]
        [SerializeField] private RectTransform _bobber = null;
        [SerializeField] private RectTransform _biteExclamation = null;
        [SerializeField] private RectTransform _junkBoot = null;
        [SerializeField] private Button _btnStrike = null;

        [Header("Параметры тайминга")]
        [SerializeField] private float _minWaitTime = 1.5f;
        [SerializeField] private float _maxWaitTime = 3.5f;
        [SerializeField] private float _reactionWindow = 0.75f;
        [SerializeField] private int _targetCatches = 2;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _scoreText = null;
        [SerializeField] private TMP_Text _instructionText = null;

        private FishingState _state = FishingState.IdleWaiting;
        private float _stateTimer = 0f;
        private int _successfulCatches = 0;
        private Vector2 _bobberIdlePos;
        private int _missesUsed = 0;

        private static readonly float[] StageWindow = new float[] { 0.75f, 0.55f, 0.40f };
        private static readonly int[] StageCatches = new int[] { 2, 3, 3 };
        private static readonly int[] StageMissBudget = new int[] { 3, 2, 1 };

        public override int StageCount => 3;
        public int MissesUsed => _missesUsed;
        public int MissesBudget => StageMissBudget[Mathf.Clamp(CurrentStage - 1, 0, StageMissBudget.Length - 1)];
        public int Catches => _successfulCatches;
        public int CatchesTarget => _targetCatches;

        protected override void Awake()
        {
            if (_bobber != null) _bobberIdlePos = _bobber.anchoredPosition;
            base.Awake();
            if (_btnStrike != null) _btnStrike.onClick.AddListener(AttemptStrike);
            if (_junkBoot != null && _junkBoot.TryGetComponent<Button>(out var bootBtn))
            {
                bootBtn.onClick.AddListener(() =>
                {
                    if (_instructionText != null)
                        _instructionText.text = "<color=#FF5555>Это старый дырявый сапог [X], а не рыба!</color>";
                });
            }
        }

        private void OnDestroy()
        {
            if (_btnStrike != null) _btnStrike.onClick.RemoveListener(AttemptStrike);
        }

        public override void Initialize()
        {
            base.Initialize();
            _successfulCatches = 0;
            _missesUsed = 0;

            int idx = Mathf.Clamp(CurrentStage - 1, 0, StageWindow.Length - 1);
            _reactionWindow = StageWindow[idx];
            _targetCatches = StageCatches[idx];

            StartWaitingCycle();
            UpdateUI();
            SetProgress(0f);
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, StageWindow.Length - 1);
            int budget = StageMissBudget[idx];
            switch (stage)
            {
                case 1: return $"Этап 1/3. Дождитесь поклёвки (знак «!») и жмите «ПОДСЕЧЬ» в течение {StageWindow[idx]:F2}с. Промахов допустимо: {budget}.";
                case 2: return $"Этап 2/3. Окно реакции {StageWindow[idx]:F2}с, поймать нужно {StageCatches[idx]}. Промахов: {budget}.";
                case 3: return $"Этап 3/3. Мастерская реакция: окно {StageWindow[idx]:F2}с. Любые {budget} промах(-а) — этап сброшен.";
                default: return _instruction;
            }
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void StartWaitingCycle()
        {
            _state = FishingState.IdleWaiting;
            _stateTimer = UnityEngine.Random.Range(_minWaitTime, _maxWaitTime);

            if (_biteExclamation != null) _biteExclamation.gameObject.SetActive(false);
            if (_bobber != null) _bobber.anchoredPosition = _bobberIdlePos;
            if (_instructionText != null)
            {
                _instructionText.text = "Внимание на поплавок... Ждите поклёвки (знак «!») и жмите «ПОДСЕЧЬ»!";
            }
        }

        public void AttemptStrike()
        {
            if (_isCompleted || IsInTransition) return;

            if (_state == FishingState.BiteTriggered)
            {
                // Успешная подсечка в окно реакции!
                _successfulCatches++;
                _state = FishingState.ResultDisplay;
                _stateTimer = 1.2f;

                if (_biteExclamation != null) _biteExclamation.gameObject.SetActive(false);
                if (_bobber != null) _bobber.anchoredPosition = _bobberIdlePos + new Vector2(0f, 40f);

                float progress = Mathf.Clamp01((float)_successfulCatches / _targetCatches);
                SetProgress(progress);
                UpdateUI();

                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Отличная реакция! Рыба поймана! ({_successfulCatches}/{_targetCatches})</color>";
                }

                if (_successfulCatches >= _targetCatches)
                {
                    CompleteMechanic();
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#00FF99>Улов готов! Все поклевки успешно реализованы!</color>";
                    }
                }
            }
            else if (_state == FishingState.IdleWaiting)
            {
                // Фальстарт!
                _state = FishingState.ResultDisplay;
                _stateTimer = 1.2f;
                RegisterMiss("фальстарт: подсечка до поклёвки");
            }
        }

        private void RegisterMiss(string reason)
        {
            _missesUsed++;
            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#FF5555>{reason}. Промахов: {_missesUsed}/{MissesBudget}</color>";
            }
            if (_missesUsed >= MissesBudget)
            {
                FailStage(reason);
            }
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space)) AttemptStrike();

            // Легкое покачивание поплавка на воде
            if (_state == FishingState.IdleWaiting && _bobber != null)
            {
                float floatOffset = Mathf.Sin(Time.unscaledTime * 4f) * 4f;
                _bobber.anchoredPosition = _bobberIdlePos + new Vector2(0f, floatOffset);
            }

            _stateTimer -= Time.unscaledDeltaTime;

            if (_state == FishingState.IdleWaiting)
            {
                if (_stateTimer <= 0f)
                {
                    // Поклевка!
                    _state = FishingState.BiteTriggered;
                    _stateTimer = _reactionWindow;

                    if (_biteExclamation != null) _biteExclamation.gameObject.SetActive(true);
                    if (_bobber != null) _bobber.anchoredPosition = _bobberIdlePos - new Vector2(0f, 25f);
                }
            }
            else if (_state == FishingState.BiteTriggered)
            {
                if (_stateTimer <= 0f)
                {
                    // Рыба ушла (истекло окно реакции)
                    _state = FishingState.ResultDisplay;
                    _stateTimer = 1.2f;

                    if (_biteExclamation != null) _biteExclamation.gameObject.SetActive(false);
                    if (_bobber != null) _bobber.anchoredPosition = _bobberIdlePos;

                    RegisterMiss("опоздали с подсечкой");
                }
            }
            else if (_state == FishingState.ResultDisplay)
            {
                if (_stateTimer <= 0f)
                {
                    StartWaitingCycle();
                }
            }
        }

        private void UpdateUI()
        {
            if (_scoreText != null)
            {
                _scoreText.text = $"Поймано: {_successfulCatches} / {_targetCatches}";
            }
        }
    }
}
