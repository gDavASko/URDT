using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M19_FloodFillColoring
{
    /// <summary>
    /// Механика #19: Присвоение идентификатора сегменту маски (Tag / Flood Fill).
    /// Задача: выбрать цвет в палитре и раскрасить сегменты схемы в соответствии с эталонными метками.
    /// Мешающие факторы:
    /// 1. Бракованная палитра с грязной жижей [X] — при ее выборе сегмент загрязняется.
    /// 2. Необходимость точного соответствия цветов эталону (Красный, Зеленый, Синий, Желтый).
    /// </summary>
    public class M19_FloodFillColoringMechanic : BaseMechanic2DModule
    {
        [Header("Сегменты для раскраски")]
        [SerializeField] private ColoringSegment[] _segments = null;

        [Header("Кнопки палитры")]
        [SerializeField] private Button _btnRed = null;
        [SerializeField] private Button _btnGreen = null;
        [SerializeField] private Button _btnBlue = null;
        [SerializeField] private Button _btnYellow = null;
        [SerializeField] private Button _btnJunkMud = null;

        [Header("Индикатор активного цвета")]
        [SerializeField] private Image _activeColorIndicator = null;
        [SerializeField] private TMP_Text _instructionText = null;

        [Header("Эталон (Образец для раскраски)")]
        [SerializeField] private RectTransform _etalonContainer = null;
        [SerializeField] private Image[] _etalonPreviews = null;

        private int _selectedColorId = 1; // 1: Red, 2: Green, 3: Blue, 4: Yellow, 99: Mud
        private Color _selectedColor = new Color(1f, 0.3f, 0.3f, 1f);

        private readonly Color COLOR_RED = new Color(1f, 0.3f, 0.3f, 1f);
        private readonly Color COLOR_GREEN = new Color(0.25f, 0.85f, 0.45f, 1f);
        private readonly Color COLOR_BLUE = new Color(0.2f, 0.55f, 1f, 1f);
        private readonly Color COLOR_YELLOW = new Color(1f, 0.85f, 0.2f, 1f);
        private readonly Color COLOR_MUD = new Color(0.45f, 0.38f, 0.32f, 1f);

        protected override void Awake()
        {
            base.Awake();
            if (_btnRed != null) _btnRed.onClick.AddListener(() => SelectColor(1, COLOR_RED));
            if (_btnGreen != null) _btnGreen.onClick.AddListener(() => SelectColor(2, COLOR_GREEN));
            if (_btnBlue != null) _btnBlue.onClick.AddListener(() => SelectColor(3, COLOR_BLUE));
            if (_btnYellow != null) _btnYellow.onClick.AddListener(() => SelectColor(4, COLOR_YELLOW));
            if (_btnJunkMud != null) _btnJunkMud.onClick.AddListener(() => SelectColor(99, COLOR_MUD));

            if (_segments != null)
            {
                foreach (var seg in _segments)
                {
                    if (seg != null) seg.OnSegmentClicked += HandleSegmentClicked;
                }
            }

            EnsureEtalonDisplay();
        }

        private void OnDestroy()
        {
            if (_btnRed != null) _btnRed.onClick.RemoveAllListeners();
            if (_btnGreen != null) _btnGreen.onClick.RemoveAllListeners();
            if (_btnBlue != null) _btnBlue.onClick.RemoveAllListeners();
            if (_btnYellow != null) _btnYellow.onClick.RemoveAllListeners();
            if (_btnJunkMud != null) _btnJunkMud.onClick.RemoveAllListeners();

            if (_segments != null)
            {
                foreach (var seg in _segments)
                {
                    if (seg != null) seg.OnSegmentClicked -= HandleSegmentClicked;
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            EnsureEtalonDisplay();
            ShuffleTargetColors();
            SelectColor(1, COLOR_RED);

            if (_segments != null)
            {
                foreach (var seg in _segments) if (seg != null) seg.ResetSegment();
            }

            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void EnsureEtalonDisplay()
        {
            if (_etalonPreviews != null && _etalonPreviews.Length == 4 && _etalonPreviews[0] != null) return;

            Sprite frameSpr = null;
            if (_segments != null && _segments.Length > 0 && _segments[0] != null)
            {
                var img = _segments[0].GetComponent<Image>();
                if (img != null) frameSpr = img.sprite;
            }

            if (_etalonContainer == null)
            {
                Transform existing = transform.Find("EtalonPanel");
                if (existing != null)
                {
                    _etalonContainer = existing as RectTransform;
                }
                else
                {
                    // Создаем панель Эталона справа от схемы
                    GameObject panelObj = new GameObject("EtalonPanel", typeof(RectTransform), typeof(Image));
                    panelObj.transform.SetParent(transform, false);
                    _etalonContainer = panelObj.GetComponent<RectTransform>();
                    _etalonContainer.anchoredPosition = new Vector2(230f, -5f);
                    _etalonContainer.sizeDelta = new Vector2(130f, 170f);

                    Image bgImg = panelObj.GetComponent<Image>();
                    bgImg.color = new Color(0.08f, 0.12f, 0.2f, 0.92f);

                    // Заголовок "ЭТАЛОН"
                    GameObject titleObj = new GameObject("EtalonTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                    titleObj.transform.SetParent(_etalonContainer, false);
                    RectTransform tRt = titleObj.GetComponent<RectTransform>();
                    tRt.anchoredPosition = new Vector2(0f, 62f);
                    tRt.sizeDelta = new Vector2(120f, 26f);
                    TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
                    titleTmp.text = "ЭТАЛОН";
                    titleTmp.fontSize = 15f;
                    titleTmp.fontStyle = FontStyles.Bold;
                    titleTmp.alignment = TextAlignmentOptions.Center;
                    titleTmp.color = new Color(1f, 0.85f, 0.25f, 1f);

                    // 4 мини-сегмента эталона
                    Vector2[] previewPositions = new Vector2[]
                    {
                        new Vector2(-28f, 16f), // Верх-Лево
                        new Vector2(28f, 16f),  // Верх-Право
                        new Vector2(-28f, -32f), // Низ-Лево
                        new Vector2(28f, -32f)   // Низ-Право
                    };

                    _etalonPreviews = new Image[4];
                    for (int i = 0; i < 4; i++)
                    {
                        GameObject prevObj = new GameObject($"Preview_{i + 1}", typeof(RectTransform), typeof(Image));
                        prevObj.transform.SetParent(_etalonContainer, false);
                        RectTransform pRt = prevObj.GetComponent<RectTransform>();
                        pRt.anchoredPosition = previewPositions[i];
                        pRt.sizeDelta = new Vector2(50f, 40f);

                        Image pImg = prevObj.GetComponent<Image>();
                        if (frameSpr != null) pImg.sprite = frameSpr;
                        pImg.raycastTarget = false;
                        _etalonPreviews[i] = pImg;
                    }
                }
            }

            if (_etalonPreviews == null || _etalonPreviews.Length != 4 || _etalonPreviews[0] == null)
            {
                _etalonPreviews = new Image[4];
                for (int i = 0; i < 4; i++)
                {
                    Transform pt = _etalonContainer.Find($"Preview_{i + 1}");
                    if (pt != null) _etalonPreviews[i] = pt.GetComponent<Image>();
                }
            }

            UpdateEtalonVisuals();
        }

        private void ShuffleTargetColors()
        {
            if (_segments == null || _segments.Length != 4) return;
            int[] colors = new int[] { 1, 2, 3, 4 };
            for (int i = 0; i < colors.Length; i++)
            {
                int r = Random.Range(i, colors.Length);
                int temp = colors[i];
                colors[i] = colors[r];
                colors[r] = temp;
            }

            for (int i = 0; i < 4; i++)
            {
                if (_segments[i] != null) _segments[i].SetExpectedColorId(colors[i]);
            }

            UpdateEtalonVisuals();
        }

        private void UpdateEtalonVisuals()
        {
            if (_etalonPreviews == null || _segments == null) return;
            for (int i = 0; i < Mathf.Min(_etalonPreviews.Length, _segments.Length); i++)
            {
                if (_etalonPreviews[i] != null && _segments[i] != null)
                {
                    _etalonPreviews[i].color = GetColorForId(_segments[i].ExpectedColorId);
                }
            }
        }

        private Color GetColorForId(int id)
        {
            switch (id)
            {
                case 1: return COLOR_RED;
                case 2: return COLOR_GREEN;
                case 3: return COLOR_BLUE;
                case 4: return COLOR_YELLOW;
                default: return Color.gray;
            }
        }

        private void SelectColor(int colorId, Color colorVisual)
        {
            _selectedColorId = colorId;
            _selectedColor = colorVisual;

            if (_activeColorIndicator != null)
            {
                _activeColorIndicator.color = colorVisual;
            }

            if (_selectedColorId == 99 && _instructionText != null)
            {
                _instructionText.text = "<color=#FF5555>Внимание! Вы выбрали грязную жижу [X]! Не используйте её для раскраски схемы!</color>";
            }
        }

        private void HandleSegmentClicked(ColoringSegment segment)
        {
            if (_isCompleted) return;

            segment.ApplyColor(_selectedColorId, _selectedColor);

            if (_selectedColorId == 99)
            {
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#FF4444>Сегмент испорчен грязью [X]! Выберите чистый цвет в палитре и перекрасьте.</color>";
                }
            }

            CheckCompletion();
        }

        private void CheckCompletion()
        {
            if (_segments == null || _segments.Length == 0) return;

            int correctCount = _segments.Count(s => s != null && s.IsCorrect);
            float progress = (float)correctCount / _segments.Length;
            SetProgress(progress);

            if (correctCount >= _segments.Length)
            {
                CompleteMechanic();
                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#00FF99>Великолепно! Все сегменты раскрашены в соответствии со схемой!</color>";
                }
            }
        }
    }
}
