using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M11_ConeEmitter
{
    /// <summary>
    /// Механика #11: Наведение конуса воздействия с деградацией HP цели (Cone Emitter).
    /// Три этапа: количество очагов растёт, конус сужается, а долгое поливание электрощита
    /// становится провалом этапа.
    /// </summary>
    public class M11_ConeEmitterMechanic : BaseMechanic2DModule, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Компоненты распылителя")]
        [SerializeField] private RectTransform _nozzleTransform = null;
        [SerializeField] private RectTransform _sprayConeVisual = null;
        [SerializeField] private FireTarget[] _targets = null;
        [SerializeField] private RectTransform _electricHazardBox = null;

        [Header("Параметры конуса")]
        [SerializeField] private float _maxRange = 460f;
        [SerializeField] private float _dps = 65f;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _instructionText = null;

        // Полуугол конуса и число доп. очагов по этапам
        private static readonly float[] _stageConeHalfAngle = { 18f, 14f, 11f };
        private static readonly int[] _stageExtraTargets = { 0, 1, 2 };
        private static readonly float[] _stageHazardBudget = { 0.9f, 0.7f, 0.5f };

        private bool _isSpraying = false;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private Vector2 _currentAimDirection = Vector2.right;

        private readonly List<FireTarget> _activeTargets = new List<FireTarget>();
        private readonly List<GameObject> _spawnedTargets = new List<GameObject>();
        private float _hazardExposure = 0f;

        public override int StageCount => 3;
        public float ConeHalfAngle => _stageConeHalfAngle[Mathf.Clamp(CurrentStage - 1, 0, _stageConeHalfAngle.Length - 1)];
        public float HazardExposureSeconds => _hazardExposure;
        public float HazardBudgetSeconds => _stageHazardBudget[Mathf.Clamp(CurrentStage - 1, 0, _stageHazardBudget.Length - 1)];

        protected override void Awake()
        {
            base.Awake();
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null) _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();

            Image rootImg = GetComponent<Image>();
            if (rootImg == null) rootImg = gameObject.AddComponent<Image>();
            rootImg.color = Color.clear;
            rootImg.raycastTarget = true;

            if (_maxRange < 450f) _maxRange = 480f;
            if (_sprayConeVisual != null && _sprayConeVisual.sizeDelta.x < _maxRange)
            {
                _sprayConeVisual.sizeDelta = new Vector2(_maxRange, Mathf.Max(_sprayConeVisual.sizeDelta.y, 160f));
            }
        }

        protected override string GetStageInstruction(int stage)
        {
            switch (stage)
            {
                case 1: return "Этап 1/3: тушите очаги огня струёй. Не поливайте электрощит (молния)!";
                case 2: return "Этап 2/3: очагов стало больше, конус сужен. Работайте прицельно.";
                default: return "Этап 3/3: пожар разгорелся. Узкая струя, малый допуск на электрощит!";
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _isSpraying = false;
            _currentAimDirection = Vector2.right;
            _hazardExposure = 0f;

            if (_maxRange < 450f) _maxRange = 480f;

            if (_sprayConeVisual != null)
            {
                _sprayConeVisual.gameObject.SetActive(false);
                if (_sprayConeVisual.sizeDelta.x < _maxRange)
                {
                    _sprayConeVisual.sizeDelta = new Vector2(_maxRange, Mathf.Max(_sprayConeVisual.sizeDelta.y, 160f));
                }
            }

            // Уничтожаем клоны с прошлой попытки
            for (int i = 0; i < _spawnedTargets.Count; i++)
            {
                if (_spawnedTargets[i] != null) Destroy(_spawnedTargets[i]);
            }
            _spawnedTargets.Clear();
            _activeTargets.Clear();

            if (_targets != null)
            {
                foreach (var t in _targets)
                {
                    if (t == null) continue;
                    t.ResetTarget();
                    _activeTargets.Add(t);
                }
            }

            int extra = _stageExtraTargets[Mathf.Clamp(CurrentStage - 1, 0, _stageExtraTargets.Length - 1)];
            FireTarget template = _activeTargets.Count > 0 ? _activeTargets[0] : null;
            if (template != null && extra > 0)
            {
                Vector2 basePos = template.RectTransform.anchoredPosition;
                for (int i = 0; i < extra; i++)
                {
                    FireTarget clone = Instantiate(template, template.transform.parent);
                    clone.name = $"FireTarget_Extra_{i + 1:00}";
                    RectTransform crt = clone.RectTransform;
                    if (crt != null)
                    {
                        crt.anchoredPosition = FindFreeAnchoredPosition(crt, basePos + new Vector2((i + 1) * 60f, ((i % 2 == 0) ? 80f : -70f)), 80f);
                    }
                    clone.ResetTarget();
                    _activeTargets.Add(clone);
                    _spawnedTargets.Add(clone.gameObject);
                    Urdt2DBeaconUtility.InstrumentGameObject(clone.gameObject);
                }
            }

            if (_nozzleTransform != null) _nozzleTransform.localRotation = Quaternion.identity;

            if (_instructionText != null) _instructionText.text = GetStageInstruction(CurrentStage);
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;

            float coneHalf = ConeHalfAngle;
            int extinguishedCount = 0;
            int totalTargets = _activeTargets.Count;
            Vector2 nozzlePos = _nozzleTransform != null ? (Vector2)_nozzleTransform.localPosition : Vector2.zero;

            for (int i = 0; i < _activeTargets.Count; i++)
            {
                var target = _activeTargets[i];
                if (target == null) continue;
                if (target.IsExtinguished) { extinguishedCount++; continue; }

                if (_isSpraying && _nozzleTransform != null)
                {
                    Vector2 targetPos = (Vector2)target.RectTransform.localPosition;
                    Vector2 toTarget = targetPos - nozzlePos;
                    float dist = toTarget.magnitude;
                    float angle = Vector2.Angle(_currentAimDirection, toTarget.normalized);

                    if (dist <= _maxRange && angle <= coneHalf)
                    {
                        target.ApplyDamage(_dps);
                        if (target.IsExtinguished) extinguishedCount++;
                    }
                }
            }

            float progress = totalTargets > 0 ? (float)extinguishedCount / totalTargets : 0f;
            SetProgress(progress);

            if (totalTargets > 0 && extinguishedCount >= totalTargets)
            {
                CompleteMechanic();
                if (_sprayConeVisual != null) _sprayConeVisual.gameObject.SetActive(false);
                if (_instructionText != null)
                {
                    _instructionText.text = $"<color=#00FF99>Все очаги ликвидированы! Этап {CurrentStage}/3 завершён.</color>";
                }
                return;
            }

            if (_isSpraying && _nozzleTransform != null)
            {
                if (_sprayConeVisual != null)
                {
                    float pulse = 1f + Mathf.Sin(Time.unscaledTime * 32f) * 0.06f;
                    _sprayConeVisual.localScale = new Vector3(1f, pulse, 1f);
                }

                bool isHazardWarning = false;
                if (_electricHazardBox != null && _electricHazardBox.gameObject.activeInHierarchy)
                {
                    Vector2 hazardPos = (Vector2)_electricHazardBox.localPosition;
                    Vector2 hazardDir = hazardPos - nozzlePos;
                    float dist = hazardDir.magnitude;
                    float angle = Vector2.Angle(_currentAimDirection, hazardDir.normalized);

                    if (dist <= _maxRange && angle <= coneHalf)
                    {
                        isHazardWarning = true;
                        _hazardExposure += Time.unscaledDeltaTime;
                        float budget = HazardBudgetSeconds;
                        if (_hazardExposure >= budget)
                        {
                            FailStage("Короткое замыкание: слишком долго поливали электрощит.");
                            return;
                        }
                        if (_instructionText != null)
                        {
                            _instructionText.text = $"<color=#FF4444>Опасно! Электрощит под струёй ({_hazardExposure:F1}/{budget:F1}с)!</color>";
                        }
                    }
                }

                if (!isHazardWarning && _instructionText != null)
                {
                    _instructionText.text = GetStageInstruction(CurrentStage);
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isCompleted || IsInTransition) return;
            _isSpraying = true;
            if (_sprayConeVisual != null) _sprayConeVisual.gameObject.SetActive(true);
            AimAtScreenPoint(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isCompleted || IsInTransition || !_isSpraying) return;
            AimAtScreenPoint(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isSpraying = false;
            if (_sprayConeVisual != null) _sprayConeVisual.gameObject.SetActive(false);
        }

        private void AimAtScreenPoint(Vector2 screenPoint)
        {
            if (_nozzleTransform == null) return;
            if (_parentCanvas == null) _parentCanvas = GetComponentInParent<Canvas>();

            RectTransform parentRt = _nozzleTransform.parent as RectTransform;
            if (parentRt == null) parentRt = transform as RectTransform;

            Camera cam = _parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (_parentCanvas.worldCamera != null ? _parentCanvas.worldCamera : Camera.main)
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screenPoint, cam, out Vector2 localPoint))
            {
                Vector2 nozzlePos = (Vector2)_nozzleTransform.localPosition;
                Vector2 diff = localPoint - nozzlePos;
                if (diff.sqrMagnitude > 4f)
                {
                    _currentAimDirection = diff.normalized;
                    float angleZ = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
                    _nozzleTransform.localRotation = Quaternion.Euler(0f, 0f, angleZ);
                }
            }
        }
    }
}
