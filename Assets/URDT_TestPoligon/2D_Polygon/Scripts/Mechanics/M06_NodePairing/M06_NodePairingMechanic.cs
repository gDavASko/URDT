using System.Collections.Generic;
using UnityEngine;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M06_NodePairing
{
    /// <summary>
    /// Механика #6: Соединение графовых узлов эластичной связью (Node Pairing).
    /// Задача: соединить проводкой соответствующие контакты (Красный -> Красный, Синий -> Синий, Зеленый -> Зеленый).
    /// Мешающие факторы:
    /// 1. Бракованный контакт со знаком 'X' (Pin_Junk) - вызывает короткое замыкание.
    /// 2. Ограниченный радиус захвата целевого контакта (необходимо точное наведение).
    /// </summary>
    public class M06_NodePairingMechanic : BaseMechanic2DModule
    {
        [Header("Контакты")]
        [SerializeField] private NodePin[] _sourcePins = null;
        [SerializeField] private NodePin[] _targetPins = null;
        [SerializeField] private Transform _wiresContainer = null;
        [SerializeField] private Sprite _wireSprite = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _instructionText = null;

        private WireLine _activeDragLine;
        private readonly List<WireLine> _completedLines = new List<WireLine>();
        private int _connectedCount = 0;
        private int _requiredConnections = 3;

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
            if (_sourcePins != null)
            {
                foreach (var pin in _sourcePins)
                {
                    if (pin != null)
                    {
                        pin.OnDragUpdated += HandleDragUpdated;
                        pin.OnDragEnded += HandleDragEnded;
                    }
                }
            }

            if (_targetPins != null)
            {
                foreach (var pin in _targetPins)
                {
                    if (pin != null)
                    {
                        pin.OnDragUpdated += HandleDragUpdated;
                        pin.OnDragEnded += HandleDragEnded;
                    }
                }
            }
        }

        private void UnbindEvents()
        {
            if (_sourcePins != null)
            {
                foreach (var pin in _sourcePins)
                {
                    if (pin != null)
                    {
                        pin.OnDragUpdated -= HandleDragUpdated;
                        pin.OnDragEnded -= HandleDragEnded;
                    }
                }
            }

            if (_targetPins != null)
            {
                foreach (var pin in _targetPins)
                {
                    if (pin != null)
                    {
                        pin.OnDragUpdated -= HandleDragUpdated;
                        pin.OnDragEnded -= HandleDragEnded;
                    }
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _connectedCount = 0;

            if (_activeDragLine != null)
            {
                Destroy(_activeDragLine.gameObject);
                _activeDragLine = null;
            }

            foreach (var line in _completedLines)
            {
                if (line != null) Destroy(line.gameObject);
            }
            _completedLines.Clear();

            if (_sourcePins != null)
            {
                foreach (var p in _sourcePins) if (p != null) p.ResetPin();
            }

            if (_targetPins != null)
            {
                foreach (var p in _targetPins) if (p != null) p.ResetPin();
            }

            if (_instructionText != null)
            {
                _instructionText.text = "Протяните провода от левых клемм к парным правым. Не замыкайте клемму (X)!";
            }

            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void HandleDragUpdated(NodePin fromPin, Vector2 screenPos)
        {
            if (fromPin == null || fromPin.IsConnected) return;

            Transform container = _wiresContainer != null ? _wiresContainer : transform;
            RectTransform containerRt = container as RectTransform;

            if (_activeDragLine == null)
            {
                GameObject lineObj = new GameObject("ActiveDragWire", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(WireLine));
                lineObj.transform.SetParent(container, false);
                _activeDragLine = lineObj.GetComponent<WireLine>();
                UnityEngine.UI.Image img = lineObj.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.raycastTarget = false;
                    if (_wireSprite != null) img.sprite = _wireSprite;
                }
                _activeDragLine.SetColor(GetPinColor(fromPin.PairId));
            }

            Camera cam = null;
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) cam = c.worldCamera;

            if (containerRt != null)
            {
                Vector2 localStart = containerRt.InverseTransformPoint(fromPin.transform.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(containerRt, screenPos, cam, out Vector2 localCursor);
                _activeDragLine.SetLocalEndpoints(localStart, localCursor, 8f);
            }
            else
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(fromPin.RectTransform, screenPos, cam, out Vector3 worldTarget);
                _activeDragLine.SetPositions(fromPin.transform.position, worldTarget, 8f);
            }
        }

        private void HandleDragEnded(NodePin fromPin, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_activeDragLine == null) return;

            NodePin targetPin = FindClosestMatchingPin(fromPin, eventData);

            if (targetPin != null && !targetPin.IsConnected)
            {
                // Дефектный контакт (X)
                if (targetPin.IsJunk || fromPin.IsJunk)
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF5555>Короткое замыкание! Не подключайте к дефектной клемме (X).</color>";
                    }
                    Destroy(_activeDragLine.gameObject);
                    _activeDragLine = null;
                    return;
                }

                // Проверка совпадения пары (A-A, B-B, C-C)
                if (string.Equals(fromPin.PairId, targetPin.PairId, System.StringComparison.OrdinalIgnoreCase))
                {
                    // Успешное соединение
                    fromPin.Connect(targetPin);
                    targetPin.Connect(fromPin);

                    Transform container = _wiresContainer != null ? _wiresContainer : transform;
                    RectTransform containerRt = container as RectTransform;
                    if (containerRt != null)
                    {
                        Vector2 localStart = containerRt.InverseTransformPoint(fromPin.transform.position);
                        Vector2 localEnd = containerRt.InverseTransformPoint(targetPin.transform.position);
                        _activeDragLine.SetLocalEndpoints(localStart, localEnd, 8f);
                    }
                    else
                    {
                        _activeDragLine.SetPositions(fromPin.transform.position, targetPin.transform.position, 8f);
                    }

                    _activeDragLine.SetColor(GetPinColor(fromPin.PairId));
                    _completedLines.Add(_activeDragLine);
                    _activeDragLine = null;

                    _connectedCount++;
                    float progress = (float)_connectedCount / _requiredConnections;
                    SetProgress(progress);

                    if (_connectedCount >= _requiredConnections)
                    {
                        CompleteMechanic();
                        if (_instructionText != null)
                        {
                            _instructionText.text = "<color=#00FF99>Все электрические цепи замкнуты корректно!</color>";
                        }
                    }
                    else
                    {
                        if (_instructionText != null)
                        {
                            _instructionText.text = $"Цепь '{fromPin.PairId}' подключена! ({_connectedCount}/{_requiredConnections})";
                        }
                    }
                    return;
                }
                else
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = $"<color=#FF9900>Несовпадение фазы! Клемма {fromPin.PairId} не подходит к {targetPin.PairId}.</color>";
                    }
                }
            }

            Destroy(_activeDragLine.gameObject);
            _activeDragLine = null;
        }

        private NodePin FindClosestMatchingPin(NodePin fromPin, UnityEngine.EventSystems.PointerEventData eventData)
        {
            Camera cam = null;
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) cam = c.worldCamera;

            Vector2 releasePos = eventData != null ? eventData.position : (Vector2)UnityEngine.Input.mousePosition;

            // 1. Прямой опрос pointerEnter
            if (eventData != null && eventData.pointerEnter != null)
            {
                NodePin hitPin = eventData.pointerEnter.GetComponentInParent<NodePin>();
                if (hitPin != null && hitPin != fromPin && !hitPin.IsConnected)
                {
                    return hitPin;
                }
            }

            Transform container = _wiresContainer != null ? _wiresContainer : transform;
            RectTransform containerRt = container as RectTransform;
            bool hasLocalRelease = false;
            Vector2 localRelease = Vector2.zero;
            if (containerRt != null)
            {
                hasLocalRelease = RectTransformUtility.ScreenPointToLocalPointInRectangle(containerRt, releasePos, cam, out localRelease);
            }

            // 2. Поиск по пулу противоположных пинов
            NodePin[] pool = fromPin.IsSource ? _targetPins : _sourcePins;
            if (pool == null || pool.Length == 0) pool = _targetPins;

            NodePin bestPin = null;
            float bestDist = float.MaxValue;
            const float maxScreenDist = 110f; // Увеличенный комфортный радиус захвата на экране
            const float maxLocalDist = 75f;   // Радиус захвата в локальных координатах канваса

            foreach (var pin in pool)
            {
                if (pin == null || pin == fromPin || pin.IsConnected) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(pin.RectTransform, releasePos, cam))
                {
                    return pin;
                }

                if (hasLocalRelease && containerRt != null)
                {
                    Vector2 pinLocalPos = containerRt.InverseTransformPoint(pin.transform.position);
                    float localDist = Vector2.Distance(localRelease, pinLocalPos);
                    if (localDist <= maxLocalDist && localDist < bestDist)
                    {
                        bestDist = localDist;
                        bestPin = pin;
                        continue;
                    }
                }

                Vector2 pinScreenPos = RectTransformUtility.WorldToScreenPoint(cam, pin.transform.position);
                float screenDist = Vector2.Distance(releasePos, pinScreenPos);
                if (screenDist <= maxScreenDist && screenDist < bestDist)
                {
                    bestDist = screenDist;
                    bestPin = pin;
                }
            }

            // 3. Если не нашли в основном пуле, проверяем все пины кроме текущего
            if (bestPin == null)
            {
                var allPins = new List<NodePin>();
                if (_targetPins != null) allPins.AddRange(_targetPins);
                if (_sourcePins != null) allPins.AddRange(_sourcePins);

                foreach (var pin in allPins)
                {
                    if (pin == null || pin == fromPin || pin.IsConnected || pin.IsSource == fromPin.IsSource) continue;

                    if (RectTransformUtility.RectangleContainsScreenPoint(pin.RectTransform, releasePos, cam))
                    {
                        return pin;
                    }

                    if (hasLocalRelease && containerRt != null)
                    {
                        Vector2 pinLocalPos = containerRt.InverseTransformPoint(pin.transform.position);
                        float localDist = Vector2.Distance(localRelease, pinLocalPos);
                        if (localDist <= maxLocalDist && localDist < bestDist)
                        {
                            bestDist = localDist;
                            bestPin = pin;
                            continue;
                        }
                    }

                    Vector2 pinScreenPos = RectTransformUtility.WorldToScreenPoint(cam, pin.transform.position);
                    float screenDist = Vector2.Distance(releasePos, pinScreenPos);
                    if (screenDist <= maxScreenDist && screenDist < bestDist)
                    {
                        bestDist = screenDist;
                        bestPin = pin;
                    }
                }
            }

            return bestPin;
        }

        private Color GetPinColor(string pairId)
        {
            switch (pairId)
            {
                case "A": return new Color(0.95f, 0.25f, 0.25f, 1f); // Красный
                case "B": return new Color(0.22f, 0.65f, 1f, 1f);  // Синий
                case "C": return new Color(0.22f, 0.95f, 0.35f, 1f); // Зеленый
                default: return new Color(0.7f, 0.7f, 0.75f, 1f);  // Дефектный
            }
        }
    }
}
