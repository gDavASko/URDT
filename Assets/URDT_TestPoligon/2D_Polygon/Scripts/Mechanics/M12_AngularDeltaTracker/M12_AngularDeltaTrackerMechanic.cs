using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M12_AngularDeltaTracker
{
    /// <summary>
    /// Механика #12: Угловой энкодер / Поворот вокруг оси (Angular Delta Tracker).
    /// Задача: повернуть рабочий вентиль на 720 градусов (2 полных оборота), чтобы открыть подачу воды.
    /// Мешающие факторы:
    /// 1. Заклинивший ржавый вентиль с пометкой [X] — при попытке крутить заклинивает и выводит предупреждение.
    /// 2. Инерция и направление вращения (учитывается абсолютное накопление оборотов).
    /// </summary>
    public class M12_AngularDeltaTrackerMechanic : BaseMechanic2DModule
    {
        [Header("Компоненты вентилей")]
        [SerializeField] private RotaryWheel _activeWheel = null;
        [SerializeField] private RotaryWheel _jammedWheel = null;

        [Header("Параметры вращения")]
        [SerializeField] private float _targetDegrees = 720f; // 2 оборота

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _turnsDisplay = null;
        [SerializeField] private Image _progressFill = null;
        [SerializeField] private TMP_Text _instructionText = null;

        private float _currentDegrees = 0f;

        protected override void Awake()
        {
            base.Awake();
            if (_activeWheel != null)
            {
                _activeWheel.OnDeltaRotated += HandleWheelDelta;
            }
            if (_jammedWheel != null)
            {
                _jammedWheel.OnJammedAttempt += HandleJammedAttempt;
            }
        }

        private void OnDestroy()
        {
            if (_activeWheel != null)
            {
                _activeWheel.OnDeltaRotated -= HandleWheelDelta;
            }
            if (_jammedWheel != null)
            {
                _jammedWheel.OnJammedAttempt -= HandleJammedAttempt;
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _currentDegrees = 0f;

            if (_activeWheel != null) _activeWheel.ResetWheel();
            if (_jammedWheel != null) _jammedWheel.ResetWheel();

            UpdateUI();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void HandleWheelDelta(float delta)
        {
            if (_isCompleted) return;

            _currentDegrees += Mathf.Abs(delta);
            float progress = Mathf.Clamp01(_currentDegrees / _targetDegrees);
            SetProgress(progress);
            UpdateUI();

            if (_currentDegrees >= _targetDegrees)
            {
                CompleteMechanic();
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#00FF99>Вентиль полностью открыт! Трубопровод функционирует.</color>";
                }
            }
        }

        private void HandleJammedAttempt()
        {
            if (_instructionText != null)
            {
                _instructionText.text = "<color=#FF5555>Ошибка! Вентиль справа заклинило ржавчиной [X]! Крутите исправный вентиль слева.</color>";
            }
        }

        private void UpdateUI()
        {
            float currentTurns = _currentDegrees / 360f;
            float targetTurns = _targetDegrees / 360f;

            if (_turnsDisplay != null)
            {
                _turnsDisplay.text = $"Обороты: {currentTurns:F1} / {targetTurns:F1} об.";
            }

            if (_progressFill != null)
            {
                _progressFill.fillAmount = Mathf.Clamp01(_currentDegrees / _targetDegrees);
            }
        }
    }
}
