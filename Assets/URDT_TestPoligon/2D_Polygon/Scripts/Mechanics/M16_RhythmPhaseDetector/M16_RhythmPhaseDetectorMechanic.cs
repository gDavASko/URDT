using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M16_RhythmPhaseDetector
{
    /// <summary>
    /// Механика #16: Синхронизация поочередных фаз движения (Rhythm Phase Detector).
    /// Задача: ритмично поочередно нажимать Левый и Правый рычаги (темп 0.3-1.1 сек), чтобы заполнить бак до 100%.
    /// Мешающие факторы:
    /// 1. Неисправный средний рычаг [X] — нажатие сбивает ритм и вычитает прогресс.
    /// 2. Нарушение темпа (слишком быстрое или медленное нажатие) сбрасывает комбо.
    /// </summary>
    public class M16_RhythmPhaseDetectorMechanic : BaseMechanic2DModule
    {
        [Header("Рычаги управления")]
        [SerializeField] private Button _btnLeftLever = null;
        [SerializeField] private Button _btnRightLever = null;
        [SerializeField] private Button _btnJunkCenterLever = null;

        [Header("Ритмические параметры")]
        [SerializeField] private float _minInterval = 0.25f;
        [SerializeField] private float _maxInterval = 1.15f;
        [SerializeField] private int _targetBeats = 8;

        [Header("UI визуализация")]
        [SerializeField] private Image _tankFill = null;
        [SerializeField] private TMP_Text _beatsText = null;
        [SerializeField] private TMP_Text _instructionText = null;
        [SerializeField] private RectTransform _metronomeTick = null;

        private int _successfulBeats = 0;
        private int _lastPressedSide = 0; // 1 = Left, 2 = Right
        private float _lastPressTime = -1f;

        protected override void Awake()
        {
            base.Awake();
            if (_btnLeftLever != null) _btnLeftLever.onClick.AddListener(() => OnLeverPressed(1));
            if (_btnRightLever != null) _btnRightLever.onClick.AddListener(() => OnLeverPressed(2));
            if (_btnJunkCenterLever != null) _btnJunkCenterLever.onClick.AddListener(OnJunkLeverPressed);
        }

        private void OnDestroy()
        {
            if (_btnLeftLever != null) _btnLeftLever.onClick.RemoveAllListeners();
            if (_btnRightLever != null) _btnRightLever.onClick.RemoveAllListeners();
            if (_btnJunkCenterLever != null) _btnJunkCenterLever.onClick.RemoveAllListeners();
        }

        public override void Initialize()
        {
            base.Initialize();
            _successfulBeats = 0;
            _lastPressedSide = 0;
            _lastPressTime = -1f;

            UpdateUI();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void OnLeverPressed(int side)
        {
            if (_isCompleted) return;

            float now = Time.unscaledTime;

            // Первая фаза
            if (_lastPressedSide == 0)
            {
                _lastPressedSide = side;
                _lastPressTime = now;
                _successfulBeats = 1;
                AnimateLever(side);
                UpdateUI();
                if (_instructionText != null)
                {
                    _instructionText.text = "Ритм начат! Теперь нажмите противоположный рычаг в темпе.";
                }
                return;
            }

            // Проверка чередования (Левый -> Правый -> Левый)
            if (side == _lastPressedSide)
            {
                // Ошибка: два раза подряд одна сторона
                _lastPressedSide = side;
                _lastPressTime = now;
                if (_successfulBeats > 0) _successfulBeats--;
                UpdateUI();
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#FF5555>Ошибка чередования! Нажимайте поочередно: ЛЕВЫЙ -> ПРАВЫЙ!</color>";
                }
                return;
            }

            // Проверка интервала темпа
            float interval = now - _lastPressTime;
            _lastPressTime = now;
            _lastPressedSide = side;
            AnimateLever(side);

            if (interval >= _minInterval && interval <= _maxInterval)
            {
                // Попадание в ритм!
                _successfulBeats++;
                UpdateUI();

                float progress = Mathf.Clamp01((float)_successfulBeats / _targetBeats);
                SetProgress(progress);

                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Отличный темп! ({interval:F2}с). Серия: {_successfulBeats}/{_targetBeats}</color>";
                }

                if (_successfulBeats >= _targetBeats)
                {
                    CompleteMechanic();
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#00FF99>Резервуар полностью заполнен! Ритм выдержан безупречно!</color>";
                    }
                }
            }
            else
            {
                // Слишком быстро или слишком медленно
                string reason = interval < _minInterval ? "Слишком быстро!" : "Слишком медленно!";
                if (_successfulBeats > 0) _successfulBeats--;
                UpdateUI();
                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#FF8844>{reason} Держите ровный размеренный темп.</color>";
                }
            }
        }

        private void OnJunkLeverPressed()
        {
            if (_isCompleted) return;

            _lastPressedSide = 0;
            _lastPressTime = -1f;
            if (_successfulBeats > 1) _successfulBeats -= 2;
            else _successfulBeats = 0;

            UpdateUI();
            SetProgress((float)_successfulBeats / _targetBeats);

            if (_instructionText != null)
            {
                _instructionText.text = "<color=#FF3333>Авария! Средний рычаг сломан [X]! Сброс ритмической цепи.</color>";
            }
        }

        private void AnimateLever(int side)
        {
            if (_metronomeTick != null)
            {
                _metronomeTick.localRotation = Quaternion.Euler(0f, 0f, side == 1 ? -25f : 25f);
            }
        }

        private void Update()
        {
            if (_isCompleted) return;

            // Возврат маятника в нейтраль
            if (_metronomeTick != null && _metronomeTick.localRotation != Quaternion.identity)
            {
                _metronomeTick.localRotation = Quaternion.Lerp(_metronomeTick.localRotation, Quaternion.identity, Time.unscaledDeltaTime * 6f);
            }
        }

        private void UpdateUI()
        {
            if (_tankFill != null)
            {
                _tankFill.fillAmount = Mathf.Clamp01((float)_successfulBeats / _targetBeats);
            }

            if (_beatsText != null)
            {
                _beatsText.text = $"Ритм-такт: {_successfulBeats} / {_targetBeats}";
            }
        }
    }
}
