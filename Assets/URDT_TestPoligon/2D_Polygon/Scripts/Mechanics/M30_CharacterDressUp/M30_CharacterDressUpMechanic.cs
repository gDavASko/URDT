using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M30_CharacterDressUp
{
    /// <summary>
    /// Механика #30: Одевание персонажа (Character Dress-Up).
    /// Задача: перетащить предметы экипировки (шлем, куртка, сапоги, щит) из гардероба на соответствующие слоты силуэта персонажа.
    /// Мешающий фактор: неподходящий/бракованный предмет [X] и запрет надевания вещей в несоответствующие слоты.
    /// </summary>
    public class M30_CharacterDressUpMechanic : BaseMechanic2DModule
    {
        [Header("Слоты манекена персонажа")]
        [SerializeField] private RectTransform _characterSilhouette = null;
        [SerializeField] private RectTransform _slotHead = null;
        [SerializeField] private RectTransform _slotBody = null;
        [SerializeField] private RectTransform _slotFeet = null;
        [SerializeField] private RectTransform _slotAccessory = null;
        [SerializeField] private float _snapRadius = 55f;

        [Header("Гардероб")]
        [SerializeField] private DressUpItem[] _items = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _progressText = null;
        [SerializeField] private Image _progressFill = null;
        [SerializeField] private TMP_Text _instructionText = null;

        private int _equippedCount = 0;
        private const int TOTAL_SLOTS = 4;

        // Параметры по этапам: количество дополнительных [X]-предметов и лимит времени
        private static readonly int[]   STAGE_EXTRA_JUNK = { 0, 1, 2 };
        private static readonly float[] STAGE_TIME_LIMIT = { 0f, 0f, 22f };
        private static readonly bool[]  STAGE_JUNK_FATAL = { false, true, true };

        private readonly List<DressUpItem> _spawnedItems = new List<DressUpItem>();
        private float _timeLeft = 0f;

        public override int StageCount => 3;

        /// <summary>Сколько слотов на манекене уже экипировано (0..4).</summary>
        public int EquippedCount => _equippedCount;
        /// <summary>Всего слотов на манекене.</summary>
        public int TotalSlots => TOTAL_SLOTS;
        /// <summary>Оставшееся время (сек) или 0, если лимит не действует.</summary>
        public float TimeLeft => _timeLeft;

        protected override void Awake()
        {
            base.Awake();
            if (_items == null || _items.Length == 0)
            {
                _items = GetComponentsInChildren<DressUpItem>(true);
            }

            if (_items != null)
            {
                foreach (var item in _items)
                {
                    if (item != null)
                    {
                        item.OnItemEndDrag += HandleItemEndDrag;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_items != null)
            {
                foreach (var item in _items)
                {
                    if (item != null)
                    {
                        item.OnItemEndDrag -= HandleItemEndDrag;
                    }
                }
            }
            foreach (var item in _spawnedItems)
            {
                if (item != null)
                {
                    item.OnItemEndDrag -= HandleItemEndDrag;
                }
            }
        }

        private void EnsureWardrobePositions()
        {
            if (_items == null || _items.Length == 0)
            {
                _items = GetComponentsInChildren<DressUpItem>(true);
            }

            // 5 строго разнесенных слотов в панели гардероба (X: 130, Y: -5):
            // Верхний ряд (Шлем, Броня), Средний ряд (Сапоги, Реактор), Нижний ряд (Брак [X])
            Vector2[] wardrobeSlots = new Vector2[]
            {
                new Vector2(75f, 40f),   // Head
                new Vector2(185f, 40f),  // Body
                new Vector2(75f, -20f),  // Feet
                new Vector2(185f, -20f), // Accessory
                new Vector2(130f, -80f)  // Junk
            };

            for (int i = 0; i < _items.Length; i++)
            {
                var item = _items[i];
                if (item != null)
                {
                    if (i < wardrobeSlots.Length)
                    {
                        item.SetInitialWardrobePosition(wardrobeSlots[i], item.transform.parent);
                    }
                    item.ResetItem();
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _equippedCount = 0;

            if (_characterSilhouette != null)
            {
                _characterSilhouette.localScale = Vector3.one;
            }

            // Уничтожаем клоны с прошлого запуска
            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                if (_spawnedItems[i] != null) Destroy(_spawnedItems[i].gameObject);
            }
            _spawnedItems.Clear();

            EnsureWardrobePositions();
            SpawnExtraJunkItems();

            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_TIME_LIMIT.Length - 1);
            _timeLeft = STAGE_TIME_LIMIT[idx];

            UpdateUI();
            SetProgress(0f);

            if (_instructionText != null)
            {
                _instructionText.text = GetStageInstruction(CurrentStage);
            }
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, STAGE_EXTRA_JUNK.Length - 1);
            int extra = STAGE_EXTRA_JUNK[idx];
            bool fatal = STAGE_JUNK_FATAL[idx];
            float lim = STAGE_TIME_LIMIT[idx];
            string junkTxt = extra == 0 ? "1 бракованный [X]" : (extra == 1 ? "2 бракованных [X]" : $"{1 + extra} бракованных [X]");
            string fatalTxt = fatal ? "Промах браком в слот — провал этапа." : "Промах браком отбрасывает предмет.";
            string limTxt = lim > 0f ? $" Лимит: {lim:F0} сек." : "";
            return $"Этап {stage}/3. Экипируйте {TOTAL_SLOTS} слотов. В гардеробе {junkTxt}. {fatalTxt}{limTxt}";
        }

        private void SpawnExtraJunkItems()
        {
            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_EXTRA_JUNK.Length - 1);
            int extra = STAGE_EXTRA_JUNK[idx];
            if (extra <= 0 || _items == null || _items.Length == 0) return;

            DressUpItem template = null;
            foreach (var it in _items)
            {
                if (it != null && it.IsJunk) { template = it; break; }
            }
            if (template == null) return;

            Transform parent = template.transform.parent;
            Vector2 basePos = template.RectTransform.anchoredPosition;
            for (int i = 0; i < extra; i++)
            {
                DressUpItem clone = Instantiate(template, parent);
                clone.name = $"DressItem_Junk_Extra_{i + 1}";
                Vector2 pos = basePos + new Vector2((i + 1) * 55f, 0f);
                clone.SetInitialWardrobePosition(pos, parent);
                clone.ResetItem();
                clone.OnItemEndDrag -= HandleItemEndDrag;
                clone.OnItemEndDrag += HandleItemEndDrag;
                _spawnedItems.Add(clone);
            }
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;
            if (_timeLeft > 0f)
            {
                _timeLeft -= Time.deltaTime;
                if (_timeLeft <= 0f)
                {
                    _timeLeft = 0f;
                    FailStage("Время на экипировку истекло");
                }
            }
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void HandleItemEndDrag(DressUpItem item, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_isCompleted || IsInTransition) return;

            int idx = Mathf.Clamp(CurrentStage - 1, 0, STAGE_JUNK_FATAL.Length - 1);
            bool junkFatal = STAGE_JUNK_FATAL[idx];

            if (item.IsJunk)
            {
                // Если [X] брошен рядом с любым слотом — на этапах 2-3 это провал.
                RectTransform[] allSlots = { _slotHead, _slotBody, _slotFeet, _slotAccessory };
                bool droppedOnSlot = false;
                foreach (var s in allSlots)
                {
                    if (s != null && Vector2.Distance(item.RectTransform.position, s.position) <= _snapRadius * 2.5f)
                    {
                        droppedOnSlot = true; break;
                    }
                }
                item.ReturnToWardrobe();
                if (droppedOnSlot && junkFatal)
                {
                    ShowWarning("Бракованный предмет [X] надет на манекен — провал этапа!");
                    FailStage("Использован бракованный предмет");
                }
                else
                {
                    ShowWarning("Бракованный предмет [X] не подходит для экипировки!");
                }
                return;
            }

            RectTransform targetSlot = GetSlotForType(item.SlotType);
            if (targetSlot == null)
            {
                item.ReturnToWardrobe();
                return;
            }

            // Проверяем дистанцию до целевого слота
            float dist = Vector2.Distance(item.RectTransform.position, targetSlot.position);
            if (dist <= _snapRadius * 2.5f) // в мировом масштабе канваса
            {
                item.SetEquipped(targetSlot);
                _equippedCount++;

                float progress = Mathf.Clamp01((float)_equippedCount / TOTAL_SLOTS);
                SetProgress(progress);
                UpdateUI();

                if (_equippedCount >= TOTAL_SLOTS)
                {
                    CompleteDressUp();
                }
            }
            else
            {
                // Проверяем, не бросили ли предмет в чужой слот
                if (IsNearAnyOtherSlot(item.RectTransform.position, targetSlot))
                {
                    ShowWarning($"Этот предмет предназначен для другого слота ({GetSlotName(item.SlotType)})!");
                }
                item.ReturnToWardrobe();
            }
        }

        private bool IsNearAnyOtherSlot(Vector3 pos, RectTransform targetSlot)
        {
            RectTransform[] allSlots = { _slotHead, _slotBody, _slotFeet, _slotAccessory };
            foreach (var s in allSlots)
            {
                if (s != null && s != targetSlot)
                {
                    if (Vector2.Distance(pos, s.position) <= _snapRadius * 2.5f) return true;
                }
            }
            return false;
        }

        private RectTransform GetSlotForType(DressSlotType type)
        {
            switch (type)
            {
                case DressSlotType.Head: return _slotHead;
                case DressSlotType.Body: return _slotBody;
                case DressSlotType.Feet: return _slotFeet;
                case DressSlotType.Accessory: return _slotAccessory;
                default: return null;
            }
        }

        private string GetSlotName(DressSlotType type)
        {
            switch (type)
            {
                case DressSlotType.Head: return "Голова";
                case DressSlotType.Body: return "Торс";
                case DressSlotType.Feet: return "Ноги";
                case DressSlotType.Accessory: return "Аксессуар";
                default: return "";
            }
        }

        private void CompleteDressUp()
        {
            CompleteMechanic();

            if (_instructionText != null)
            {
                _instructionText.text = "<color=#00FF99>Персонаж полностью экипирован и готов к экспедиции!</color>";
            }

            if (_characterSilhouette != null)
            {
                _characterSilhouette.localScale = Vector3.one * 1.08f;
            }
        }

        private void ShowWarning(string msg)
        {
            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#FF5555>{msg}</color>";
            }
        }

        private void UpdateUI()
        {
            if (_progressText != null)
            {
                _progressText.text = $"Экипировано: {_equippedCount} / {TOTAL_SLOTS}";
            }

            if (_progressFill != null)
            {
                _progressFill.fillAmount = Mathf.Clamp01((float)_equippedCount / TOTAL_SLOTS);
            }
        }
    }
}
