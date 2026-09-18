using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KBP.URDT.TestPoligon.Mechanics2D.M05_TimelineSequencer
{
    /// <summary>
    /// Ячейка таймлайна очереди команд (Command Slot).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class CommandSlot : MonoBehaviour
    {
        [Header("Конфигурация слота")]
        [SerializeField] private int _stepIndex = 0;
        [SerializeField] private TMP_Text _stepLabel = null;
        [SerializeField] private Image _highlightBorder = null;

        private RectTransform _rectTransform;
        private CommandChip _assignedChip;

        public int StepIndex => _stepIndex;
        public bool IsOccupied => _assignedChip != null;
        public CommandChip AssignedChip => _assignedChip;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_stepLabel != null)
            {
                _stepLabel.text = $"#{_stepIndex + 1}";
            }
        }

        public void AssignChip(CommandChip chip)
        {
            _assignedChip = chip;
            if (_highlightBorder != null)
            {
                _highlightBorder.color = new Color(0.2f, 0.9f, 0.4f, 0.8f);
            }
        }

        public void ClearSlot()
        {
            _assignedChip = null;
            if (_highlightBorder != null)
            {
                _highlightBorder.color = new Color(0.4f, 0.5f, 0.6f, 0.4f);
            }
        }
    }
}
