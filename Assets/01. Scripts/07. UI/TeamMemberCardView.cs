using System;
using DOTORION.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.UI
{
    /// <summary>The editable per-member card prefab.</summary>
    public sealed class TeamMemberCardView : MonoBehaviour
    {
        private static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9d);

        [Header("Prefab references")]
        [SerializeField] private Image _background;
        [SerializeField] private Image _avatarBackground;
        [SerializeField] private Image _avatarIcon;
        [SerializeField] private Button _avatarButton;
        [SerializeField] private Text _avatarText;
        [SerializeField] private Text _timerText;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _detailText;
        [SerializeField] private Button _nudgeButton;
        [SerializeField] private DoubleClickHandle _nameDoubleClick;

        private string _memberId;
        private TeamAvatarCatalog _avatarCatalog;

        /// <summary>Raised with the bound member's id when the poke button is used.</summary>
        public event Action<string> NudgeRequested;

        /// <summary>Raised when the person clicks their own profile icon to change it.</summary>
        public event Action AvatarEditRequested;

        /// <summary>Raised when the person double clicks their own name to change it.</summary>
        public event Action RenameRequested;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (_nudgeButton != null)
            {
                _nudgeButton.onClick.AddListener(() => NudgeRequested?.Invoke(_memberId));
            }

            if (_avatarButton != null)
            {
                _avatarButton.onClick.AddListener(() => AvatarEditRequested?.Invoke());
            }

            _nameDoubleClick?.Initialize(() => RenameRequested?.Invoke());
        }

        /// <summary>Supplies the artwork the card draws stored avatar keys with.</summary>
        public void SetAvatarCatalog(TeamAvatarCatalog catalog)
        {
            _avatarCatalog = catalog;
        }

        /// <summary>
        /// Only your own icon is clickable. A teammate's is not something you get
        /// to change, and a button that does nothing still eats the click.
        /// </summary>
        public void SetAvatarEditable(bool editable)
        {
            if (_avatarButton != null)
            {
                _avatarButton.enabled = editable;
            }
        }

        /// <summary>
        /// Only your own name. It used to require being clocked out as well,
        /// because renaming was a sign-out and sign-in and a stray double click
        /// would end the session being timed. Renaming happens in place now - the
        /// member id never changes and the session is untouched - so there is
        /// nothing left for that restriction to protect.
        /// </summary>
        public void SetRenameAvailable(bool available)
        {
            if (_nameDoubleClick != null)
            {
                _nameDoubleClick.enabled = available;
            }
        }

        /// <summary>
        /// Shows the poke button only when a poke would actually arrive: not on
        /// your own card, not for a teammate who has gone home, and not while you
        /// are clocked out yourself, which the server refuses anyway.
        /// </summary>
        public void SetNudgeAvailable(bool available)
        {
            if (_nudgeButton != null)
            {
                _nudgeButton.gameObject.SetActive(available);
            }
        }

        private bool _initialized;

        public void Bind(MemberState member, bool isLocalMember, DateTimeOffset nowUtc)
        {
            _memberId = member.MemberId;
            var isOnline = MemberStatusDisplay.IsOnline(member);
            var accent = MemberStatusDisplay.Accent(member);

            // The card's own surface is prefab artwork now, so nothing here
            // repaints it. The avatar tile still takes the status colour: that
            // tile is what the card is read by from across the room.
            _avatarBackground.color = accent;
            BindAvatar(member, isOnline);
            _nameText.text = member.DisplayName;
            _nameText.color = isLocalMember ? DOTORIONPalette.Accent : DOTORIONPalette.TextPrimary;
            _statusText.text = MemberStatusDisplay.Label(member);
            _statusText.color = accent;

            if (isOnline && member.CheckedInAtUtc.HasValue)
            {
                _timerText.text = FormatElapsed(member.GetAttendanceElapsed(nowUtc));
                _timerText.color = DOTORIONPalette.TextPrimary;

                // The note is what the person chose to say about right now, so it
                // outranks the check-in time, which the timer already implies.
                var hasNote = !string.IsNullOrWhiteSpace(member.StatusNote);
                _detailText.text = hasNote
                    ? member.StatusNote
                    : "출근 " + FormatKoreaTime(member.CheckedInAtUtc.Value, includeDate: false);
                _detailText.color = hasNote
                    ? DOTORIONPalette.TextPrimary
                    : DOTORIONPalette.TextSecondary;
            }
            else
            {
                _timerText.text = "--:--:--";
                _timerText.color = DOTORIONPalette.Offline;
                _detailText.text = member.LastCheckedOutAtUtc.HasValue
                    ? "마지막 퇴근\n" + FormatKoreaTime(member.LastCheckedOutAtUtc.Value, includeDate: true)
                    : "출근 기록 없음";
                _detailText.color = DOTORIONPalette.TextSecondary;
            }
        }

        /// <summary>
        /// Draws the picked icon over the status-coloured tile, so the icon never
        /// costs the card its at-a-glance status colour. An unknown key - one
        /// whose sprite a later build dropped - falls back to the name initial
        /// rather than to an empty tile.
        /// </summary>
        private void BindAvatar(MemberState member, bool isOnline)
        {
            var sprite = _avatarCatalog != null ? _avatarCatalog.Find(member.AvatarKey) : null;
            if (_avatarIcon != null)
            {
                _avatarIcon.sprite = sprite;
                _avatarIcon.enabled = sprite != null;
                // Offline cards are dimmed everywhere else too, and a
                // full-strength icon would be the brightest thing on a card that
                // is asleep. Only far enough to read as dimmed, though: the icon
                // is a picture someone chose, so it still has to be recognisable.
                _avatarIcon.color = isOnline ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            }

            var showInitial = sprite == null;
            _avatarText.text = showInitial ? InitialFor(member.DisplayName) : string.Empty;
            _avatarText.enabled = showInitial;
        }

        private static string InitialFor(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName) ? "?" : displayName.Trim().Substring(0, 1);
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            var totalHours = (int)Math.Floor(elapsed.TotalHours);
            return $"{totalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }

        private static string FormatKoreaTime(DateTimeOffset utc, bool includeDate)
        {
            return utc.ToOffset(KoreaOffset).ToString(includeDate ? "MM/dd HH:mm" : "HH:mm");
        }
    }
}
