using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TeamOverlay.UI
{
    /// <summary>
    /// Turns a graphic into a double click target. Disabling the component is
    /// what withholds the gesture: the event system skips disabled behaviours, so
    /// nothing has to re-check who is allowed to use it at click time.
    /// </summary>
    public sealed class DoubleClickHandle : MonoBehaviour, IPointerClickHandler
    {
        private Action _onDoubleClick;

        public void Initialize(Action onDoubleClick) => _onDoubleClick = onDoubleClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            {
                _onDoubleClick?.Invoke();
            }
        }
    }
}
