using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace KBP.URDT.TestPoligon.Mechanics2D.M36_Match3
{
    /// <summary>
    /// Один самоцвет на игровом поле. Публикует ColorId/Row/Col для AI-игрока.
    /// Управление — перетаскивание в направлении соседа (>= 30 UI-единиц по любой из осей).
    /// </summary>
    public class Match3Gem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private M36_Match3Mechanic _mechanic;
        private Image _image;
        private TMP_Text _label;
        private int _colorId;
        private int _row;
        private int _col;
        private Vector2 _startLocal;

        public int ColorId => _colorId;
        public int Row => _row;
        public int Col => _col;

        public void Bind(M36_Match3Mechanic m, int r, int c) { _mechanic = m; _row = r; _col = c; }
        public void SetVisuals(TMP_Text lbl, Image img) { _label = lbl; _image = img; }
        public void SetCell(int r, int c) { _row = r; _col = c; }

        public void ApplyColor(int colorId, Color hue, char letter)
        {
            _colorId = colorId;
            if (_image != null) _image.color = hue;
            if (_label != null) _label.text = letter.ToString();
        }

        public void OnBeginDrag(PointerEventData ev)
        {
            if (_mechanic == null) return;
            var board = _mechanic.BoardRect;
            if (board == null) return;
            var canvas = _mechanic.Canvas;
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board, ev.position, cam, out _startLocal);
        }

        public void OnDrag(PointerEventData ev) { /* обработка на OnEndDrag */ }

        public void OnEndDrag(PointerEventData ev)
        {
            if (_mechanic == null) return;
            var board = _mechanic.BoardRect;
            if (board == null) return;
            var canvas = _mechanic.Canvas;
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(board, ev.position, cam, out Vector2 endLocal)) return;
            Vector2 delta = endLocal - _startLocal;
            _mechanic.TryPlayerSwap(this, delta);
        }
    }
}
