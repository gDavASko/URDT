using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KBP.URDT.TestPoligon.Mechanics2D.M18_GraphFlowClosure
{
    public enum PipeType
    {
        Straight, // Соединяет противоположные стороны
        Corner,   // Соединяет две смежные стороны
        Source,   // Источник (всегда активен)
        Sink,     // Приемник
        BrokenJunk // Сломанная труба [X] - не пропускает поток
    }

    /// <summary>
    /// Ячейка трубы, поворачивающаяся на 90 градусов при клике.
    /// Направления битовой маски: 1=Вверх, 2=Вправо, 4=Вниз, 8=Влево.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class PipeTile : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private PipeType _type = PipeType.Straight;
        [SerializeField] private int _rotationSteps = 0; // 0..3 (умножается на 90 градусов)
        [SerializeField] private Image _pipeImage = null;

        public event Action<PipeTile> OnTileRotated;

        private RectTransform _rectTransform;
        public int GridX { get; set; }
        public int GridY { get; set; }
        public PipeType Type => _type;
        public bool HasWater { get; private set; }

        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public Image PipeImage => _pipeImage != null ? _pipeImage : (_pipeImage = GetComponent<Image>());

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_pipeImage == null) _pipeImage = GetComponent<Image>();
            ApplyVisualRotation();
        }

        public void Setup(PipeType type, int initialRotation, Sprite sprite = null)
        {
            _type = type;
            _rotationSteps = initialRotation % 4;
            HasWater = false;
            if (_pipeImage == null) _pipeImage = GetComponent<Image>();
            if (sprite != null && _pipeImage != null) _pipeImage.sprite = sprite;
            ApplyVisualRotation();
            UpdateWaterVisual(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_type == PipeType.Source || _type == PipeType.Sink || _type == PipeType.BrokenJunk) return;

            _rotationSteps = (_rotationSteps + 1) % 4;
            ApplyVisualRotation();
            OnTileRotated?.Invoke(this);
        }

        private void ApplyVisualRotation()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            _rectTransform.localRotation = Quaternion.Euler(0f, 0f, -_rotationSteps * 90f);
        }

        public void UpdateWaterVisual(bool hasWater)
        {
            HasWater = hasWater;
            if (_pipeImage != null)
            {
                if (_type == PipeType.BrokenJunk)
                {
                    _pipeImage.color = new Color(0.8f, 0.4f, 0.4f, 1f);
                }
                else
                {
                    _pipeImage.color = hasWater ? new Color(0.2f, 1f, 0.9f, 1f) : Color.white;
                }
            }
        }

        /// <summary>
        /// Возвращает маску открытых сторон (Up=1, Right=2, Down=4, Left=8).
        /// </summary>
        public int GetOpenConnections()
        {
            if (_type == PipeType.BrokenJunk) return 0;

            int baseMask = 0;
            switch (_type)
            {
                case PipeType.Straight:
                    baseMask = 2 | 8; // Горизонтальная труба (Вправо и Влево) в исходном спрайте
                    break;
                case PipeType.Corner:
                    baseMask = 4 | 8; // Угловая труба (Вниз и Влево) в исходном спрайте
                    break;
                case PipeType.Source:
                    baseMask = 2;     // Вправо в базовом положении
                    break;
                case PipeType.Sink:
                    baseMask = 1 | 2 | 4 | 8; // Приемник принимает поток с любой стороны
                    break;
            }

            // Вращаем битовую маску на _rotationSteps вправо
            int mask = baseMask;
            for (int i = 0; i < _rotationSteps; i++)
            {
                int newMask = 0;
                if ((mask & 1) != 0) newMask |= 2;
                if ((mask & 2) != 0) newMask |= 4;
                if ((mask & 4) != 0) newMask |= 8;
                if ((mask & 8) != 0) newMask |= 1;
                mask = newMask;
            }

            return mask;
        }
    }
}
