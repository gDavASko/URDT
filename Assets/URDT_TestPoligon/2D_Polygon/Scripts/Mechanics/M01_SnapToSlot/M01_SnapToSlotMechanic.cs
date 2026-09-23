using System.Collections.Generic;
using UnityEngine;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M01_SnapToSlot
{
    /// <summary>
    /// Механика #1: Drag & Drop — примагничивание деталей в подходящие пазы.
    /// Три этапа возрастающей сложности: больше бракованных деталей, точнее посадка.
    /// Провал: попытка вставить бракованную деталь (X) в паз.
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
        private float[] _initialSnapThresholds;
        private readonly List<GameObject> _spawnedExtras = new List<GameObject>();

        public override int StageCount => 3;
        public int RequiredSnaps => _slots != null ? _slots.Length : 0;
        public int SnappedCount => _snappedCount;

        protected override void Awake()
        {
            base.Awake();
            CaptureInitialPositions();
            BindEvents();
        }

        protected override string GetStageInstruction(int stage)
        {
            switch (stage)
            {
                case 1: return "Этап 1/3. Перетащите цветные детали в соответствующие пазы. Бракованную деталь (X) не используйте.";
                case 2: return "Этап 2/3. Деталей и брака стало больше. Посадка требует более точного наведения.";
                default: return "Этап 3/3. Максимум помех: несколько бракованных деталей, узкий допуск. Не ошибитесь!";
            }
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
                _initialSnapThresholds = new float[_items.Length];
                for (int i = 0; i < _items.Length; i++)
                {
                    if (_items[i] != null)
                    {
                        _initialItemPositions[i] = _items[i].RectTransform.anchoredPosition;
                        _initialSnapThresholds[i] = _items[i].SnapThreshold;
                    }
                }
            }
        }

        private void ShufflePositions()
        {
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

            // Очистка ранее склонированных мусорных деталей
            for (int i = _spawnedExtras.Count - 1; i >= 0; i--)
            {
                if (_spawnedExtras[i] != null) Destroy(_spawnedExtras[i]);
            }
            _spawnedExtras.Clear();

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
                for (int i = 0; i < _items.Length; i++)
                {
                    var item = _items[i];
                    if (item != null)
                    {
                        item.transform.localScale = Vector3.one;
                        // Восстановить исходный радиус
                        if (_initialSnapThresholds != null && i < _initialSnapThresholds.Length)
                        {
                            item.SetSnapThreshold(_initialSnapThresholds[i]);
                        }
                        item.ResetItem();
                    }
                }
            }

            ApplyStageDifficulty(CurrentStage);
            UpdateLocalStatus();
        }

        private void ApplyStageDifficulty(int stage)
        {
            // На 2-3 этапах: клонируем дополнительные бракованные детали и снижаем радиус магнита
            int extraJunks = stage == 2 ? 1 : (stage >= 3 ? 2 : 0);
            float thresholdFactor = stage == 2 ? 0.85f : (stage >= 3 ? 0.7f : 1f);

            if (_items != null)
            {
                foreach (var item in _items)
                {
                    if (item != null) item.SetSnapThreshold(item.SnapThreshold * thresholdFactor);
                }
            }

            // Найти образцовую бракованную деталь и клонировать
            SnapDraggableItem junkTemplate = null;
            if (_items != null)
            {
                foreach (var it in _items) { if (it != null && it.IsJunk) { junkTemplate = it; break; } }
            }

            if (junkTemplate == null || extraJunks <= 0) return;

            Transform parent = junkTemplate.transform.parent;
            // Предметы после перемешивания ещё едут домой: занятыми считаются их ДОМАШНИЕ позиции.
            var reserved = new List<Vector2>();
            foreach (var it in _items) if (it != null) reserved.Add(it.HomePosition);
            for (int i = 0; i < extraJunks; i++)
            {
                var clone = Instantiate(junkTemplate, parent);
                clone.name = $"Item_Junk_Extra_{stage}_{i + 1}";
                clone.SetJunk(true);
                clone.SetItemId("__junk_extra_" + i);
                Vector2 offset = new Vector2(80f + i * 60f, -30f + (i % 2) * 20f);
                clone.RectTransform.anchoredPosition = FindFreeAnchoredPosition(clone.RectTransform, junkTemplate.HomePosition + offset, 140f, reserved);
                clone.SetHomePosition(clone.RectTransform.anchoredPosition);
                reserved.Add(clone.HomePosition);
                clone.OnItemSnapped += HandleItemSnapped;
                clone.OnItemRejected += HandleItemRejected;
                _spawnedExtras.Add(clone.gameObject);
            }
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
            if (IsInTransition) return;

            if (item != null && item.IsJunk && slot != null)
            {
                // Игрок явно пытался вставить бракованную деталь в паз — провал этапа.
                if (_localInstructionText != null)
                {
                    _localInstructionText.text = "<color=#FF5555>Бракованная деталь в пазу! Этап провален.</color>";
                }
                FailStage("Попытка установить бракованную деталь (X) в паз");
                return;
            }

            if (_localInstructionText != null)
            {
                if (item != null && item.IsJunk)
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
                _localInstructionText.text = $"Этап {CurrentStage}/{StageCount}. Заполнено пазов: {_snappedCount} / {total}";
            }
        }
    }
}
