using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M28_DualBridgeRoute
{
    /// <summary>
    /// Механика #28: Построение маршрута с мостиками для двух сущностей в 2 финиша (Dual Bridge Route).
    /// Задача: перетащить исправные мостики в два разрыва трассы и запустить двух персонажей к их финишам.
    /// Мешающий фактор: сломанный мостик [X], не выдерживающий нагрузки.
    /// </summary>
    public class M28_DualBridgeRouteMechanic : BaseMechanic2DModule
    {
        [Header("Сущности и финиши")]
        [SerializeField] private RectTransform _entityA = null; // Синий бот
        [SerializeField] private RectTransform _entityB = null; // Оранжевый бот
        [SerializeField] private RectTransform _goalA = null;
        [SerializeField] private RectTransform _goalB = null;

        [Header("Разрывы дорожек (Слоты мостов)")]
        [SerializeField] private RectTransform _slotA = null;
        [SerializeField] private RectTransform _slotB = null;
        [SerializeField] private float _snapRadius = 75f;

        [Header("Мостики")]
        [SerializeField] private BridgePlank[] _planks = null;

        [Header("Управление и UI")]
        [SerializeField] private Button _btnStart = null;
        [SerializeField] private TMP_Text _instructionText = null;
        [SerializeField] private Image _progressFill = null;

        private Vector2 _startPosA = new Vector2(-230f, 75f);
        private Vector2 _startPosB = new Vector2(-230f, 5f);
        private BridgePlank _slottedPlankA;
        private BridgePlank _slottedPlankB;
        private bool _isWalking = false;

        protected override void Awake()
        {
            base.Awake();
            if (_entityA != null) _startPosA = _entityA.anchoredPosition;
            if (_entityB != null) _startPosB = _entityB.anchoredPosition;

            if (_btnStart != null)
            {
                _btnStart.onClick.AddListener(OnStartClicked);
            }

            BindPlankEvents();
        }

        private void Start()
        {
            if (_entityA != null && _startPosA == Vector2.zero) _startPosA = _entityA.anchoredPosition;
            if (_entityB != null && _startPosB == Vector2.zero) _startPosB = _entityB.anchoredPosition;
            UpdateStateUI();
        }

        private void Update()
        {
            if (_isCompleted || _isWalking) return;

            bool ready = _slottedPlankA != null && _slottedPlankB != null;
            if (ready && (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return)))
            {
                OnStartClicked();
            }
        }

        private void BindPlankEvents()
        {
            if (_planks == null || _planks.Length == 0)
            {
                _planks = GetComponentsInChildren<BridgePlank>(true);
            }

            if (_planks != null)
            {
                foreach (var plank in _planks)
                {
                    if (plank != null)
                    {
                        plank.OnPlankEndDrag -= HandlePlankEndDrag;
                        plank.OnPlankEndDrag += HandlePlankEndDrag;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_btnStart != null) _btnStart.onClick.RemoveListener(OnStartClicked);

            if (_planks != null)
            {
                foreach (var plank in _planks)
                {
                    if (plank != null)
                    {
                        plank.OnPlankEndDrag -= HandlePlankEndDrag;
                    }
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            StopAllCoroutines();
            _isWalking = false;
            _slottedPlankA = null;
            _slottedPlankB = null;

            if (_entityA != null) _entityA.anchoredPosition = _startPosA;
            if (_entityB != null) _entityB.anchoredPosition = _startPosB;

            BindPlankEvents();

            if (_planks != null)
            {
                foreach (var plank in _planks)
                {
                    if (plank != null) plank.ResetPlank();
                }
            }

            UpdateStateUI();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void HandlePlankEndDrag(BridgePlank plank, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_isWalking || _isCompleted || plank == null) return;

            RectTransform rootRt = transform as RectTransform;
            Vector2 plankPosInRoot = rootRt.InverseTransformPoint(plank.transform.position);
            Vector2 slotAPosInRoot = _slotA != null ? (Vector2)rootRt.InverseTransformPoint(_slotA.position) : new Vector2(9999f, 9999f);
            Vector2 slotBPosInRoot = _slotB != null ? (Vector2)rootRt.InverseTransformPoint(_slotB.position) : new Vector2(9999f, 9999f);

            float distA = Vector2.Distance(plankPosInRoot, slotAPosInRoot);
            float distB = Vector2.Distance(plankPosInRoot, slotBPosInRoot);

            // Проверяем попадание в слот A
            if (distA <= _snapRadius && distA <= distB)
            {
                if (plank.IsBroken)
                {
                    ShowWarning("Сломанный мостик [X] рухнет под весом бота! Используйте целый мост.");
                    plank.ReturnToOrigin();
                    return;
                }

                if (_slottedPlankB == plank) _slottedPlankB = null;
                _slottedPlankA = plank;
                plank.transform.SetParent(rootRt, true);
                plank.RectTransform.anchoredPosition = _slotA.anchoredPosition;
                UpdateStateUI();
                return;
            }

            // Проверяем попадание в слот B
            if (distB <= _snapRadius)
            {
                if (plank.IsBroken)
                {
                    ShowWarning("Сломанный мостик [X] рухнет под весом бота! Используйте целый мост.");
                    plank.ReturnToOrigin();
                    return;
                }

                if (_slottedPlankA == plank) _slottedPlankA = null;
                _slottedPlankB = plank;
                plank.transform.SetParent(rootRt, true);
                plank.RectTransform.anchoredPosition = _slotB.anchoredPosition;
                UpdateStateUI();
                return;
            }

            // Если брошен мимо слотов — освобождаем слот и возвращаем на склад
            if (_slottedPlankA == plank) _slottedPlankA = null;
            if (_slottedPlankB == plank) _slottedPlankB = null;
            plank.ReturnToOrigin();
            UpdateStateUI();
        }

        private void UpdateStateUI()
        {
            int bridgesPlaced = (_slottedPlankA != null ? 1 : 0) + (_slottedPlankB != null ? 1 : 0);
            float progress = bridgesPlaced * 0.35f;
            SetProgress(progress);

            if (_progressFill != null)
            {
                _progressFill.fillAmount = progress;
            }

            bool ready = _slottedPlankA != null && _slottedPlankB != null;
            if (_btnStart != null)
            {
                _btnStart.interactable = ready && !_isWalking;
                Image btnImg = _btnStart.GetComponent<Image>();
                if (btnImg != null)
                {
                    btnImg.color = ready ? new Color(0f, 0.85f, 0.45f, 1f) : new Color(0.2f, 0.28f, 0.38f, 0.85f);
                }
            }

            if (_instructionText != null && !_isWalking && !_isCompleted)
            {
                if (!ready)
                {
                    _instructionText.text = $"Установите оба мостика в разрывы путей (установлено {bridgesPlaced}/2). Остерегайтесь сломанного [X]!";
                }
                else
                {
                    _instructionText.text = "<color=#00FF99>Мостики установлены! Нажмите 'ПУСК >>' (или пробел).</color>";
                }
            }
        }

        private void ShowWarning(string msg)
        {
            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#FF4444>{msg}</color>";
            }
        }

        private void OnStartClicked()
        {
            if (_isWalking || _isCompleted) return;
            if (_slottedPlankA == null || _slottedPlankB == null) return;

            StartCoroutine(WalkEntitiesRoutine());
        }

        private IEnumerator WalkEntitiesRoutine()
        {
            _isWalking = true;
            if (_btnStart != null) _btnStart.interactable = false;

            Vector2 targetA = _goalA != null ? _goalA.anchoredPosition : _startPosA + new Vector2(430f, 0f);
            Vector2 targetB = _goalB != null ? _goalB.anchoredPosition : _startPosB + new Vector2(430f, 0f);

            float duration = 2.4f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Плавная анимация шагов (покачивание вверх-вниз)
                float stepBobA = Mathf.Sin(t * Mathf.PI * 10f) * 5f;
                float stepBobB = Mathf.Cos(t * Mathf.PI * 10f) * 5f;

                if (_entityA != null)
                {
                    Vector2 currentPos = Vector2.Lerp(_startPosA, targetA, t);
                    currentPos.y += stepBobA;
                    _entityA.anchoredPosition = currentPos;
                }

                if (_entityB != null)
                {
                    Vector2 currentPos = Vector2.Lerp(_startPosB, targetB, t);
                    currentPos.y += stepBobB;
                    _entityB.anchoredPosition = currentPos;
                }

                float totalProgress = 0.7f + (t * 0.3f);
                SetProgress(totalProgress);
                if (_progressFill != null) _progressFill.fillAmount = totalProgress;

                yield return null;
            }

            if (_entityA != null) _entityA.anchoredPosition = targetA;
            if (_entityB != null) _entityB.anchoredPosition = targetB;

            _isWalking = false;
            SetProgress(1f);
            if (_progressFill != null) _progressFill.fillAmount = 1f;

            CompleteMechanic();

            if (_instructionText != null)
            {
                _instructionText.text = "<color=#00FF99>УСПЕХ! Оба робота благополучно пересекли мосты и достигли финиша!</color>";
            }
        }
    }
}
