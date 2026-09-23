using System.Collections.Generic;
using UnityEngine;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M06_NodePairing
{
    /// <summary>
    /// Механика #6: Соединение графовых узлов эластичной связью (Node Pairing).
    /// Три этапа: больше клемм и повышенная точность соединения.
    /// Провал: попытка подключить провод к бракованной клемме (X) или соединить неверную пару.
    /// </summary>
    public class M06_NodePairingMechanic : BaseMechanic2DModule
    {
        public override int StageCount => 3;
        private readonly List<GameObject> _spawnedExtras = new List<GameObject>();
        public int ConnectedCount => _connectedCount;
        public int RequiredConnections => _requiredConnections;

        protected override string GetStageInstruction(int stage)
        {
            switch (stage)
            {
                case 1: return "Этап 1/3. Соедините проводами парные клеммы одного цвета. Не касайтесь клемм (X).";
                case 2: return "Этап 2/3. Появились дополнительные ложные клеммы. Неверное соединение — этап начнётся заново.";
                default: return "Этап 3/3. Больше клемм-обманок. Работайте точно: короткое замыкание сбрасывает прогресс.";
            }
        }

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

            // Удалить клоны прошлого этапа
            for (int i = _spawnedExtras.Count - 1; i >= 0; i--)
            {
                if (_spawnedExtras[i] != null) Destroy(_spawnedExtras[i]);
            }
            _spawnedExtras.Clear();

            if (_sourcePins != null)
            {
                foreach (var p in _sourcePins) if (p != null) p.ResetPin();
            }

            if (_targetPins != null)
            {
                foreach (var p in _targetPins) if (p != null) p.ResetPin();
            }

            SpawnStageExtras(CurrentStage);

            SetProgress(0f);
        }

        private void SpawnStageExtras(int stage)
        {
            if (stage < 2) return;

            NodePin srcTemplate = null;
            NodePin tgtTemplate = null;
            if (_sourcePins != null && _sourcePins.Length > 0) srcTemplate = _sourcePins[0];
            if (_targetPins != null && _targetPins.Length > 0) tgtTemplate = _targetPins[0];

            int extra = stage == 2 ? 1 : 2;
            for (int i = 0; i < extra; i++)
            {
                if (srcTemplate != null) SpawnJunkPin(srcTemplate, true, i, stage);
                if (tgtTemplate != null) SpawnJunkPin(tgtTemplate, false, i, stage);
            }
        }

        private void SpawnJunkPin(NodePin template, bool source, int index, int stage)
        {
            var clone = Instantiate(template, template.transform.parent);
            clone.name = $"Pin_Junk_{(source ? "Src" : "Tgt")}_S{stage}_{index + 1}";
            clone.SetSource(source);
            clone.SetJunk(true);
            clone.SetPairId("X");
            var rt = clone.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 basePos = template.GetComponent<RectTransform>().anchoredPosition;
                rt.anchoredPosition = FindFreeAnchoredPosition(rt, basePos + new Vector2(0f, -80f - index * 60f), 70f);
            }
            clone.OnDragUpdated += HandleDragUpdated;
            clone.OnDragEnded += HandleDragEnded;
            _spawnedExtras.Add(clone.gameObject);
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
                // Дефектный контакт (X) — короткое замыкание, провал этапа
                if (targetPin.IsJunk || fromPin.IsJunk)
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF5555>Короткое замыкание на клемме (X)! Этап провален.</color>";
                    }
                    Destroy(_activeDragLine.gameObject);
                    _activeDragLine = null;
                    FailStage("Короткое замыкание на дефектной клемме (X)");
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
                        _instructionText.text = $"<color=#FF5555>Несовпадение фазы: {fromPin.PairId} ≠ {targetPin.PairId}. Этап провален.</color>";
                    }
                    Destroy(_activeDragLine.gameObject);
                    _activeDragLine = null;
                    if (CurrentStage >= 2)
                    {
                        FailStage($"Соединение неверной пары ({fromPin.PairId} → {targetPin.PairId})");
                    }
                    return;
                }
            }

            if (_activeDragLine != null)
            {
                Destroy(_activeDragLine.gameObject);
                _activeDragLine = null;
            }
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
