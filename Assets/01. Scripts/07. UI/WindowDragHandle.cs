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

        /// <summary>
        /// Makes a full-window modal draggable by any part that is not a control.
        ///
        /// The modals cover the whole window, top bar included, so the overlay's
        /// own drag strip is out of reach while one is up. Each modal prefab has
        /// a <c>WindowDragArea</c> of its own, but it sits under the
        /// <c>ModalBackdrop</c>, which is drawn later, covers the whole window and
        /// takes every click - so the strip never receives one. A handle on the
        /// backdrop catches presses anywhere on the modal instead; buttons and
        /// input fields still get theirs first, because a press goes to the
        /// nearest handler and they are their own.
        /// </summary>
        public static void WireModal(Component modal, Action beginDrag)
        {
            if (modal == null || beginDrag == null)
            {
                return;
            }

            var backdrop = modal.transform.Find("ModalBackdrop");
            if (backdrop != null && backdrop.GetComponent<WindowDragHandle>() == null)
            {
                backdrop.gameObject.AddComponent<WindowDragHandle>();
            }

            foreach (var handle in modal.GetComponentsInChildren<WindowDragHandle>(true))
            {
                handle.Initialize(beginDrag);
            }
        }
    }
}
