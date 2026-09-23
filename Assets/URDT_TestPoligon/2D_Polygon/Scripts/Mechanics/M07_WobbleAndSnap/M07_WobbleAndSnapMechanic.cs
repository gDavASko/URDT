using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M07_WobbleAndSnap
{
    /// <summary>
    /// Механика #7: Расшатывание связки и извлечение зуба (Wobble & Snap).
    /// Три этапа: связка всё крепче — раскачивать нужно дольше и точнее.
    /// Провал: попытка рвануть щипцы вверх до готовности связки.
    /// </summary>
    public class M07_WobbleAndSnapMechanic : BaseMechanic2DModule
    {
        [Header("Компоненты")]
        [SerializeField] private WobbleItem _wobbleItem = null;
        [SerializeField] private DentalForcepsTool _forcepsTool = null;
        [SerializeField] private Image _fatigueProgressBar = null;
        [SerializeField] private TMP_Text _fatigueLabel = null;
        [SerializeField] private TMP_Text _instructionText = null;

        [Header("Стадийная сложность")]
        [SerializeField] private float _baseFatigueRate = 1.4f;
        [SerializeField] private float _baseSnapThreshold = 80f;

        public override int StageCount => 3;
        public float CurrentFatigue => _wobbleItem != null ? _wobbleItem.Fatigue : 0f;

        protected override string GetStageInstruction(int stage)
        {
            switch (stage)
            {
                case 1: return "Этап 1/3. Наложите щипцы на коронку и раскачивайте связку до 100%. Не тяните преждевременно.";
                case 2: return "Этап 2/3. Связка крепче — потребуется больше движений. Рывок вверх до готовности сорвёт этап.";
                default: return "Этап 3/3. Самая упрямая связка. Работайте плавно и уверенно, только полностью ослабленный зуб поддастся.";
            }
        }

        protected override void Awake()
        {
            base.Awake();
            BindEvents();
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        private void BindEvents()
        {
            if (_wobbleItem != null)
            {
                _wobbleItem.OnFatigueChanged += HandleFatigueChanged;
                _wobbleItem.OnSnapped += HandleSnapped;
                _wobbleItem.OnPrematurePull += HandlePrematurePull;
            }

            if (_forcepsTool != null)
            {
                _forcepsTool.OnAttached += HandleForcepsAttached;
            }
        }

        private void UnbindEvents()
        {
            if (_wobbleItem != null)
            {
                _wobbleItem.OnFatigueChanged -= HandleFatigueChanged;
                _wobbleItem.OnSnapped -= HandleSnapped;
                _wobbleItem.OnPrematurePull -= HandlePrematurePull;
            }

            if (_forcepsTool != null)
            {
                _forcepsTool.OnAttached -= HandleForcepsAttached;
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            if (_wobbleItem != null)
            {
                // На поздних этапах связка крепче: медленный рост усталости, более высокий порог рывка.
                float rate = _baseFatigueRate;
                float thresh = _baseSnapThreshold;
                switch (CurrentStage)
                {
                    case 2: rate *= 0.7f; thresh *= 1.15f; break;
                    case 3: rate *= 0.5f; thresh *= 1.3f; break;
                }
                _wobbleItem.SetFatigueRate(rate);
                _wobbleItem.SetSnapReleaseThreshold(thresh);
                _wobbleItem.ResetItem();
            }

            if (_forcepsTool != null)
            {
                _forcepsTool.ResetTool();
            }

            if (_fatigueProgressBar != null) _fatigueProgressBar.fillAmount = 0f;
            if (_fatigueLabel != null) _fatigueLabel.text = "Усталость связки: 0%";

            if (_instructionText != null)
            {
                _instructionText.text = $"Этап {CurrentStage}/{StageCount}. Возьмите щипцы и наложите их на коронку зуба.";
            }

            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void HandleForcepsAttached()
        {
            SetProgress(0.15f);
            if (_instructionText != null && !_isCompleted)
            {
                _instructionText.text = "<color=#00DDFF>Щипцы зафиксированы!</color> Раскачивайте связку.";
            }
        }

        private void HandleFatigueChanged(float fatigue)
        {
            if (_fatigueProgressBar != null) _fatigueProgressBar.fillAmount = fatigue;
            if (_fatigueLabel != null) _fatigueLabel.text = $"Усталость связки: {(fatigue * 100f):F0}%";
            SetProgress(0.15f + fatigue * 0.7f);

            if (_instructionText != null && !_isCompleted)
            {
                if (fatigue >= 1f)
                {
                    _instructionText.text = "<color=#FFCC00>Связка ослаблена! Плавно потяните щипцы вверх.</color>";
                }
                else
                {
                    _instructionText.text = $"Раскачивайте щипцы: {(fatigue * 100f):F0}% / 100%";
                }
            }
        }

        private void HandleSnapped()
        {
            SetProgress(1f);
            CompleteMechanic();
            if (_instructionText != null && !IsInTransition)
            {
                _instructionText.text = "<color=#00FF99>Зуб успешно удалён!</color>";
            }
        }

        private void HandlePrematurePull()
        {
            if (IsInTransition || _isCompleted) return;
            if (_instructionText != null)
            {
                _instructionText.text = "<color=#FF5555>Слишком ранний рывок! Щипцы соскочили — этап начнётся заново.</color>";
            }
            FailStage("Преждевременный рывок вверх при неполной усталости связки");
        }
    }
}
