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

        private float _balance = 0f; // Диапазон от -1.0f (соперник) до +1.0f (игрок)

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
            UpdateVisuals();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        public void OnPullTapped()
        {
            if (_isCompleted) return;

            _balance = Mathf.Clamp(_balance + _tapPower, -1f, 1f);
            UpdateVisuals();

            if (_balance >= _winThreshold)
            {
                CompleteMechanic();
                SetProgress(1f);
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#00FF99>Победа! Канат полностью перетянут в вашу зону!</color>";
                }
            }
        }

        public void OnSlipHazardTapped()
        {
            if (_isCompleted) return;

            // Штрафной проскольз назад
            _balance = Mathf.Clamp(_balance - 0.25f, -1f, 1f);
            UpdateVisuals();

            if (_instructionText != null)
            {
                _instructionText.text = "<color=#FF4444>Проскальзывание [X]! Скользкий узел сдернул канат назад!</color>";
            }
        }

        private void Update()
        {
            if (_isCompleted) return;

            // Постоянное затухание (тяга соперника)
            if (_balance > -0.95f)
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
