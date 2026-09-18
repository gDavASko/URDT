using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M11_ConeEmitter
{
    /// <summary>
    /// Механика #11: Наведение конуса воздействия с деградацией HP цели (Cone Emitter).
    /// Задача: удерживать струю огнетушителя на очагах пожара до полного тушения.
    /// Мешающие факторы:
    /// 1. Электрощит со знаком молнии (Hazard Box) — при попадании струи искрит и дает штраф.
    /// 2. Узкий сектор распыления (35 градусов) и ограничение дистанции струи.
    /// </summary>
    public class M11_ConeEmitterMechanic : BaseMechanic2DModule, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Компоненты распылителя")]
        [SerializeField] private RectTransform _nozzleTransform = null;
        [SerializeField] private RectTransform _sprayConeVisual = null;
        [SerializeField] private FireTarget[] _targets = null;
        [SerializeField] private RectTransform _electricHazardBox = null;

        [Header("Параметры конуса")]
        [SerializeField] private float _coneHalfAngle = 18f;
        [SerializeField] private float _maxRange = 460f;
        [SerializeField] private float _dps = 65f;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _instructionText = null;

        private bool _isSpraying = false;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private Vector2 _currentAimDirection = Vector2.right;

        protected override void Awake()
        {
            base.Awake();
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null) _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();

            // Гарантируем прозрачный Image на корне, чтобы ловить клики и перетаскивание по всей рабочей области экрана
            Image rootImg = GetComponent<Image>();
            if (rootImg == null)
            {
                rootImg = gameObject.AddComponent<Image>();
            }
            rootImg.color = Color.clear;
            rootImg.raycastTarget = true;

            // Защита от заниженной дальности струи из сериализованных данных префаба
            if (_maxRange < 450f)
            {
                _maxRange = 480f;
            }

            if (_sprayConeVisual != null && _sprayConeVisual.sizeDelta.x < _maxRange)
            {
                _sprayConeVisual.sizeDelta = new Vector2(_maxRange, Mathf.Max(_sprayConeVisual.sizeDelta.y, 160f));
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _isSpraying = false;
            _currentAimDirection = Vector2.right;

            if (_maxRange < 450f)
            {
                _maxRange = 480f;
            }

            if (_sprayConeVisual != null)
            {
                _sprayConeVisual.gameObject.SetActive(false);
                if (_sprayConeVisual.sizeDelta.x < _maxRange)
                {
                    _sprayConeVisual.sizeDelta = new Vector2(_maxRange, Mathf.Max(_sprayConeVisual.sizeDelta.y, 160f));
                }
            }

            if (_targets != null)
            {
                foreach (var t in _targets) if (t != null) t.ResetTarget();
            }

            if (_nozzleTransform != null)
            {
                _nozzleTransform.localRotation = Quaternion.identity;
            }

            if (_instructionText != null)
            {
                _instructionText.text = "Зажмите и направляйте струю на очаги огня. Остерегайтесь электрощита (молния)!";
            }

            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void Update()
        {
            if (_isCompleted) return;

            int extinguishedCount = 0;
            int totalTargets = _targets != null ? _targets.Length : 0;
            Vector2 nozzlePos = _nozzleTransform != null ? (Vector2)_nozzleTransform.localPosition : Vector2.zero;

            // Проверка и нанесение урона очагам огня
            if (_targets != null)
            {
                foreach (var target in _targets)
                {
                    if (target == null) continue;
                    if (target.IsExtinguished)
                    {
                        extinguishedCount++;
                        continue;
                    }

                    if (_isSpraying && _nozzleTransform != null)
                    {
                        Vector2 targetPos = (Vector2)target.RectTransform.localPosition;
                        Vector2 toTarget = targetPos - nozzlePos;
                        float dist = toTarget.magnitude;
                        float angle = Vector2.Angle(_currentAimDirection, toTarget.normalized);

                        if (dist <= _maxRange && angle <= _coneHalfAngle)
                        {
                            target.ApplyDamage(_dps);
                            if (target.IsExtinguished)
                            {
                                extinguishedCount++;
                            }
                        }
                    }
                }
            }

            float progress = totalTargets > 0 ? (float)extinguishedCount / totalTargets : 0f;
            SetProgress(progress);

            // Проверка завершения механики (срабатывает всегда, даже если игрок отпустил мышь)
            if (totalTargets > 0 && extinguishedCount >= totalTargets)
            {
                CompleteMechanic();
                if (_sprayConeVisual != null) _sprayConeVisual.gameObject.SetActive(false);
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#00FF99>Все очаги возгорания успешно ликвидированы!</color>";
                }
                return;
            }

            // Активное распыление: визуал и предупреждение об электрощите
            if (_isSpraying && _nozzleTransform != null)
            {
                // Пульсация струи
                if (_sprayConeVisual != null)
                {
                    float pulse = 1f + Mathf.Sin(Time.unscaledTime * 32f) * 0.06f;
                    _sprayConeVisual.localScale = new Vector3(1f, pulse, 1f);
                }

                bool isHazardWarning = false;
                // Проверка электрощита (штраф / предупреждение)
                if (_electricHazardBox != null && _electricHazardBox.gameObject.activeInHierarchy)
                {
                    Vector2 hazardPos = (Vector2)_electricHazardBox.localPosition;
                    Vector2 hazardDir = hazardPos - nozzlePos;
                    float dist = hazardDir.magnitude;
                    float angle = Vector2.Angle(_currentAimDirection, hazardDir.normalized);

                    if (dist <= _maxRange && angle <= _coneHalfAngle)
                    {
                        isHazardWarning = true;
                        if (_instructionText != null)
                        {
                            _instructionText.text = "<color=#FF4444>Опасно! Не лейте воду на электрощит (молния)!</color>";
                        }
                    }
                }

                if (!isHazardWarning && _instructionText != null)
                {
                    _instructionText.text = "Зажмите и направляйте струю на очаги огня. Остерегайтесь электрощита (молния)!";
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isCompleted) return;
            _isSpraying = true;
            if (_sprayConeVisual != null) _sprayConeVisual.gameObject.SetActive(true);
            AimAtScreenPoint(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isCompleted || !_isSpraying) return;
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

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRt,
                    screenPoint,
                    cam,
                    out Vector2 localPoint))
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
