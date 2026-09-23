using System.Collections.Generic;
using UnityEngine;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M02_ContainerSorting
{
    /// <summary>
    /// Механика #2: Классификация и маршрутизация по типам (Bucket / Filter Sorting).
    /// Три этапа: больше предметов и мусора, требуется точное определение типа.
    /// Провал: попытка положить в корзину неподходящий предмет (не совпадение типа) — этап сбрасывается.
    /// </summary>
    public class M02_ContainerSortingMechanic : BaseMechanic2DModule
    {
        [Header("Корзины и предметы")]
        [SerializeField] private SortContainer[] _containers = null;
        [SerializeField] private SortDraggableItem[] _items = null;
        [SerializeField] private GameObject _obstacleBarrier = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _localInstructionText = null;

        private int _totalRequired;
        private int _totalDeposited;
        private Vector2[] _initialContainerPositions;
        private Vector2[] _initialItemPositions;
        private readonly List<GameObject> _spawnedExtras = new List<GameObject>();

        public override int StageCount => 3;
        public int TotalDeposited => _totalDeposited;
        public int TotalRequired => _totalRequired;

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
                case 1: return "Этап 1/3. Разложите красные яблоки и синие ягоды по соответствующим корзинам. Мусор (X) не кладите в корзины.";
                case 2: return "Этап 2/3. Дополнительные предметы и лишний мусор. Ошибка сортировки — этап начнётся заново.";
                default: return "Этап 3/3. Максимум предметов и мусора. Работайте аккуратно — любая грубая ошибка сбрасывает прогресс.";
            }
        }

        private void CaptureInitialPositions()
        {
            if (_containers != null && _initialContainerPositions == null)
            {
                _initialContainerPositions = new Vector2[_containers.Length];
                for (int i = 0; i < _containers.Length; i++)
                {
                    if (_containers[i] != null)
                    {
                        _initialContainerPositions[i] = _containers[i].RectTransform.anchoredPosition;
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
            if (_containers != null && _initialContainerPositions != null && _containers.Length == _initialContainerPositions.Length)
            {
                Vector2[] shuffled = (Vector2[])_initialContainerPositions.Clone();
                ShuffleArray(shuffled);
                for (int i = 0; i < _containers.Length; i++)
                {
                    if (_containers[i] != null)
                    {
                        _containers[i].RectTransform.anchoredPosition = shuffled[i];
                    }
                }
            }

            if (_items != null && _initialItemPositions != null && _items.Length == _initialItemPositions.Length)
            {
                Vector2[] shuffled = (Vector2[])_initialItemPositions.Clone();
                ShuffleArray(shuffled);
                for (int i = 0; i < _items.Length; i++)
                {
                    if (_items[i] != null)
                    {
                        _items[i].SetHomePosition(shuffled[i]);
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
            if (_items != null)
            {
                foreach (var item in _items)
                {
                    if (item != null)
                    {
                        item.OnItemDeposited += HandleItemDeposited;
                        item.OnItemRejected += HandleItemRejected;
                    }
                }
            }

            if (_containers != null)
            {
                foreach (var container in _containers)
                {
                    if (container != null)
                    {
                        container.OnCountChanged += HandleContainerCountChanged;
                    }
                }
            }
        }

        private void UnbindEvents()
        {
            if (_items != null)
            {
                foreach (var item in _items)
                {
                    if (item != null)
                    {
                        item.OnItemDeposited -= HandleItemDeposited;
                        item.OnItemRejected -= HandleItemRejected;
                    }
                }
            }

            if (_containers != null)
            {
                foreach (var container in _containers)
                {
                    if (container != null)
                    {
                        container.OnCountChanged -= HandleContainerCountChanged;
                    }
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _totalDeposited = 0;
            _totalRequired = 0;

            // Удалить клонированные лишние предметы предыдущего этапа
            for (int i = _spawnedExtras.Count - 1; i >= 0; i--)
            {
                if (_spawnedExtras[i] != null) Destroy(_spawnedExtras[i]);
            }
            _spawnedExtras.Clear();

            CaptureInitialPositions();
            ShufflePositions();

            if (_containers != null)
            {
                foreach (var c in _containers)
                {
                    if (c != null)
                    {
                        c.ResetContainer();
                        _totalRequired += c.RequiredCount;
                    }
                }
            }

            if (_items != null)
            {
                foreach (var it in _items)
                {
                    if (it != null)
                    {
                        it.transform.localScale = Vector3.one;
                        it.ResetItem();
                    }
                }
            }

            SpawnStageExtras(CurrentStage);

            if (_localInstructionText != null)
            {
                _localInstructionText.text = "Разложите предметы по корзинам.";
            }

            SetProgress(0f);
        }

        private void SpawnStageExtras(int stage)
        {
            // Этап 2: +1 мусор. Этап 3: +2 мусора и +1 обычный "лишний" предмет.
            int extraJunk = stage == 2 ? 1 : (stage >= 3 ? 2 : 0);
            int extraDecoys = stage >= 3 ? 1 : 0;

            SortDraggableItem junkTemplate = null;
            SortDraggableItem realTemplate = null;
            if (_items != null)
            {
                foreach (var it in _items)
                {
                    if (it == null) continue;
                    if (it.IsJunk && junkTemplate == null) junkTemplate = it;
                    else if (!it.IsJunk && realTemplate == null) realTemplate = it;
                    if (junkTemplate != null && realTemplate != null) break;
                }
            }

            // Предметы после перемешивания ещё едут домой: занятыми считаются их ДОМАШНИЕ позиции.
            var reserved = new System.Collections.Generic.List<Vector2>();
            if (_items != null) foreach (var it in _items) if (it != null) reserved.Add(it.HomePosition);
            for (int i = 0; i < extraJunk && junkTemplate != null; i++)
            {
                var clone = Instantiate(junkTemplate, junkTemplate.transform.parent);
                clone.name = $"Item_JunkStone_Extra_{stage}_{i + 1}";
                clone.SetJunk(true);
                clone.SetItemTypeId("__junk_extra_" + i);
                Vector2 offset = new Vector2(70f + i * 55f, -20f + (i % 2) * 25f);
                clone.RectTransform.anchoredPosition = FindFreeAnchoredPosition(clone.RectTransform, junkTemplate.HomePosition + offset, 140f, reserved);
                reserved.Add(clone.RectTransform.anchoredPosition);
                clone.SetHomePosition(clone.RectTransform.anchoredPosition);
                clone.OnItemDeposited += HandleItemDeposited;
                clone.OnItemRejected += HandleItemRejected;
                _spawnedExtras.Add(clone.gameObject);
            }

            // Лишний "правильный" предмет: он не увеличивает Required, но выглядит как валидный.
            for (int i = 0; i < extraDecoys && realTemplate != null; i++)
            {
                var clone = Instantiate(realTemplate, realTemplate.transform.parent);
                clone.name = $"Item_Decoy_Extra_{stage}_{i + 1}";
                clone.SetJunk(false);
                // Тип совпадает с шаблоном — корзина примет, но переполнение не даст завершить
                Vector2 offset = new Vector2(-70f - i * 55f, -25f);
                clone.RectTransform.anchoredPosition = FindFreeAnchoredPosition(clone.RectTransform, realTemplate.HomePosition + offset, 140f, reserved);
                reserved.Add(clone.RectTransform.anchoredPosition);
                clone.SetHomePosition(clone.RectTransform.anchoredPosition);
                clone.OnItemDeposited += HandleItemDeposited;
                clone.OnItemRejected += HandleItemRejected;
                _spawnedExtras.Add(clone.gameObject);
            }
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void HandleItemDeposited(SortDraggableItem item, SortContainer container)
        {
            _totalDeposited++;
            float progress = _totalRequired > 0 ? (float)_totalDeposited / _totalRequired : 1f;
            SetProgress(progress);

            if (_totalDeposited >= _totalRequired)
            {
                CompleteMechanic();
                if (_localInstructionText != null)
                {
                    _localInstructionText.text = "<color=#00FF88>Все предметы успешно рассортированы!</color>";
                }
            }
            else if (_localInstructionText != null)
            {
                _localInstructionText.text = $"Успешно: {_totalDeposited}/{_totalRequired}. Продолжайте.";
            }
        }

        private void HandleItemRejected(SortDraggableItem item, SortContainer targetContainer)
        {
            if (IsInTransition) return;

            // Реальная ошибка: игрок сбросил предмет над корзиной, но тип не совпал (или мусор в корзину).
            if (targetContainer != null && item != null)
            {
                if (item.IsJunk)
                {
                    if (_localInstructionText != null)
                    {
                        _localInstructionText.text = "<color=#FF5555>Мусор в корзине! Этап провален.</color>";
                    }
                    FailStage("Мусор (X) сброшен в сортировочную корзину");
                    return;
                }

                // Переполненная корзина своего типа — мягкий отказ (по GDD): предмет возвращается, этап не проваливается.
                if (targetContainer.IsFull && string.Equals(targetContainer.AcceptedTypeId, item.ItemTypeId, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (_localInstructionText != null)
                    {
                        _localInstructionText.text = "<color=#FFCC44>Корзина уже заполнена — этот предмет лишний.</color>";
                    }
                    return;
                }

                if (!targetContainer.CanAccept(item.ItemTypeId))
                {
                    if (_localInstructionText != null)
                    {
                        _localInstructionText.text = "<color=#FF5555>Предмет попал не в ту корзину. Этап провален.</color>";
                    }
                    FailStage("Предмет отправлен в корзину неверного типа");
                    return;
                }
            }

            if (_localInstructionText != null)
            {
                if (item != null && item.IsJunk)
                {
                    _localInstructionText.text = "<color=#FF6666>Этот мусорный камень не подходит ни для одной корзины.</color>";
                }
                else
                {
                    _localInstructionText.text = "<color=#FFCC00>Донесите предмет до нужной корзины.</color>";
                }
            }
        }

        private void HandleContainerCountChanged(SortContainer container, int count)
        {
            // Optional telemetry
        }
    }
}
