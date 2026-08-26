using System;

namespace TeamOverlay.Core
{
    /// <summary>
    /// A realtime domain event. State is the complete member snapshot immediately
    /// after the mutation, so a subscriber does not need to race a second query.
    /// </summary>
    public sealed class TeamEvent
    {
        public TeamEvent(
            string eventId,
            TeamEventType type,
            string actorMemberId,
            DateTimeOffset occurredAtUtc,
            MemberState state,
            ActivityStatus? activityStatus = null,
            CheckoutReason? checkoutReason = null,
            string targetMemberId = null)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new ArgumentException("An event id is required.", nameof(eventId));
            }

            if (string.IsNullOrWhiteSpace(actorMemberId))
            {
                throw new ArgumentException("An actor member id is required.", nameof(actorMemberId));
            }

            EventId = eventId;
            Type = type;
            ActorMemberId = actorMemberId;
            TargetMemberId = targetMemberId;
            OccurredAtUtc = occurredAtUtc.ToUniversalTime();
            State = state ?? throw new ArgumentNullException(nameof(state));
            ActivityStatus = activityStatus;
            CheckoutReason = checkoutReason;
        }

        public string EventId { get; }

        public TeamEventType Type { get; }

        public string ActorMemberId { get; }

        public string TargetMemberId { get; }

        public DateTimeOffset OccurredAtUtc { get; }

        public MemberState State { get; }

        public ActivityStatus? ActivityStatus { get; }

        public CheckoutReason? CheckoutReason { get; }
    }
}
