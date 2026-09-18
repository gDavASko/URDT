using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M15_TimingInterceptor
{
    /// <summary>
    /// Механика #15: Перехват объекта во временном окне допуска (Timing Interceptor).
    /// Задача: отбить приближающийся мяч ровно в момент его нахождения в зеленой зоне перехвата (3 раза подряд).
    /// Мешающие факторы:
    /// 1. Ложный фантомный мяч [X] красного цвета — при попытке отбить его серия сбрасывается.
    /// 2. Узкое окно допуска (допуск по координате +/- 25px) и переменная скорость подачи.
    /// </summary>
    public class M15_TimingInterceptorMechanic : BaseMechanic2DModule
    {
        [Header("Визуальные элементы")]
        [SerializeField] private RectTransform _ballTransform = null;
        [SerializeField] private RectTransform _interceptorLine = null;
        [SerializeField] private RectTransform _hitPaddle = null;
        [SerializeField] private Button _interceptButton = null;

        [Header("Параметры перехвата")]
        [SerializeField] private float _targetLineX = -100f;
        [SerializeField] private float _tolerance = 30f;
        [SerializeField] private int _requiredStreak = 3;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _streakText = null;
        [SerializeField] private TMP_Text _instructionText = null;
        [SerializeField] private Image _ballImage = null;

        private int _currentStreak = 0;
        private float _ballSpeed = 190f;
        private float _ballX = 220f;
        private bool _isDecoy = false;
        private bool _isDeflected = false;
        private float _deflectTimer = 0f;

        protected override void Awake()
        {
            if (_interceptorLine != null) _targetLineX = _interceptorLine.anchoredPosition.x;
            base.Awake();
            if (_interceptButton != null) _interceptButton.onClick.AddListener(AttemptInterception);
        }

        private void OnDestroy()
        {
            if (_interceptButton != null) _interceptButton.onClick.RemoveListener(AttemptInterception);
        }

        public override void Initialize()
        {
            base.Initialize();
            _currentStreak = 0;
            ResetBallCycle();
            UpdateUI();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        public void AttemptInterception()
        {
            if (_isCompleted || _isDeflected) return;

            // Анимация взмаха ракетки
            if (_hitPaddle != null)
            {
                _hitPaddle.localRotation = Quaternion.Euler(0f, 0f, 35f);
            }

            float offset = Mathf.Abs(_ballX - _targetLineX);

            if (_isDecoy)
            {
                // Попытка отбить фантомный мяч [X]
                _currentStreak = 0;
                UpdateUI();
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#FF4444>Ошибка! Это был ложный мяч-фантом [X]! Серия сброшена.</color>";
                }
                ResetBallCycle();
                return;
            }

            if (offset <= _tolerance)
            {
                // Успешный перехват
                _isDeflected = true;
                _deflectTimer = 0f;
                _currentStreak++;
                UpdateUI();

                float progress = Mathf.Clamp01((float)_currentStreak / _requiredStreak);
                SetProgress(progress);

                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Точный перехват! Серия: {_currentStreak}/{_requiredStreak}</color>";
                }

                if (_currentStreak >= _requiredStreak)
                {
                    CompleteMechanic();
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#00FF99>Идеальный тайминг! 3 перехвата подряд выполнены!</color>";
                    }
                }
            }
            else
            {
                // Промах
                _currentStreak = 0;
                UpdateUI();
                if (_instructionText != null)
                {
                    string msg = _ballX > _targetLineX ? "Слишком рано!" : "Слишком поздно!";
                    _instructionText.text = $"<color=#FF8844>{msg} Серия сброшена.</color>";
                }
            }
        }

        private void Update()
        {
            if (_isCompleted) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space)) AttemptInterception();

            // Возврат ракетки в исходное положение
            if (_hitPaddle != null && _hitPaddle.localRotation != Quaternion.identity)
            {
                _hitPaddle.localRotation = Quaternion.Lerp(_hitPaddle.localRotation, Quaternion.identity, Time.unscaledDeltaTime * 10f);
            }

            if (_isDeflected)
            {
                _deflectTimer += Time.unscaledDeltaTime;
                _ballX += _ballSpeed * 2f * Time.unscaledDeltaTime;
                if (_ballTransform != null) _ballTransform.anchoredPosition = new Vector2(_ballX, 0f);

                if (_ballX > 250f || _deflectTimer > 1.2f)
                {
                    ResetBallCycle();
                }
                return;
            }

            // Движение мяча влево
            _ballX -= _ballSpeed * Time.unscaledDeltaTime;
            if (_ballTransform != null) _ballTransform.anchoredPosition = new Vector2(_ballX, 0f);

            // Мяч пролетел мимо линии без перехвата
            if (_ballX < -190f)
            {
                if (!_isDecoy && _currentStreak > 0)
                {
                    _currentStreak = 0;
                    UpdateUI();
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF5555>Мяч упущен за пределы! Серия сброшена.</color>";
                    }
                }
                ResetBallCycle();
            }
        }

        private void ResetBallCycle()
        {
            _ballX = 220f;
            _isDeflected = false;
            _deflectTimer = 0f;

            // С вероятностью 25% спавним фантомный мяч-обманку
            _isDecoy = UnityEngine.Random.value < 0.28f;

            _ballSpeed = UnityEngine.Random.Range(170f, 230f);

            if (_ballImage != null)
            {
                _ballImage.color = _isDecoy ? new Color(1f, 0.3f, 0.3f, 0.7f) : Color.white;
            }

            if (_ballTransform != null)
            {
                _ballTransform.anchoredPosition = new Vector2(_ballX, 0f);
            }
        }

        private void UpdateUI()
        {
            if (_streakText != null)
            {
                _streakText.text = $"Серия перехватов: {_currentStreak} / {_requiredStreak}";
            }
        }
    }
}
