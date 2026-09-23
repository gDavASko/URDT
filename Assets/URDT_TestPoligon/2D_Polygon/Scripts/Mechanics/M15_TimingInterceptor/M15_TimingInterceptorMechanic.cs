using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M15_TimingInterceptor
{
    /// <summary>
    /// Механика #15: Перехват объекта во временном окне допуска (Timing Interceptor).
    /// Три этапа: сужение окна, ускорение подачи, рост требуемой серии. Промах, попытка отбить
    /// фантомный мяч [X] и «упущенный» мяч — провал этапа.
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

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _streakText = null;
        [SerializeField] private TMP_Text _instructionText = null;
        [SerializeField] private Image _ballImage = null;

        // Допуск, требуемая серия и диапазон скорости по этапам.
        private static readonly float[] _stageTolerance      = { 30f, 22f, 16f };
        private static readonly int[]   _stageRequiredStreak = { 3,   3,   4   };
        private static readonly float[] _stageSpeedMin       = { 170f, 210f, 235f };
        private static readonly float[] _stageSpeedMax       = { 230f, 260f, 285f };
        private static readonly float[] _stageDecoyChance    = { 0.28f, 0.32f, 0.35f };

        private int _currentStreak = 0;
        private float _ballSpeed = 190f;
        private float _ballX = 220f;
        private bool _isDecoy = false;
        private bool _isDeflected = false;
        private float _deflectTimer = 0f;

        public override int StageCount => 3;
        public int CurrentStreak => _currentStreak;
        public int RequiredStreak => _stageRequiredStreak[Mathf.Clamp(CurrentStage - 1, 0, _stageRequiredStreak.Length - 1)];
        public float Tolerance => _stageTolerance[Mathf.Clamp(CurrentStage - 1, 0, _stageTolerance.Length - 1)];
        public bool IsDecoy => _isDecoy;
        public float BallX => _ballX;
        public float TargetLineX => _targetLineX;

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

        protected override string GetStageInstruction(int stage)
        {
            float tol = _stageTolerance[Mathf.Clamp(stage - 1, 0, _stageTolerance.Length - 1)];
            int need = _stageRequiredStreak[Mathf.Clamp(stage - 1, 0, _stageRequiredStreak.Length - 1)];
            switch (stage)
            {
                case 1: return $"Этап 1/3: отбейте мяч у линии ±{tol:F0}px, серия {need} подряд. Не бейте фантом [X]!";
                case 2: return $"Этап 2/3: подача быстрее, допуск ±{tol:F0}px, серия {need}.";
                default: return $"Этап 3/3: молниеносная подача, ±{tol:F0}px, серия {need}. Не ошибиться!";
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _currentStreak = 0;
            ResetBallCycle();
            if (_instructionText != null) _instructionText.text = GetStageInstruction(CurrentStage);
            UpdateUI();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        public void AttemptInterception()
        {
            if (_isCompleted || IsInTransition || _isDeflected) return;

            if (_hitPaddle != null) _hitPaddle.localRotation = Quaternion.Euler(0f, 0f, 35f);

            if (_isDecoy)
            {
                FailStage("Ошибка: попытка отбить ложный мяч-фантом [X].");
                return;
            }

            float offset = Mathf.Abs(_ballX - _targetLineX);
            float tol = Tolerance;
            if (offset <= tol)
            {
                _isDeflected = true;
                _deflectTimer = 0f;
                _currentStreak++;
                UpdateUI();

                int need = RequiredStreak;
                float progress = Mathf.Clamp01((float)_currentStreak / need);
                SetProgress(progress);

                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Точный перехват! Серия: {_currentStreak}/{need}</color>";
                }

                if (_currentStreak >= need)
                {
                    CompleteMechanic();
                    if (_instructionText != null)
                    {
                        _instructionText.text = $"<color=#00FF99>Этап {CurrentStage}/3 пройден идеально!</color>";
                    }
                }
            }
            else
            {
                string msg = _ballX > _targetLineX ? "Слишком рано!" : "Слишком поздно!";
                FailStage($"Промах: {msg} (сдвиг {offset:F0}px, допуск {tol:F0}px).");
            }
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;

            if (_hitPaddle != null && _hitPaddle.localRotation != Quaternion.identity)
            {
                _hitPaddle.localRotation = Quaternion.Lerp(_hitPaddle.localRotation, Quaternion.identity, Time.unscaledDeltaTime * 10f);
            }

            if (_isDeflected)
            {
                _deflectTimer += Time.unscaledDeltaTime;
                _ballX += _ballSpeed * 2f * Time.unscaledDeltaTime;
                if (_ballTransform != null) _ballTransform.anchoredPosition = new Vector2(_ballX, 0f);
                if (_ballX > 250f || _deflectTimer > 1.2f) ResetBallCycle();
                return;
            }

            _ballX -= _ballSpeed * Time.unscaledDeltaTime;
            if (_ballTransform != null) _ballTransform.anchoredPosition = new Vector2(_ballX, 0f);

            if (_ballX < -190f)
            {
                if (_isDecoy)
                {
                    // Фантом прошёл мимо — это правильно, просто следующая подача.
                    ResetBallCycle();
                }
                else
                {
                    FailStage("Мяч упущен за пределы линии перехвата.");
                }
            }
        }

        private void ResetBallCycle()
        {
            _ballX = 220f;
            _isDeflected = false;
            _deflectTimer = 0f;

            float decoyChance = _stageDecoyChance[Mathf.Clamp(CurrentStage - 1, 0, _stageDecoyChance.Length - 1)];
            _isDecoy = UnityEngine.Random.value < decoyChance;

            float sMin = _stageSpeedMin[Mathf.Clamp(CurrentStage - 1, 0, _stageSpeedMin.Length - 1)];
            float sMax = _stageSpeedMax[Mathf.Clamp(CurrentStage - 1, 0, _stageSpeedMax.Length - 1)];
            _ballSpeed = UnityEngine.Random.Range(sMin, sMax);

            if (_ballImage != null)
            {
                _ballImage.color = _isDecoy ? new Color(1f, 0.3f, 0.3f, 0.7f) : Color.white;
            }

            if (_ballTransform != null) _ballTransform.anchoredPosition = new Vector2(_ballX, 0f);
        }

        private void UpdateUI()
        {
            if (_streakText != null)
            {
                _streakText.text = $"Серия перехватов: {_currentStreak} / {RequiredStreak}";
            }
        }
    }
}
