using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M20_VerticalStacking
{
    /// <summary>
    /// Механика #20: Своевременный сброс с проверкой устойчивости (Vertical Stacking).
    /// Задача: вовремя сбрасывать качающиеся блоки на платформу, чтобы построить устойчивую башню из 3 блоков.
    /// Мешающие факторы:
    /// 1. Нестабильный растрескавшийся блок [X] — имеет сниженное окно допуска по центру.
    /// 2. Маятниковое колебание крана с увеличивающейся скоростью.
    /// </summary>
    public class M20_VerticalStackingMechanic : BaseMechanic2DModule
    {
        [Header("Визуальные элементы")]
        [SerializeField] private RectTransform _swingingArm = null;
        [SerializeField] private RectTransform _activeBlock = null;
        [SerializeField] private RectTransform _basePlatform = null;
        [SerializeField] private Button _btnDrop = null;

        [Header("Спрайты")]
        [SerializeField] private Sprite _standardBlockSprite = null;
        [SerializeField] private Sprite _junkBlockSprite = null;

        [Header("Параметры башни")]
        [SerializeField] private float _swingAmplitude = 140f;
        [SerializeField] private float _swingSpeed = 2.4f;
        [SerializeField] private float _maxOffsetTolerance = 35f;
        [SerializeField] private int _targetStackCount = 3;
        [SerializeField] private float _blockHeight = 45f;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _stackText = null;
        [SerializeField] private TMP_Text _instructionText = null;

        private int _stackedCount = 0;
        private bool _isFalling = false;
        private float _fallSpeed = 450f;
        private float _baseY = -120f;
        private float _currentTopX = 0f;
        private List<GameObject> _placedBlocks = new List<GameObject>();
        private Image _activeBlockImage;

        protected override void Awake()
        {
            if (_activeBlock != null) _activeBlockImage = _activeBlock.GetComponent<Image>();
            if (_basePlatform != null) _baseY = _basePlatform.anchoredPosition.y + 35f;
            base.Awake();
            if (_btnDrop != null) _btnDrop.onClick.AddListener(DropBlock);
        }

        private void OnDestroy()
        {
            if (_btnDrop != null) _btnDrop.onClick.RemoveListener(DropBlock);
            ClearPlacedBlocks();
        }

        public override void Initialize()
        {
            base.Initialize();
            _stackedCount = 0;
            _isFalling = false;
            _currentTopX = 0f;

            ClearPlacedBlocks();
            ResetSwingingBlock();
            UpdateUI();
            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        public void DropBlock()
        {
            if (_isCompleted || _isFalling) return;
            _isFalling = true;
        }

        private void ResetSwingingBlock()
        {
            _isFalling = false;
            if (_activeBlock != null)
            {
                _activeBlock.anchoredPosition = new Vector2(0f, 65f);
                _activeBlock.gameObject.SetActive(true);

                // 2-й блок делаем бракованным [X]
                bool isJunk = (_stackedCount == 1);
                if (_activeBlockImage != null)
                {
                    _activeBlockImage.sprite = isJunk ? _junkBlockSprite : _standardBlockSprite;
                    _activeBlockImage.color = Color.white;
                }
            }
        }

        private void Update()
        {
            if (_isCompleted) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space)) DropBlock();

            float targetPlatformY = _baseY + (_stackedCount * _blockHeight);

            if (!_isFalling)
            {
                // Маятниковое колебание
                float time = Time.unscaledTime * _swingSpeed;
                float swingX = Mathf.Sin(time) * _swingAmplitude;

                if (_swingingArm != null)
                {
                    _swingingArm.anchoredPosition = new Vector2(swingX, 85f);
                }
                if (_activeBlock != null)
                {
                    _activeBlock.anchoredPosition = new Vector2(swingX, 65f);
                }
            }
            else
            {
                // Падение блока вниз
                if (_activeBlock != null)
                {
                    Vector2 pos = _activeBlock.anchoredPosition;
                    pos.y -= _fallSpeed * Time.unscaledDeltaTime;

                    if (pos.y <= targetPlatformY)
                    {
                        pos.y = targetPlatformY;
                        _activeBlock.anchoredPosition = pos;

                        // Проверка точности приземления
                        float offset = Mathf.Abs(pos.x - _currentTopX);
                        bool isJunk = (_stackedCount == 1);
                        float tolerance = isJunk ? (_maxOffsetTolerance * 0.6f) : _maxOffsetTolerance;

                        if (offset <= tolerance)
                        {
                            // Успешная посадка в стек
                            _stackedCount++;
                            _currentTopX = pos.x;

                            // Закрепляем блок в сцене
                            GameObject placed = Instantiate(_activeBlock.gameObject, _activeBlock.parent);
                            placed.GetComponent<RectTransform>().anchoredPosition = pos;
                            _placedBlocks.Add(placed);

                            float progress = Mathf.Clamp01((float)_stackedCount / _targetStackCount);
                            SetProgress(progress);
                            UpdateUI();

                            if (_stackedCount >= _targetStackCount)
                            {
                                CompleteMechanic();
                                if (_activeBlock != null) _activeBlock.gameObject.SetActive(false);
                                if (_instructionText != null)
                                {
                                    _instructionText.text = "<color=#00FF99>Башня идеально построена! Устойчивость подтверждена!</color>";
                                }
                                return;
                            }
                            else
                            {
                                if (_instructionText != null)
                                {
                                    _instructionText.text = $"<color=#00FF99>Точная посадка (смещение {offset:F0}px)!</color>";
                                }
                            }
                        }
                        else
                        {
                            // Падение/соскальзывание блока
                            if (_instructionText != null)
                            {
                                _instructionText.text = $"<color=#FF5555>Перекос! Смещение {offset:F0}px превысило допуск ({tolerance:F0}px). Попробуйте еще раз!</color>";
                            }
                        }

                        ResetSwingingBlock();
                    }
                    else
                    {
                        _activeBlock.anchoredPosition = pos;
                    }
                }
            }
        }

        private void UpdateUI()
        {
            if (_stackText != null)
            {
                _stackText.text = $"Блоков в башне: {_stackedCount} / {_targetStackCount}";
            }
        }

        private void ClearPlacedBlocks()
        {
            foreach (var b in _placedBlocks)
            {
                if (b != null) Destroy(b);
            }
            _placedBlocks.Clear();
        }
    }
}
