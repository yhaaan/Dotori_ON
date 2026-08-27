using TeamOverlay.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.UI
{
    /// <summary>
    /// One line of the mini overlay: a name and a status pill, and nothing else.
    /// The full card's timer and note are deliberately absent - the mini overlay
    /// exists to be small enough to leave on screen, and everything it drops is a
    /// double click away.
    /// </summary>
    public sealed class MiniMemberRowView : MonoBehaviour
    {
        [Header("Prefab references")]
        [SerializeField] private Text _nameText;
        [SerializeField] private Image _pill;
        [SerializeField] private Image _dot;
        [SerializeField] private Text _statusText;

        public void Bind(MemberState member)
        {
            var isOnline = MemberStatusDisplay.IsOnline(member);
            var accent = MemberStatusDisplay.Accent(member);

            if (_nameText != null)
            {
                _nameText.text = member.DisplayName;
                _nameText.color = isOnline
                    ? TeamOverlayPalette.TextPrimary
                    : TeamOverlayPalette.TextSecondary;
            }

            if (_pill != null) _pill.color = accent;
            // The pill carries the status colour, so its text has to be the dark
            // one. Every online colour in the palette is a light pastel and the
            // usual near-white label disappears into it. The dot stays light for
            // the same reason in reverse: it has to read against the pill.
            if (_dot != null) _dot.color = TeamOverlayPalette.TextPrimary;
            if (_statusText != null)
            {
                _statusText.text = MemberStatusDisplay.Label(member);
                _statusText.color = TeamOverlayPalette.Window;
            }
        }
    }
}
