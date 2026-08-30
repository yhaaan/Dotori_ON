using System;
using System.Collections.Generic;
using System.Linq;
using DOTORION.Core;

namespace DOTORION.UI
{
    /// <summary>
    /// Left to right, the row reads as how recently someone was here: the
    /// people still in, longest-standing first, then the ones who have gone,
    /// most recently gone first, and last the ones who never came today.
    ///
    /// A card moves only when its owner actually did something, which is the
    /// point. Sorting by anything that drifts - a heartbeat, an activity change,
    /// an updated timestamp - would shuffle cards while people sat still, and a
    /// list that moves on its own is one nobody can find a name in.
    /// </summary>
    public static class MemberCardOrder
    {
        public static MemberState[] Sort(IEnumerable<MemberState> members, int take)
        {
            return (members ?? Enumerable.Empty<MemberState>())
                .OrderBy(member => MemberStatusDisplay.IsOnline(member) ? 0 : 1)
                .ThenBy(PresenceKey)
                .ThenBy(member => member.SortOrder)
                .ThenBy(member => member.MemberId, StringComparer.Ordinal)
                .Take(take)
                .ToArray();
        }

        /// <summary>
        /// Online members sort by when they arrived, earliest first: the longer
        /// you have been here, the further left you sit.
        ///
        /// Offline ones sort by when they left, most recent first, so the person
        /// who has just gone lands right behind the people still in. They were
        /// here a minute ago and are the likeliest to come back, which makes
        /// them the first of the absent worth looking at. Whoever never came
        /// today goes last - being absent all day is further from present than
        /// having just finished.
        ///
        /// Ticks rather than the timestamps, because the two halves run in
        /// opposite directions and one key has to carry both. The sign flip is
        /// what turns "latest first" into an ascending sort.
        /// </summary>
        private static long PresenceKey(MemberState member)
        {
            if (MemberStatusDisplay.IsOnline(member))
            {
                return member.CheckedInAtUtc?.UtcTicks ?? long.MaxValue;
            }

            return member.LastCheckedOutAtUtc.HasValue
                ? -member.LastCheckedOutAtUtc.Value.UtcTicks
                : long.MaxValue;
        }
    }
}
