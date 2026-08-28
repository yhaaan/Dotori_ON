using System;
using System.Collections.Generic;

namespace DOTORION.Core
{
    /// <summary>
    /// Which teammates have left a status note nobody has read yet.
    ///
    /// A note is the one thing a teammate writes for the others, and it is the
    /// one thing the mini overlay cannot show - there is no room for it beside a
    /// name and a status. So it is tracked instead: the mini row marks who wrote
    /// one, and the taskbar says so when the window is not being looked at.
    ///
    /// Read means read. The moment the full overlay is in front of someone, the
    /// note is on the card they are looking at, so everything pending clears at
    /// once rather than one row at a time.
    /// </summary>
    public sealed class UnreadNoteTracker
    {
        private readonly HashSet<string> _unread = new HashSet<string>(StringComparer.Ordinal);

        public int Count => _unread.Count;

        /// <summary>True when this is news, which is what decides whether to interrupt anyone.</summary>
        public bool Add(string memberId)
        {
            return !string.IsNullOrEmpty(memberId) && _unread.Add(memberId);
        }

        public bool IsUnread(string memberId)
        {
            return !string.IsNullOrEmpty(memberId) && _unread.Contains(memberId);
        }

        public void ClearAll()
        {
            _unread.Clear();
        }

        /// <summary>
        /// Drops anyone who no longer has a note to read - they cleared it, went
        /// home, or left the team. Without this a note that was withdrawn before
        /// anyone looked would blink forever.
        /// </summary>
        public void RetainOnly(IEnumerable<string> memberIdsWithNotes)
        {
            if (_unread.Count == 0)
            {
                return;
            }

            var keep = new HashSet<string>(memberIdsWithNotes ?? Array.Empty<string>(), StringComparer.Ordinal);
            _unread.IntersectWith(keep);
        }
    }
}
