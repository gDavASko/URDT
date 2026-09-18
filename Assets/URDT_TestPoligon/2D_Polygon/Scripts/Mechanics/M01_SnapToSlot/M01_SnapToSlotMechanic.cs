using UnityEngine;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M01_SnapToSlot
{
    /// <summary>
    /// Механика #1: Drag & Drop - Позиционирование с примагничиванием в таргет (Snap-to-Slot).
    /// Задача игрока: перетащить три цветные детали в соответствующие пазы.
    /// Мешающие факторы:
    /// 1. Мусорная деталь со знаком 'X', которую не принимает ни один слот.
    /// 2. Статичный барьер-препятствие по центру, требующий огибания.
    /// </summary>
    public class M01_SnapToSlotMechanic : BaseMechanic2DModule
    {
        [Header("Слоты и детали")]
        [SerializeField] private SnapSlot[] _slots = null;
        [SerializeField] private SnapDraggableItem[] _items = null;
        [SerializeField] private GameObject _obstacleBarrier = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _localInstructionText = null;

        private int _snappedCount;
        private Vector2[] _initialSlotPositions;
        private Vector2[] _initialItemPositions;

        protected override void Awake()
        {
            base.Awake();
            CaptureInitialPositions();
            BindEvents();
        }

        private void CaptureInitialPositions()
        {
            if (_slots != null && _initialSlotPositions == null)
            {
                _initialSlotPositions = new Vector2[_slots.Length];
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null)
                    {
                        _initialSlotPositions[i] = _slots[i].RectTransform.anchoredPosition;
                    }
                }
            }

            if (_items != null && _initialItemPositions == null)
            {
                _initialItemPositions = new Vector2[_items.Length];
                for (int i = 0; i < _items.Length; i++)
                {
                    if (_items[i] != null)
                    {
                        _initialItemPositions[i] = _items[i].RectTransform.anchoredPosition;
                    }
                }
            }
        }

        private void ShufflePositions()
        {
            // Рандомизация позиций слотов-пазов
            if (_slots != null && _initialSlotPositions != null && _slots.Length == _initialSlotPositions.Length)
            {
                Vector2[] shuffledSlotPositions = (Vector2[])_initialSlotPositions.Clone();
                ShuffleArray(shuffledSlotPositions);
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null)
                    {
                        _slots[i].RectTransform.anchoredPosition = shuffledSlotPositions[i];
                    }
                }
            }

            // Рандомизация позиций предметов в лотке
            if (_items != null && _initialItemPositions != null && _items.Length == _initialItemPositions.Length)
            {
                Vector2[] shuffledItemPositions = (Vector2[])_initialItemPositions.Clone();
                ShuffleArray(shuffledItemPositions);
                for (int i = 0; i < _items.Length; i++)
                {
                    if (_items[i] != null)
                    {
                        _items[i].SetHomePosition(shuffledItemPositions[i]);
                    }
                }
            }
        }

        private static void ShuffleArray<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int rnd = UnityEngine.Random.Range(0, i + 1);
                T temp = array[i];
                array[i] = array[rnd];
                array[rnd] = temp;
            }
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        private void BindEvents()
        {
            if (_items == null) return;
            foreach (var item in _items)
            {
                if (item != null)
                {
                    item.OnItemSnapped += HandleItemSnapped;
                    item.OnItemRejected += HandleItemRejected;
                }
            }
        }

        private void UnbindEvents()
        {
            if (_items == null) return;
            foreach (var item in _items)
            {
                if (item != null)
                {
                    item.OnItemSnapped -= HandleItemSnapped;
                    item.OnItemRejected -= HandleItemRejected;
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _snappedCount = 0;

            CaptureInitialPositions();
            ShufflePositions();

            if (_slots != null)
            {
                foreach (var slot in _slots)
                {
                    if (slot != null) slot.ResetSlot();
                }
            }

            if (_items != null)
            {
                foreach (var item in _items)
                {
                    if (item != null)
                    {
                        item.transform.localScale = Vector3.one;
                        item.ResetItem();
                    }
                }
            }

            UpdateLocalStatus();
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        public SnapSlot[] Slots => _slots;

        private void HandleItemSnapped(SnapDraggableItem item, SnapSlot slot)
        {
            _snappedCount++;
            int total = _slots != null ? _slots.Length : 3;
            NotifyProgress(_snappedCount, total);
            UpdateLocalStatus();

            if (_snappedCount >= total)
            {
                if (_localInstructionText != null)
                {
                    _localInstructionText.text = "<color=#00FF99><b>ОТЛИЧНО! Все детали на своих местах!</b></color>";
                }
            }
        }

        private void HandleItemRejected(SnapDraggableItem item, SnapSlot slot)
        {
            if (_localInstructionText != null)
            {
                if (item.IsJunk)
                {
                    _localInstructionText.text = "<color=#FF5555>Эта деталь бракованная и не подходит ни в один паз!</color>";
                }
                else if (slot != null)
                {
                    _localInstructionText.text = "<color=#FFAA00>Деталь не подходит к этому пазу. Найдите правильный!</color>";
                }
                else
                {
                    _localInstructionText.text = "<color=#FFAA00>Перетащите деталь ближе к одному из пазов выше!</color>";
                }
            }
        }

        private void UpdateLocalStatus()
        {
            if (_localInstructionText != null && !_isCompleted)
            {
                int total = _slots != null ? _slots.Length : 3;
                _localInstructionText.text = $"Перетащите подходящие детали в пазы. Заполнено: {_snappedCount} / {total}";
            }
        }
    }
}
