using System;
using System.Collections.Generic;
using System.Linq;
using DOTORION.Core;

namespace DOTORION.UI
{
    /// <summary>
    /// Left to right, the row reads as the order the day happened in: whoever
    /// arrived first is furthest left, and whoever went home most recently is
    /// furthest right.
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
                .ThenBy(ArrivalKey)
                .ThenBy(member => member.SortOrder)
                .ThenBy(member => member.MemberId, StringComparer.Ordinal)
                .Take(take)
                .ToArray();
        }

        /// <summary>
        /// Online members sort by when they arrived, earliest first. Offline ones
        /// sort by when they left, earliest first, which puts the person who just
        /// went home at the very end. Someone who has not been on at all has not
        /// left either, so they stay ahead of anyone who has - being absent all
        /// day is not the same as having just finished.
        /// </summary>
        private static DateTimeOffset ArrivalKey(MemberState member)
        {
            if (MemberStatusDisplay.IsOnline(member))
            {
                return member.CheckedInAtUtc ?? DateTimeOffset.MaxValue;
            }

            return member.LastCheckedOutAtUtc ?? DateTimeOffset.MinValue;
        }
    }
}
