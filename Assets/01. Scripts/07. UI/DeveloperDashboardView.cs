using System;
using System.Collections.Generic;
using DOTORION.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.UI
{
    /// <summary>
    /// The developer dashboard. It exists so the team's rows can be read without
    /// opening the Supabase console, and it is deliberately plain: a list, a
    /// couple of buttons, and no way to change anything by accident.
    ///
    /// Erasing a member is the one destructive thing here, so it is two presses
    /// with the name spelled out in between. An accidental click cannot do it,
    /// and there is nothing to undo it with.
    /// </summary>
    public sealed class DeveloperDashboardView : MonoBehaviour
    {
        /// <summary>Four members plus room to notice when there are somehow more.</summary>
        public const int RowCount = 6;

        [Header("Prefab references")]
        [SerializeField] private DeveloperDashboardRowView[] _rows = new DeveloperDashboardRowView[RowCount];
        [SerializeField] private Text _feedbackText;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _signOutButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private Text _confirmText;
        [SerializeField] private Button _confirmDeleteButton;
        [SerializeField] private Button _cancelDeleteButton;

        private bool _initialized;
        private string _pendingDeleteMemberId;
        private IReadOnlyList<AdminMemberSummary> _members = Array.Empty<AdminMemberSummary>();

        public event Action RefreshRequested;

        /// <summary>Raised only after the erase has been confirmed by name.</summary>
        public event Action<string> DeleteConfirmed;

        /// <summary>Forgets which member this PC is signed in as.</summary>
        public event Action SignOutRequested;

        public event Action CloseRequested;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _refreshButton?.onClick.AddListener(() => RefreshRequested?.Invoke());
            _signOutButton?.onClick.AddListener(() => SignOutRequested?.Invoke());
            _closeButton?.onClick.AddListener(() => CloseRequested?.Invoke());
            _cancelDeleteButton?.onClick.AddListener(CancelDelete);
            _confirmDeleteButton?.onClick.AddListener(ConfirmDelete);

            foreach (var row in _rows)
            {
                if (row == null) continue;
                row.Initialize();
                row.DeleteRequested += AskToDelete;
            }

            CancelDelete();
        }

        public void Bind(IReadOnlyList<AdminMemberSummary> members, string localMemberId)
        {
            var list = members ?? Array.Empty<AdminMemberSummary>();
            _members = list;
            // A confirmation naming a member who is no longer in the list would
            // be asking about a row that just went away.
            CancelDelete();
            for (var index = 0; index < _rows.Length; index++)
            {
                var row = _rows[index];
                if (row == null) continue;
                if (index >= list.Count)
                {
                    row.Clear();
                    continue;
                }

                row.Bind(
                    list[index],
                    string.Equals(list[index].MemberId, localMemberId, StringComparison.Ordinal));
            }

            ShowFeedback(list.Count + "명", false);
        }

        public void ShowFeedback(string message, bool isError)
        {
            if (_feedbackText == null)
            {
                return;
            }

            _feedbackText.text = message ?? string.Empty;
        }

        public void SetBusy(bool busy)
        {
            if (_refreshButton != null) _refreshButton.interactable = !busy;
            if (_signOutButton != null) _signOutButton.interactable = !busy;
            if (_confirmDeleteButton != null) _confirmDeleteButton.interactable = !busy;
        }

        /// <summary>
        /// Names the member in the confirmation rather than asking "are you sure":
        /// the row that was clicked and the row that will be erased have to be the
        /// same one, and the only way to know is to read the name back.
        /// </summary>
        private void AskToDelete(string memberId)
        {
            var name = NameFor(memberId);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            _pendingDeleteMemberId = memberId;
            if (_confirmText != null)
            {
                _confirmText.text = name + "님의 계정과 모든 기록을 지웁니다. 되돌릴 수 없습니다.";
            }

            _confirmPanel?.SetActive(true);
        }

        private void CancelDelete()
        {
            _pendingDeleteMemberId = null;
            _confirmPanel?.SetActive(false);
        }

        private void ConfirmDelete()
        {
            var memberId = _pendingDeleteMemberId;
            CancelDelete();
            if (!string.IsNullOrEmpty(memberId))
            {
                DeleteConfirmed?.Invoke(memberId);
            }
        }

        private string NameFor(string memberId)
        {
            foreach (var member in _members)
            {
                if (string.Equals(member.MemberId, memberId, StringComparison.Ordinal))
                {
                    return member.DisplayName;
                }
            }

            return null;
        }
    }
}
