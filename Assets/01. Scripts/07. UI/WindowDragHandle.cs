using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DOTORION.UI
{
    public sealed class WindowDragHandle : MonoBehaviour, IPointerDownHandler
    {
        private Action _beginDrag;

        public void Initialize(Action beginDrag) => _beginDrag = beginDrag;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _beginDrag?.Invoke();
            }
        }
    }
}
