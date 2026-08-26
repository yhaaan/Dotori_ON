using System;

namespace TeamOverlay.Core
{
    /// <summary>
    /// Immutable snapshot used by the UI. Every timestamp exposed by this type is
    /// normalized to UTC by the constructor.
    /// </summary>
    public sealed class MemberState
    {
        public MemberState(
            string memberId,
            string displayName,
            string avatarKey,
            int sortOrder,
            AttendanceStatus attendanceStatus,
            ActivityStatus? activityStatus,
            ConnectionStatus connectionStatus,
            DateTimeOffset? checkedInAtUtc,
            DateTimeOffset? activityStartedAtUtc,
            DateTimeOffset? lastHeartbeatAtUtc,
            DateTimeOffset? lastCheckedOutAtUtc,
            DateTimeOffset updatedAtUtc,
            string statusNote = null)
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                throw new ArgumentException("A member id is required.", nameof(memberId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A display name is required.", nameof(displayName));
            }

            if (sortOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sortOrder));
            }

            if (attendanceStatus == AttendanceStatus.ClockedIn)
            {
                if (!activityStatus.HasValue)
                {
                    throw new ArgumentException("A clocked-in member must have an activity.", nameof(activityStatus));
                }

                if (!checkedInAtUtc.HasValue)
                {
                    throw new ArgumentException("A clocked-in member must have a check-in time.", nameof(checkedInAtUtc));
                }

                if (!activityStartedAtUtc.HasValue)
                {
                    throw new ArgumentException("A clocked-in member must have an activity start time.", nameof(activityStartedAtUtc));
                }
            }
            else if (activityStatus.HasValue || checkedInAtUtc.HasValue || activityStartedAtUtc.HasValue)
            {
                throw new ArgumentException("A clocked-out member cannot have an active session or activity.");
            }

            MemberId = memberId;
            DisplayName = displayName;
            AvatarKey = avatarKey ?? string.Empty;
            SortOrder = sortOrder;
            AttendanceStatus = attendanceStatus;
            ActivityStatus = activityStatus;
            ConnectionStatus = connectionStatus;
            CheckedInAtUtc = ToUtc(checkedInAtUtc);
            ActivityStartedAtUtc = ToUtc(activityStartedAtUtc);
            LastHeartbeatAtUtc = ToUtc(lastHeartbeatAtUtc);
            LastCheckedOutAtUtc = ToUtc(lastCheckedOutAtUtc);
            UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
            // A note describes current work, so it is meaningless once the
            // session is closed. The server clears it on checkout; dropping it
            // here too keeps a hand-built state from disagreeing.
            StatusNote = attendanceStatus == AttendanceStatus.ClockedIn && !string.IsNullOrWhiteSpace(statusNote)
                ? statusNote.Trim()
                : null;
        }

        public string MemberId { get; }

        public string DisplayName { get; }

        public string AvatarKey { get; }

        public int SortOrder { get; }

        public AttendanceStatus AttendanceStatus { get; }

        public ActivityStatus? ActivityStatus { get; }

        public ConnectionStatus ConnectionStatus { get; }

        public DateTimeOffset? CheckedInAtUtc { get; }

        public DateTimeOffset? ActivityStartedAtUtc { get; }

        public DateTimeOffset? LastHeartbeatAtUtc { get; }

        public DateTimeOffset? LastCheckedOutAtUtc { get; }

        public DateTimeOffset UpdatedAtUtc { get; }

        /// <summary>Optional short note shown on the card while clocked in.</summary>
        public string StatusNote { get; }

        public bool IsClockedIn => AttendanceStatus == AttendanceStatus.ClockedIn;

        /// <summary>
        /// Returns a presentation-only attendance timer. It never mutates backend
        /// state and clamps clock skew to zero.
        /// </summary>
        public TimeSpan GetAttendanceElapsed(DateTimeOffset utcNow)
        {
            if (!CheckedInAtUtc.HasValue)
            {
                return TimeSpan.Zero;
            }

            var elapsed = utcNow.ToUniversalTime() - CheckedInAtUtc.Value;
            return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }

        private static DateTimeOffset? ToUtc(DateTimeOffset? value)
        {
            return value.HasValue ? value.Value.ToUniversalTime() : (DateTimeOffset?)null;
        }
    }
}
