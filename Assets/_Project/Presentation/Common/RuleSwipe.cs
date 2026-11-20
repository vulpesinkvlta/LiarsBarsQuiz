using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LiarsBar.Presentation.Common
{
    public sealed class RuleSwipe : MonoBehaviour, IDragHandler, IEndDragHandler
    {
        public Action<int> Swiped;
        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            var delta = eventData.position - eventData.pressPosition;
            if (Mathf.Abs(delta.x) > Screen.width * 0.06f && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                Swiped?.Invoke(delta.x < 0 ? 1 : -1);
        }
    }
}
