using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace KBP.URDT.TestPoligon.Mechanics2D.M22_GridPathfinding
{
    public enum GridCellType
    {
        Walkable,
        ObstacleWall,
        GlitchTrapHazard,
        Goal
    }

    /// <summary>
    /// Интерактивная ячейка сетки перемещения.
    /// Автоматически поддерживает стили, цветовую кодировку препятствий/ловушек/целей и самовосстановление UI.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class GridTileButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GridCellType _cellType = GridCellType.Walkable;
        [SerializeField] private Image _tileImage = null;
        [SerializeField] private TMP_Text _markerText = null;

        [SerializeField] private int _cellX;
        [SerializeField] private int _cellY;

        public event Action<GridTileButton> OnTileClicked;

        private RectTransform _rectTransform;
        public int CellX { get => _cellX; set => _cellX = value; }
        public int CellY { get => _cellY; set => _cellY = value; }
        public GridCellType CellType => _cellType;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_tileImage == null) _tileImage = GetComponent<Image>();
            EnsureMarker();
            ApplyVisualTheme();
        }

        private void Start()
        {
            ApplyVisualTheme();
        }

        public void Setup(GridCellType type, int x, int y, Sprite sprite, Color tint)
        {
            _cellType = type;
            _cellX = x;
            _cellY = y;
            if (_tileImage == null) _tileImage = GetComponent<Image>();
            if (_tileImage != null)
            {
                if (sprite != null) _tileImage.sprite = sprite;
                _tileImage.color = tint;
            }
            ApplyVisualTheme();
        }

        public void ApplyCellType(GridCellType type, Sprite optionalSprite = null)
        {
            _cellType = type;
            if (_tileImage == null) _tileImage = GetComponent<Image>();
            if (_tileImage != null && optionalSprite != null)
            {
                _tileImage.sprite = optionalSprite;
            }
            ApplyVisualTheme();
        }

        private void EnsureMarker()
        {
            if (_markerText != null) return;
            var child = transform.Find("TileMarker");
            if (child != null)
            {
                _markerText = child.GetComponent<TMP_Text>();
            }
            if (_markerText == null)
            {
                GameObject mObj = new GameObject("TileMarker", typeof(RectTransform), typeof(TextMeshProUGUI));
                mObj.transform.SetParent(transform, false);
                RectTransform mrt = mObj.GetComponent<RectTransform>();
                mrt.anchorMin = Vector2.zero;
                mrt.anchorMax = Vector2.one;
                mrt.offsetMin = Vector2.zero;
                mrt.offsetMax = Vector2.zero;

                _markerText = mObj.GetComponent<TextMeshProUGUI>();
                _markerText.alignment = TextAlignmentOptions.Center;
                _markerText.fontStyle = FontStyles.Bold;
                _markerText.raycastTarget = false;
            }
        }

        public void ApplyVisualTheme()
        {
            if (_tileImage == null) _tileImage = GetComponent<Image>();
            EnsureMarker();

            bool hasValidSprite = _tileImage != null && _tileImage.sprite != null;

            switch (_cellType)
            {
                case GridCellType.ObstacleWall:
                    if (_tileImage != null)
                    {
                        // Красный оттенок для стены-препятствия
                        _tileImage.color = hasValidSprite ? Color.white : new Color(0.55f, 0.12f, 0.16f, 1f);
                    }
                    if (_markerText != null)
                    {
                        _markerText.gameObject.SetActive(!hasValidSprite);
                        _markerText.text = "<color=#FF4455>[X]</color>";
                        _markerText.fontSize = 20;
                    }
                    break;

                case GridCellType.GlitchTrapHazard:
                    if (_tileImage != null)
                    {
                        // Янтарно-желтый оттенок для ловушки
                        _tileImage.color = hasValidSprite ? Color.white : new Color(0.65f, 0.48f, 0.08f, 1f);
                    }
                    if (_markerText != null)
                    {
                        _markerText.gameObject.SetActive(!hasValidSprite);
                        _markerText.text = "<color=#FFD700>[!]</color>";
                        _markerText.fontSize = 20;
                    }
                    break;

                case GridCellType.Goal:
                    if (_tileImage != null)
                    {
                        // Изумрудно-зеленый оттенок для финиша
                        _tileImage.color = hasValidSprite ? Color.white : new Color(0.10f, 0.55f, 0.28f, 1f);
                    }
                    if (_markerText != null)
                    {
                        _markerText.gameObject.SetActive(!hasValidSprite);
                        _markerText.text = "<color=#00FFAA>GOAL</color>";
                        _markerText.fontSize = 15;
                    }
                    break;

                case GridCellType.Walkable:
                default:
                    if (_tileImage != null)
                    {
                        // Темно-синий оттенок для доступного пола
                        _tileImage.color = hasValidSprite ? Color.white : new Color(0.10f, 0.17f, 0.28f, 1f);
                    }
                    if (_markerText != null)
                    {
                        _markerText.gameObject.SetActive(false);
                        _markerText.text = "";
                    }
                    break;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnTileClicked?.Invoke(this);
        }
    }
}
