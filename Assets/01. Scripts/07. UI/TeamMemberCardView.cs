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

        /// <summary>
        /// The object that carries the whole poke widget - frame art and button
        /// together. Left empty it falls back to the button's own object, which
        /// is right only while the button is the entire widget.
        /// </summary>
        [SerializeField] private GameObject _nudgeRoot;
        [SerializeField] private DoubleClickHandle _nameDoubleClick;

        /// <summary>
        /// The status colours live on the card rather than in the palette so
        /// they can be tuned against the artwork without a recompile. They are
        /// the last colours code still paints here: everything else on the card
        /// is whatever the prefab says, but the status has to change while the
        /// app runs, so it cannot be baked into the art.
        /// </summary>
        [Header("Status colours")]
        [SerializeField] private Color _workingColor = new Color(0.30980393f, 0.81960785f, 0.6313726f, 1f);
        [SerializeField] private Color _breakColor = new Color(0.95686275f, 0.7882353f, 0.3647059f, 1f);
        [SerializeField] private Color _mealColor = new Color(1f, 0.5568628f, 0.44705883f, 1f);
        [SerializeField] private Color _onlineColor = new Color(0.43137255f, 0.65882355f, 0.99607843f, 1f);
        [SerializeField] private Color _offlineColor = new Color(0.4f, 0.4509804f, 0.5254902f, 1f);

        /// <summary>
        /// Tints the card's own artwork. White leaves the sprite exactly as it
        /// was drawn, so the offline one is the only colour doing any work here:
        /// it says the card is asleep without a second graphic for it.
        /// </summary>
        [Header("Card colours")]
        [SerializeField] private Color _onlineCardColor = Color.white;
        [SerializeField] private Color _offlineCardColor = new Color(0.65f, 0.65f, 0.65f, 1f);

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
            // Hiding the button alone would leave its frame behind as an empty
            // box, so the whole widget goes together.
            var widget = _nudgeRoot != null
                ? _nudgeRoot
                : (_nudgeButton != null ? _nudgeButton.gameObject : null);
            if (widget != null)
            {
                widget.SetActive(available);
            }
        }

        private bool _initialized;

        public void Bind(MemberState member, DateTimeOffset nowUtc)
        {
            _memberId = member.MemberId;
            var isOnline = MemberStatusDisplay.IsOnline(member);
            var accent = AccentFor(member);

            if (_background != null)
            {
                _background.color = isOnline ? _onlineCardColor : _offlineCardColor;
            }

            // The avatar tile takes the status colour: that tile is what the
            // card is read by from across the room.
            _avatarBackground.color = accent;
            BindAvatar(member, isOnline);
            _nameText.text = member.DisplayName;
            _statusText.text = MemberStatusDisplay.Label(member);
            _statusText.color = accent;

            if (isOnline && member.CheckedInAtUtc.HasValue)
            {
                _timerText.text = FormatElapsed(member.GetAttendanceElapsed(nowUtc));

                // The note is what the person chose to say about right now, so it
                // outranks the check-in time, which the timer already implies.
                var hasNote = !string.IsNullOrWhiteSpace(member.StatusNote);
                _detailText.text = hasNote
                    ? member.StatusNote
                    : "출근 " + FormatKoreaTime(member.CheckedInAtUtc.Value, includeDate: false);
            }
            else
            {
                _timerText.text = "--:--:--";
                _detailText.text = member.LastCheckedOutAtUtc.HasValue
                    ? "마지막 퇴근\n" + FormatKoreaTime(member.LastCheckedOutAtUtc.Value, includeDate: true)
                    : "출근 기록 없음";
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

        /// <summary>
        /// Same rule as <see cref="MemberStatusDisplay.Accent"/>, reading the
        /// colours off this component instead of the palette. The label still
        /// comes from there, so the words and the colour cannot drift apart.
        /// </summary>
        private Color AccentFor(MemberState member)
        {
            if (!MemberStatusDisplay.IsOnline(member))
            {
                return _offlineColor;
            }

            switch (member.ActivityStatus)
            {
                case ActivityStatus.Working: return _workingColor;
                case ActivityStatus.Break: return _breakColor;
                case ActivityStatus.Meal: return _mealColor;
                default: return _onlineColor;
            }
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
