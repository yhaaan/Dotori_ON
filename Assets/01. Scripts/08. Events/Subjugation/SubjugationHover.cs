using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace DOTORION.Events.Subjugation
{
    public sealed class SubjugationHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<bool> Changed;
        public void OnPointerEnter(PointerEventData data) => Changed?.Invoke(true);
        public void OnPointerExit(PointerEventData data) => Changed?.Invoke(false);
    }
}
