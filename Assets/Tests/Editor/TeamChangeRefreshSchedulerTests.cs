using System.Collections.Generic;
using DOTORION.Core;
using NUnit.Framework;

namespace DOTORION.Tests
{
    public sealed class TeamChangeRefreshSchedulerTests
    {
        [Test]
        public void BurstUsesOneFixedWindowAndWaitsUntilReadActuallyStarts()
        {
            var scheduler = new TeamChangeRefreshScheduler();
            var signals = new Signals();
            signals.Items.Enqueue(TeamChangeKind.State);
            scheduler.Observe(signals, 10f);
            Assert.That(scheduler.IsDue(10.29f), Is.False);
            signals.Items.Enqueue(TeamChangeKind.Member);
            signals.Items.Enqueue(TeamChangeKind.Nudge);
            scheduler.Observe(signals, 10.2f);
            Assert.That(signals.Items, Is.Empty);
            Assert.That(scheduler.IsDue(10.31f), Is.True);
            scheduler.Observe(signals, 11f);
            Assert.That(scheduler.IsDue(11f), Is.True, "A skipped refresh must not consume the pending read.");
            scheduler.MarkRefreshStarted();
            Assert.That(scheduler.IsDue(12f), Is.False);
        }

        [Test]
        public void JoinAndRejoinRefreshImmediatelyButDuplicateGenerationDoesNot()
        {
            var scheduler = new TeamChangeRefreshScheduler();
            var signals = new Signals { JoinGeneration = 1 };
            signals.Items.Enqueue(TeamChangeKind.State);
            Assert.That(scheduler.Observe(signals, 0f), Is.True);
            Assert.That(scheduler.IsDue(0f), Is.True);
            scheduler.MarkRefreshStarted();
            Assert.That(scheduler.Observe(signals, 1f), Is.False);
            Assert.That(scheduler.IsDue(1f), Is.False);
            signals.JoinGeneration++;
            Assert.That(scheduler.Observe(signals, 2f), Is.True);
            Assert.That(scheduler.IsDue(2f), Is.True);
        }

        [Test]
        public void SignalsDuringReadAndFailedReadsRemainRetryable()
        {
            var scheduler = new TeamChangeRefreshScheduler();
            var signals = new Signals();
            scheduler.MarkRefreshStarted();
            signals.Items.Enqueue(TeamChangeKind.Nudge);
            scheduler.Observe(signals, 1f);
            Assert.That(scheduler.IsDue(1.31f), Is.True);
            scheduler.MarkRefreshStarted();
            scheduler.RequestRetry(2f);
            Assert.That(scheduler.IsDue(2f), Is.True);
            scheduler.Reset();
            Assert.That(scheduler.Observe(null, 3f), Is.False);
            Assert.That(scheduler.IsDue(3f), Is.False);
            signals.JoinGeneration = 1;
            Assert.That(scheduler.Observe(signals, 4f), Is.True);
        }

        private sealed class Signals : ITeamChangeSignals
        {
            public readonly Queue<TeamChangeKind> Items = new Queue<TeamChangeKind>();
            public bool IsLive => true;
            public int JoinGeneration { get; set; }
            public bool TryDequeue(out TeamChangeKind kind)
            {
                kind = default;
                if (Items.Count == 0) return false;
                kind = Items.Dequeue();
                return true;
            }
        }
    }
}
