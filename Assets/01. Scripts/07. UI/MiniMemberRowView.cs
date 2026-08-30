using DOTORION.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.UI
{
    /// <summary>
    /// One line of the mini overlay: a name and a status pill, and nothing else.
    /// The full card's timer and note are deliberately absent - the mini overlay
    /// exists to be small enough to leave on screen, and everything it drops is a
    /// double click away.
    /// </summary>
    public sealed class MiniMemberRowView : MonoBehaviour
    {
        /// <summary>
        /// Slow enough to read as "look at me" rather than as a fault, and fast
        /// enough to catch the eye in a window the size of a business card.
        /// </summary>
        private const float BlinkSeconds = 0.9f;

        [Header("Prefab references")]
        [SerializeField] private Text _nameText;
        [SerializeField] private Image _pill;
        [SerializeField] private Image _dot;
        [SerializeField] private Text _statusText;

        /// <summary>
        /// The same five status colours the card carries. They are separate
        /// fields rather than a shared asset, which means they can drift - and
        /// the two views show the same person at the same moment, so they must
        /// not. Change one, change the other.
        /// </summary>
        [Header("Status colours")]
        [SerializeField] private Color _workingColor = new Color(0.30980393f, 0.81960785f, 0.6313726f, 1f);
        [SerializeField] private Color _breakColor = new Color(0.95686275f, 0.7882353f, 0.3647059f, 1f);
        [SerializeField] private Color _mealColor = new Color(1f, 0.5568628f, 0.44705883f, 1f);
        [SerializeField] private Color _onlineColor = new Color(0.43137255f, 0.65882355f, 0.99607843f, 1f);
        [SerializeField] private Color _offlineColor = new Color(0.4f, 0.4509804f, 0.5254902f, 1f);

        /// <summary>
        /// The pill carries the status colour, so its text has to be the dark
        /// one: every online colour is a light pastel and a near-white label
        /// disappears into it. The dot stays light for the same reason in
        /// reverse - it has to read against the pill.
        /// </summary>
        [Header("Row colours")]
        [SerializeField] private Color _nameOnlineColor = new Color(0.95686275f, 0.96862745f, 0.9843137f, 1f);
        [SerializeField] private Color _nameOfflineColor = new Color(0.6627451f, 0.70980394f, 0.7764706f, 1f);
        [SerializeField] private Color _pillTextColor = new Color(0.0627451f, 0.08627451f, 0.12941177f, 1f);
        [SerializeField] private Color _dotIdleColor = new Color(0.95686275f, 0.96862745f, 0.9843137f, 1f);
        [SerializeField] private Color _dotNoteColor = new Color(0.9137255f, 0.44313726f, 0.44313726f, 1f);

        private bool _hasUnreadNote;

        /// <summary>
        /// <paramref name="hasUnreadNote"/> makes the status dot pulse red. The
        /// note itself has nowhere to go in a row this size, so the dot only says
        /// that there is one; reading it means going back to the full overlay,
        /// which is also what marks it read.
        /// </summary>
        public void Bind(MemberState member, bool hasUnreadNote)
        {
            _hasUnreadNote = hasUnreadNote;
            var isOnline = MemberStatusDisplay.IsOnline(member);

            if (_nameText != null)
            {
                _nameText.text = member.DisplayName;
                _nameText.color = isOnline ? _nameOnlineColor : _nameOfflineColor;
            }

            if (_pill != null) _pill.color = AccentFor(member);
            if (_statusText != null)
            {
                _statusText.text = MemberStatusDisplay.Label(member);
                _statusText.color = _pillTextColor;
            }
        }

        /// <summary>
        /// The dot is driven here rather than in Bind because Bind runs five
        /// times a second, which is neither often enough to blink smoothly nor
        /// regular enough to blink evenly.
        /// </summary>
        private void Update()
        {
            if (_dot == null)
            {
                return;
            }

            var lit = _hasUnreadNote
                && Mathf.Repeat(Time.unscaledTime, BlinkSeconds) < BlinkSeconds * 0.5f;
            _dot.color = lit ? _dotNoteColor : _dotIdleColor;
        }

        /// <summary>
        /// Same rule as <see cref="MemberStatusDisplay.Accent"/>, reading the
        /// colours off this component. The label still comes from there, so the
        /// words and the colour cannot drift apart.
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
    }
}
