using System;
using System.Collections.Generic;
using DOTORION.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DOTORION.UI
{
    /// <summary>
    /// The name-and-status only overlay. It replaces the whole window rather than
    /// folding out of it, so it owns its own drag strip: the body is the double
    /// click target that brings the full overlay back, and a strip that started a
    /// window drag on the way down would eat the first of those two clicks.
    /// </summary>
    public sealed class MiniOverlayPanelView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Prefab references")]
        [SerializeField] private MiniMemberRowView[] _rows = new MiniMemberRowView[4];
        [SerializeField] private WindowDragHandle _dragHandle;

        /// <summary>Raised when the body is double clicked to leave the mini overlay.</summary>
        public event Action RestoreRequested;

        public void Initialize(Action beginWindowDrag)
        {
            _dragHandle?.Initialize(beginWindowDrag);
        }

        public void Bind(IReadOnlyList<MemberState> orderedMembers)
        {
            // The overlay is bound five times a second whether it is showing or
            // not, and it is off far more often than on.
            if (!gameObject.activeSelf)
            {
                return;
            }

            for (var index = 0; index < _rows.Length; index++)
            {
                var row = _rows[index];
                if (row == null) continue;
                var hasMember = index < orderedMembers.Count;
                row.gameObject.SetActive(hasMember);
                if (hasMember) row.Bind(orderedMembers[index]);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            {
                RestoreRequested?.Invoke();
            }
        }
    }
}
