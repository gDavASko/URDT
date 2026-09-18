using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M07_WobbleAndSnap
{
    /// <summary>
    /// Механика #7: Преодоление сопротивления пружины с расшатыванием (Wobble & Snap).
    /// Задача: раскачивать объект из стороны в сторону, накапливая усталость связки (до 100%), затем потянуть вверх для извлечения.
    /// Мешающие факторы:
    /// 1. Сила упругости пружины, возвращающая объект в центр.
    /// 2. Ограничение на извлечение: вытянуть вверх можно только после полного расшатывания.
    /// </summary>
    public class M07_WobbleAndSnapMechanic : BaseMechanic2DModule
    {
        [Header("Компоненты")]
        [SerializeField] private WobbleItem _wobbleItem = null;
        [SerializeField] private DentalForcepsTool _forcepsTool = null;
        [SerializeField] private Image _fatigueProgressBar = null;
        [SerializeField] private TMP_Text _fatigueLabel = null;
        [SerializeField] private TMP_Text _instructionText = null;

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
                _wobbleItem.ResetItem();
            }

            if (_forcepsTool != null)
            {
                _forcepsTool.ResetTool();
            }

            if (_fatigueProgressBar != null)
            {
                _fatigueProgressBar.fillAmount = 0f;
            }

            if (_fatigueLabel != null)
            {
                _fatigueLabel.text = "Усталость связки: 0%";
            }

            if (_instructionText != null)
            {
                _instructionText.text = "Возьмите щипцы со столика справа и наложите их на коронку зуба!";
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
                _instructionText.text = "<color=#00DDFF>Щипцы зафиксированы!</color> Раскачивайте их влево-вправо, ослабляя связку!";
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
                    _instructionText.text = "<color=#FFCC00>Связка ослаблена! Потяните щипцы вверх для удаления!</color>";
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
            if (_instructionText != null)
            {
                _instructionText.text = "<color=#00FF99>Зуб успешно удален хирургическими щипцами!</color>";
            }
        }
    }
}
