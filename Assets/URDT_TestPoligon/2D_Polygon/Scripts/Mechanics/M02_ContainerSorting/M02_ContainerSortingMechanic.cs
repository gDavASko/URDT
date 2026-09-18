using UnityEngine;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M02_ContainerSorting
{
    /// <summary>
    /// Механика #2: Классификация и маршрутизация по типам (Bucket / Filter Sorting).
    /// Задача: отсортировать предметы по красной и синей корзинам.
    /// Мешающие факторы:
    /// 1. Серый булыжник / мусор (Junk Stone), не принимаемый ни одной корзиной.
    /// 2. Барьер-перегородка между лотком спавна и корзинами.
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

        protected override void Awake()
        {
            base.Awake();
            CaptureInitialPositions();
            BindEvents();
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

            if (_localInstructionText != null)
            {
                _localInstructionText.text = "Разложите красные яблоки и синие ягоды в соответствующие корзины.";
            }

            SetProgress(0f);
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
            else
            {
                if (_localInstructionText != null)
                {
                    _localInstructionText.text = $"Успешно: {_totalDeposited}/{_totalRequired}. Продолжайте сортировку!";
                }
            }
        }

        private void HandleItemRejected(SortDraggableItem item)
        {
            if (_localInstructionText != null)
            {
                if (item.IsJunk)
                {
                    _localInstructionText.text = "<color=#FF6666>Этот мусорный камень не подходит ни для одной корзины!</color>";
                }
                else
                {
                    _localInstructionText.text = "<color=#FFCC00>Корзина не принимает данный тип предмета!</color>";
                }
            }
        }

        private void HandleContainerCountChanged(SortContainer container, int count)
        {
            // Optional telemetry
        }
    }
}
