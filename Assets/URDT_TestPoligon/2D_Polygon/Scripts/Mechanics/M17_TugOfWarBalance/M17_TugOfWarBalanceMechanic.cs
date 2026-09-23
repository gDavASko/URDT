using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M17_TugOfWarBalance
{
    /// <summary>
    /// Механика #17: Аккумулятор интенсивности ввода с диссипацией (Tug-of-War Balance).
    /// Задача: частыми тапами перетягивать канат на свою сторону (зелёная зона победы), преодолевая постоянное затухание/натяжение.
    /// Мешающие факторы:
    /// 1. Скользкий узел / ложная кнопка [X] — при нажатии канат резко проскальзывает назад.
    /// 2. Постоянное сопротивление соперника (-0.45/сек).
    /// </summary>
    public class M17_TugOfWarBalanceMechanic : BaseMechanic2DModule
    {
        [Header("Интерактивные кнопки")]
        [SerializeField] private Button _btnPull = null;
        [SerializeField] private Button _btnSlipHazard = null;

        [Header("Визуализация каната")]
        [SerializeField] private RectTransform _ropeMarker = null;
        [SerializeField] private RectTransform _ropeVisual = null;
        [SerializeField] private Image _balanceFill = null;

        [Header("Параметры перетягивания")]
        [SerializeField] private float _tapPower = 0.085f;
        [SerializeField] private float _decayRate = 0.38f;
        [SerializeField] private float _winThreshold = 0.85f;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _balanceText = null;
        [SerializeField] private TMP_Text _instructionText = null;

        // Параметры по этапам: тяга соперника, порог победы, штраф за скользкий узел, сила рывка.
        private static readonly float[] StageDecay = new float[] { 0.38f, 0.48f, 0.58f };
        private static readonly float[] StageThreshold = new float[] { 0.85f, 0.88f, 0.90f };
        private static readonly float[] StageSlipPenalty = new float[] { 0.20f, 0.30f, 0.40f };
        private static readonly float[] StageTapPower = new float[] { 0.085f, 0.100f, 0.115f };
        private const float LoseThreshold = -0.85f;

        private float _balance = 0f; // Диапазон от -1.0f (соперник) до +1.0f (игрок)
        private int _slipHits = 0;

        public override int StageCount => 3;
        public int SlipHits => _slipHits;
        public float CurrentBalance => _balance;
        public float CurrentWinThreshold => _winThreshold;

        protected override void Awake()
        {
            base.Awake();
            if (_btnPull != null) _btnPull.onClick.AddListener(OnPullTapped);
            if (_btnSlipHazard != null) _btnSlipHazard.onClick.AddListener(OnSlipHazardTapped);
        }

        private void OnDestroy()
        {
            if (_btnPull != null) _btnPull.onClick.RemoveListener(OnPullTapped);
            if (_btnSlipHazard != null) _btnSlipHazard.onClick.RemoveListener(OnSlipHazardTapped);
        }

        public override void Initialize()
        {
            base.Initialize();
            _balance = 0f;
            _slipHits = 0;
            int idx = Mathf.Clamp(CurrentStage - 1, 0, StageDecay.Length - 1);
            _decayRate = StageDecay[idx];
            _winThreshold = StageThreshold[idx];
            _tapPower = StageTapPower[idx];
            UpdateVisuals();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        protected override string GetStageInstruction(int stage)
        {
            switch (stage)
            {
                case 1: return "Этап 1/3. Быстрыми тапами перетягивайте канат в свою (правую) зону. Избегайте кнопки [X] — она возвращает канат назад.";
                case 2: return "Этап 2/3. Соперник упрямее: тяга соперника выросла, а порог победы поднят. Не касайтесь [X]!";
                case 3: return "Этап 3/3. Максимальное сопротивление. Любой промах по узлу [X] очень болезнен. Не дайте сопернику утащить канат за красную черту.";
                default: return _instruction;
            }
        }

        public void OnPullTapped()
        {
            if (_isCompleted || IsInTransition) return;

            _balance = Mathf.Clamp(_balance + _tapPower, -1f, 1f);
            UpdateVisuals();

            if (_balance >= _winThreshold)
            {
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#00FF99>Этап пройден! Канат в вашей зоне!</color>";
                }
                CompleteMechanic();
                SetProgress(1f);
            }
        }

        public void OnSlipHazardTapped()
        {
            if (_isCompleted || IsInTransition) return;

            _slipHits++;
            int idx = Mathf.Clamp(CurrentStage - 1, 0, StageSlipPenalty.Length - 1);
            _balance = Mathf.Clamp(_balance - StageSlipPenalty[idx], -1f, 1f);
            UpdateVisuals();

            if (_instructionText != null)
            {
                _instructionText.text = "<color=#FF4444>Проскальзывание [X]! Скользкий узел сдернул канат назад!</color>";
            }

            // На последнем этапе три касания скользкого узла — верный провал
            if (CurrentStage >= 3 && _slipHits >= 3)
            {
                FailStage("три касания скользкого узла [X]");
            }
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;

            // Постоянное затухание (тяга соперника)
            if (_balance > -1f)
            {
                _balance -= _decayRate * Time.unscaledDeltaTime;
                _balance = Mathf.Clamp(_balance, -1f, 1f);
                UpdateVisuals();
            }

            // Клавиатурная поддержка пробела
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                OnPullTapped();
            }

            // Прогресс для системы хоста
            float normalizedProgress = Mathf.Clamp01((_balance - (-1f)) / (_winThreshold - (-1f)));
            SetProgress(normalizedProgress);

            // Провал: соперник утащил канат в свою зону
            if (_balance <= LoseThreshold)
            {
                FailStage("соперник перетянул канат в свою зону");
            }
        }

        private void UpdateVisuals()
        {
            if (_ropeVisual != null)
            {
                _ropeVisual.anchoredPosition = new Vector2(_balance * 20f, 40f);
            }

            if (_ropeMarker != null)
            {
                _ropeMarker.anchoredPosition = new Vector2(_balance * 160f, 0f);
            }

            if (_balanceFill != null)
            {
                _balanceFill.fillAmount = Mathf.Clamp01((_balance + 1f) * 0.5f);
            }

            if (_balanceText != null)
            {
                int percent = Mathf.RoundToInt((_balance + 1f) * 50f);
                _balanceText.text = $"Баланс сил: {percent}% (Цель: 92%)";
            }
        }
    }
}
