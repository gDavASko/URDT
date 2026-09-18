using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M14_SlingshotImpulser
{
    /// <summary>
    /// Механика #14: Баллистический импульс с обратным вектором (Slingshot Impulser).
    /// Задача: оттянуть снаряд назад и отпустить, чтобы сбить 3 жестяные банки.
    /// Мешающие факторы:
    /// 1. Неразрушимая каменная колонна-препятствие [X] по центру — перекрывает прямой выстрел.
    /// 2. Ограничение дальности натяжения и баллистическая гравитация снаряда.
    /// </summary>
    public class M14_SlingshotImpulserMechanic : BaseMechanic2DModule, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Снаряд и рогатка")]
        [SerializeField] private RectTransform _projectileBall = null;
        [SerializeField] private RectTransform _slingshotAnchor = null;
        [SerializeField] private RectTransform _trajectoryIndicator = null;

        [Header("Траектория прицеливания")]
        [SerializeField] private RectTransform _trajectoryDotsContainer = null;
        [SerializeField] private int _trajectoryPointCount = 22;
        [SerializeField] private float _timeStep = 0.04f;
        [SerializeField] private Color _trajectoryColor = new Color(1f, 0.85f, 0.25f, 0.9f);

        [Header("Мишени и препятствие")]
        [SerializeField] private SlingshotTargetCan[] _targets = null;
        [SerializeField] private RectTransform _obstaclePillar = null;

        [Header("Параметры баллистики")]
        [SerializeField] private float _maxPullDistance = 90f;
        [SerializeField] private float _launchForceMultiplier = 7f;
        [SerializeField] private float _gravity = 250f;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _scoreText = null;
        [SerializeField] private TMP_Text _instructionText = null;

        private Vector2 _anchorPos;
        private Vector2 _velocity;
        private bool _isAiming = false;
        private bool _isFlying = false;
        private float _flightTimer = 0f;
        private Canvas _canvas;
        private int _hitCount = 0;

        private readonly List<RectTransform> _dots = new List<RectTransform>();
        private readonly List<Image> _dotImages = new List<Image>();

        protected override void Awake()
        {
            base.Awake();
            _canvas = GetComponentInParent<Canvas>();
            if (_slingshotAnchor != null) _anchorPos = _slingshotAnchor.anchoredPosition;

            if (_targets != null)
            {
                foreach (var t in _targets)
                {
                    if (t != null) t.OnHit += HandleCanHit;
                }
            }

            EnsureTrajectoryDots();
        }

        private void OnDestroy()
        {
            if (_targets != null)
            {
                foreach (var t in _targets)
                {
                    if (t != null) t.OnHit -= HandleCanHit;
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _isAiming = false;
            _isFlying = false;
            _hitCount = 0;
            _flightTimer = 0f;

            if (_slingshotAnchor != null) _anchorPos = _slingshotAnchor.anchoredPosition;
            ResetBall();

            if (_targets != null)
            {
                foreach (var t in _targets) if (t != null) t.ResetCan();
            }

            if (_trajectoryIndicator != null) _trajectoryIndicator.gameObject.SetActive(false);
            HideTrajectory();

            UpdateScoreUI();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void ResetBall()
        {
            _isFlying = false;
            _velocity = Vector2.zero;
            HideTrajectory();
            if (_projectileBall != null)
            {
                _projectileBall.anchoredPosition = _anchorPos;
                _projectileBall.gameObject.SetActive(true);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isCompleted || _isFlying) return;

            Vector2 pointerPos = ScreenToLocalPos(eventData.position);
            if (Vector2.Distance(pointerPos, _anchorPos) <= 70f)
            {
                _isAiming = true;
                UpdateAim(pointerPos);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isAiming || _isFlying) return;
            Vector2 pointerPos = ScreenToLocalPos(eventData.position);
            UpdateAim(pointerPos);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isAiming) return;
            _isAiming = false;

            if (_trajectoryIndicator != null) _trajectoryIndicator.gameObject.SetActive(false);
            HideTrajectory();

            if (_projectileBall != null)
            {
                Vector2 pull = _anchorPos - _projectileBall.anchoredPosition;
                if (pull.magnitude > 15f)
                {
                    _isFlying = true;
                    _flightTimer = 0f;
                    _velocity = pull * _launchForceMultiplier;
                }
                else
                {
                    ResetBall();
                }
            }
        }

        private void EnsureTrajectoryDots()
        {
            if (_dots.Count > 0 && _dots[0] != null) return;
            _dots.Clear();
            _dotImages.Clear();

            Sprite dotSprite = null;
            if (_projectileBall != null)
            {
                var ballImg = _projectileBall.GetComponent<Image>();
                if (ballImg != null) dotSprite = ballImg.sprite;
            }

            if (_trajectoryDotsContainer == null)
            {
                Transform existing = transform.Find("TrajectoryDots");
                if (existing != null)
                {
                    _trajectoryDotsContainer = existing as RectTransform;
                }
                else
                {
                    GameObject go = new GameObject("TrajectoryDots", typeof(RectTransform));
                    go.transform.SetParent(transform, false);
                    _trajectoryDotsContainer = go.GetComponent<RectTransform>();
                    _trajectoryDotsContainer.anchorMin = Vector2.zero;
                    _trajectoryDotsContainer.anchorMax = Vector2.one;
                    _trajectoryDotsContainer.offsetMin = Vector2.zero;
                    _trajectoryDotsContainer.offsetMax = Vector2.zero;
                }
            }

            if (_projectileBall != null && _trajectoryDotsContainer.GetSiblingIndex() > _projectileBall.GetSiblingIndex())
            {
                _trajectoryDotsContainer.SetSiblingIndex(_projectileBall.GetSiblingIndex());
            }

            int childCount = _trajectoryDotsContainer.childCount;
            for (int i = 0; i < _trajectoryPointCount; i++)
            {
                RectTransform dotRt;
                Image dotImg;
                if (i < childCount)
                {
                    dotRt = _trajectoryDotsContainer.GetChild(i) as RectTransform;
                    dotImg = dotRt.GetComponent<Image>();
                    if (dotImg == null) dotImg = dotRt.gameObject.AddComponent<Image>();
                }
                else
                {
                    GameObject dotObj = new GameObject($"Dot_{i:00}", typeof(RectTransform), typeof(Image));
                    dotObj.transform.SetParent(_trajectoryDotsContainer, false);
                    dotRt = dotObj.GetComponent<RectTransform>();
                    dotImg = dotObj.GetComponent<Image>();
                }

                dotImg.raycastTarget = false;
                if (dotSprite != null) dotImg.sprite = dotSprite;

                float factor = 1f - ((float)i / _trajectoryPointCount);
                float size = Mathf.Lerp(6f, 14f, factor);
                dotRt.sizeDelta = new Vector2(size, size);

                Color col = _trajectoryColor;
                col.a = Mathf.Lerp(0.2f, 0.95f, factor);
                dotImg.color = col;

                dotRt.gameObject.SetActive(false);
                _dots.Add(dotRt);
                _dotImages.Add(dotImg);
            }
        }

        private void UpdateAim(Vector2 pointerPos)
        {
            Vector2 pullVector = pointerPos - _anchorPos;
            if (pullVector.magnitude > _maxPullDistance)
            {
                pullVector = pullVector.normalized * _maxPullDistance;
            }

            if (_projectileBall != null)
            {
                _projectileBall.anchoredPosition = _anchorPos + pullVector;
            }

            if (_trajectoryIndicator != null)
            {
                _trajectoryIndicator.gameObject.SetActive(false);
            }

            UpdateTrajectory(pullVector);
        }

        private void UpdateTrajectory(Vector2 pullVector)
        {
            EnsureTrajectoryDots();

            if (pullVector.magnitude < 10f)
            {
                HideTrajectory();
                return;
            }

            Vector2 launchVelocity = -pullVector * _launchForceMultiplier;
            bool blocked = false;

            Vector2 obsPos = _obstaclePillar != null ? _obstaclePillar.anchoredPosition : Vector2.zero;
            Vector2 obsSize = _obstaclePillar != null ? _obstaclePillar.sizeDelta : Vector2.zero;
            float halfW = obsSize.x * 0.5f + 12f;
            float halfH = obsSize.y * 0.5f + 12f;

            for (int i = 0; i < _dots.Count; i++)
            {
                if (blocked)
                {
                    _dots[i].gameObject.SetActive(false);
                    continue;
                }

                float t = (i + 1) * _timeStep;
                Vector2 pt = _anchorPos + launchVelocity * t + 0.5f * new Vector2(0f, -_gravity) * (t * t);

                // Столкновение с колонной-препятствием [X]
                if (_obstaclePillar != null &&
                    Mathf.Abs(pt.x - obsPos.x) <= halfW &&
                    Mathf.Abs(pt.y - obsPos.y) <= halfH)
                {
                    _dots[i].anchoredPosition = pt;
                    _dots[i].gameObject.SetActive(true);
                    _dotImages[i].color = new Color(1f, 0.3f, 0.2f, 0.95f); // Красная точка удара
                    blocked = true;
                    continue;
                }

                // Превышение границ игрового поля
                if (pt.y < -220f || pt.x > 330f || pt.x < -330f)
                {
                    _dots[i].gameObject.SetActive(false);
                    blocked = true;
                    continue;
                }

                // Подсветка траектории при попадании в активную мишень
                bool targetHit = false;
                if (_targets != null)
                {
                    foreach (var target in _targets)
                    {
                        if (target != null && !target.IsHit &&
                            Vector2.Distance(pt, target.RectTransform.anchoredPosition) < 32f)
                        {
                            targetHit = true;
                            break;
                        }
                    }
                }

                float factor = 1f - ((float)i / _dots.Count);
                Color col = targetHit ? new Color(0.3f, 1f, 0.5f, 0.95f) : _trajectoryColor;
                col.a = Mathf.Lerp(0.25f, 0.95f, factor);
                _dotImages[i].color = col;

                _dots[i].anchoredPosition = pt;
                _dots[i].gameObject.SetActive(true);
            }
        }

        private void HideTrajectory()
        {
            if (_dots != null)
            {
                for (int i = 0; i < _dots.Count; i++)
                {
                    if (_dots[i] != null) _dots[i].gameObject.SetActive(false);
                }
            }
        }

        private void Update()
        {
            if (_isCompleted) return;

            if (_isFlying && _projectileBall != null)
            {
                _flightTimer += Time.unscaledDeltaTime;
                _velocity.y -= _gravity * Time.unscaledDeltaTime;

                Vector2 pos = _projectileBall.anchoredPosition;
                pos += _velocity * Time.unscaledDeltaTime;
                _projectileBall.anchoredPosition = pos;

                // Проверка столкновения с препятствием
                if (_obstaclePillar != null)
                {
                    if (Vector2.Distance(pos, _obstaclePillar.anchoredPosition) < 40f)
                    {
                        // Рикошет / отскок
                        _velocity.x = -Mathf.Abs(_velocity.x) * 0.4f;
                        if (_instructionText != null)
                        {
                            _instructionText.text = "<color=#FF8844>Удар о препятствие [X]! Снаряд срикошетил в сторону.</color>";
                        }
                    }
                }

                // Проверка попадания в мишени
                if (_targets != null)
                {
                    foreach (var target in _targets)
                    {
                        if (target == null || target.IsHit) continue;
                        if (Vector2.Distance(pos, target.RectTransform.anchoredPosition) < 35f)
                        {
                            target.Hit();
                            break;
                        }
                    }
                }

                // Превышение границ полета или времени
                if (pos.x > 320f || pos.y < -220f || pos.x < -320f || _flightTimer > 2.5f)
                {
                    ResetBall();
                }
            }
        }

        private void HandleCanHit(SlingshotTargetCan can)
        {
            _hitCount++;
            UpdateScoreUI();

            int totalTargets = _targets != null ? _targets.Length : 3;
            float progress = Mathf.Clamp01((float)_hitCount / totalTargets);
            SetProgress(progress);

            if (_hitCount >= totalTargets)
            {
                CompleteMechanic();
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#00FF99>Великолепно! Все банки сбиты меткими выстрелами!</color>";
                }
            }
        }

        private void UpdateScoreUI()
        {
            int totalTargets = _targets != null ? _targets.Length : 3;
            if (_scoreText != null)
            {
                _scoreText.text = $"Сбито: {_hitCount} / {totalTargets}";
            }
        }

        private Vector2 ScreenToLocalPos(Vector2 screenPoint)
        {
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform,
                screenPoint,
                cam,
                out Vector2 localPoint);
            return localPoint;
        }
    }
}
