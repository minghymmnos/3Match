using UnityEngine;
using UnityEngine.EventSystems;

namespace StarManor
{
    /// <summary>棋子的点击/拖拽输入处理。</summary>
    public class PieceInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [HideInInspector] public BoardController controller;
        [HideInInspector] public Vector2Int cell;

        private bool _dragConsumed;
        private Vector2 _totalDrag;

        public void OnPointerDown(PointerEventData e)
        {
            _dragConsumed = false;
            _totalDrag = Vector2.zero;
            if (controller != null) controller.OnPieceDown(cell, e.position);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_dragConsumed || controller == null) return;
            _totalDrag += e.delta;
            if (_totalDrag.magnitude < 36f) return;
            Vector2Int dir;
            if (Mathf.Abs(_totalDrag.x) > Mathf.Abs(_totalDrag.y))
                dir = _totalDrag.x > 0 ? new Vector2Int(1, 0) : new Vector2Int(-1, 0);
            else
                dir = _totalDrag.y > 0 ? new Vector2Int(0, 1) : new Vector2Int(0, -1);
            _dragConsumed = true;
            controller.OnSwipe(cell, dir);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (controller != null) controller.OnPieceUp(cell, e.position, !_dragConsumed);
        }
    }
}
