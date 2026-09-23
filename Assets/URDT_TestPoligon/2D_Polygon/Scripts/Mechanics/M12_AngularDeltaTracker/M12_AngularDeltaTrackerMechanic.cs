using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M12_AngularDeltaTracker
{
    /// <summary>
    /// Механика #12: Угловой энкодер / Поворот вокруг оси (Angular Delta Tracker).
    /// Три этапа: 2, 3 и 4 оборота исправного вентиля. На поздних этапах добавляется лимит
    /// времени, а упорные попытки открутить заклинивший вентиль срывают этап.
    /// </summary>
    public class M12_AngularDeltaTrackerMechanic : BaseMechanic2DModule
    {
        [Header("Компоненты вентилей")]
        [SerializeField] private RotaryWheel _activeWheel = null;
        [SerializeField] private RotaryWheel _jammedWheel = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _turnsDisplay = null;
        [SerializeField] private Image _progressFill = null;
        [SerializeField] private TMP_Text _instructionText = null;

        private static readonly float[] _stageTargetDegrees = { 720f, 1080f, 1440f };
        private static readonly float[] _stageTimeLimit    = { 0f,   30f,   28f };
        private static readonly int[]   _stageJamTolerance = { 5,    3,     2 };

        private float _currentDegrees = 0f;
        private float _timeRemaining = 0f;
        private bool _hasTimeLimit = false;
        private int _jamAttempts = 0;

        public override int StageCount => 3;

        public float AccumulatedDegrees => _currentDegrees;
        public float TargetDegrees => _stageTargetDegrees[Mathf.Clamp(CurrentStage - 1, 0, _stageTargetDegrees.Length - 1)];
        public float TimeRemaining => _timeRemaining;
        public bool HasTimeLimit => _hasTimeLimit;
        public int JamAttempts => _jamAttempts;
        public int JamAttemptsAllowed => _stageJamTolerance[Mathf.Clamp(CurrentStage - 1, 0, _stageJamTolerance.Length - 1)];

        protected override void Awake()
        {
            base.Awake();
            if (_activeWheel != null) _activeWheel.OnDeltaRotated += HandleWheelDelta;
            if (_jammedWheel != null) _jammedWheel.OnJammedAttempt += HandleJammedAttempt;
        }

        private void OnDestroy()
        {
            if (_activeWheel != null) _activeWheel.OnDeltaRotated -= HandleWheelDelta;
            if (_jammedWheel != null) _jammedWheel.OnJammedAttempt -= HandleJammedAttempt;
        }

        protected override string GetStageInstruction(int stage)
        {
            float turns = _stageTargetDegrees[Mathf.Clamp(stage - 1, 0, _stageTargetDegrees.Length - 1)] / 360f;
            switch (stage)
            {
                case 1: return $"Этап 1/3: поверните исправный вентиль на {turns:F0} оборота.";
                case 2: return $"Этап 2/3: {turns:F0} оборота за 30 сек. Не хватайтесь за ржавый [X]!";
                default: return $"Этап 3/3: {turns:F0} оборота за 28 сек. Экономьте попытки.";
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _currentDegrees = 0f;
            _jamAttempts = 0;

            if (_activeWheel != null) _activeWheel.ResetWheel();
            if (_jammedWheel != null) _jammedWheel.ResetWheel();

            int idx = Mathf.Clamp(CurrentStage - 1, 0, _stageTimeLimit.Length - 1);
            _hasTimeLimit = _stageTimeLimit[idx] > 0f;
            _timeRemaining = _stageTimeLimit[idx];

            if (_instructionText != null) _instructionText.text = GetStageInstruction(CurrentStage);
            UpdateUI();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;
            if (!_hasTimeLimit) return;
            _timeRemaining -= Time.unscaledDeltaTime;
            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                FailStage("Время истекло — вентиль не открыт полностью.");
            }
        }

        private void HandleWheelDelta(float delta)
        {
            if (_isCompleted || IsInTransition) return;

            _currentDegrees += Mathf.Abs(delta);
            float target = TargetDegrees;
            float progress = Mathf.Clamp01(_currentDegrees / target);
            SetProgress(progress);
            UpdateUI();

            if (_currentDegrees >= target)
            {
                CompleteMechanic();
                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Этап {CurrentStage}/3 пройден — вентиль открыт!</color>";
                }
            }
        }

        private void HandleJammedAttempt()
        {
            if (_isCompleted || IsInTransition) return;
            _jamAttempts++;
            int allowed = JamAttemptsAllowed;

            if (_jamAttempts >= allowed)
            {
                FailStage($"Сломали механизм: {_jamAttempts} попыток крутить ржавый вентиль [X].");
                return;
            }

            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#FF5555>Ржавый вентиль [X]! Попытка {_jamAttempts}/{allowed}. Крутите левый.</color>";
            }
        }

        private void UpdateUI()
        {
            float target = TargetDegrees;
            float currentTurns = _currentDegrees / 360f;
            float targetTurns = target / 360f;

            if (_turnsDisplay != null)
            {
                string timer = _hasTimeLimit ? $"  ⏱ {_timeRemaining:F0}с" : string.Empty;
                _turnsDisplay.text = $"Обороты: {currentTurns:F1} / {targetTurns:F1}{timer}";
            }

            if (_progressFill != null)
            {
                _progressFill.fillAmount = Mathf.Clamp01(_currentDegrees / target);
            }
        }
    }
}
