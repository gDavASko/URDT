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

            EnsureWardrobePositions();

            UpdateUI();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void HandleItemEndDrag(DressUpItem item, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_isCompleted) return;

            if (item.IsJunk)
            {
                ShowWarning("Бракованный предмет [X] не подходит для экипировки!");
                item.ReturnToWardrobe();
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
