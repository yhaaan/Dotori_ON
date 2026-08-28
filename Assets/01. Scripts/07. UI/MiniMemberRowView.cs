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

        private Color _dotColor = Color.white;
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
            var accent = MemberStatusDisplay.Accent(member);

            if (_nameText != null)
            {
                _nameText.text = member.DisplayName;
                _nameText.color = isOnline
                    ? DOTORIONPalette.TextPrimary
                    : DOTORIONPalette.TextSecondary;
            }

            if (_pill != null) _pill.color = accent;
            // The pill carries the status colour, so its text has to be the dark
            // one. Every online colour in the palette is a light pastel and the
            // usual near-white label disappears into it. The dot stays light for
            // the same reason in reverse: it has to read against the pill.
            _dotColor = DOTORIONPalette.TextPrimary;
            if (_statusText != null)
            {
                _statusText.text = MemberStatusDisplay.Label(member);
                _statusText.color = DOTORIONPalette.Window;
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
            _dot.color = lit ? DOTORIONPalette.Danger : _dotColor;
        }
    }
}
