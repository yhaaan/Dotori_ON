using NUnit.Framework;
using DOTORION.Core;

namespace DOTORION.Tests.EditMode
{
    public sealed class UnreadNoteTrackerTests
    {
        [Test]
        public void AFirstNote_IsNewsAndASecondOneFromTheSamePersonIsNot()
        {
            var tracker = new UnreadNoteTracker();

            // Whether to interrupt anyone hangs on this being true only once.
            Assert.That(tracker.Add("hamcho"), Is.True);
            Assert.That(tracker.Add("hamcho"), Is.False);
            Assert.That(tracker.IsUnread("hamcho"), Is.True);
        }

        [Test]
        public void ReadingTheOverlay_ClearsEveryoneAtOnce()
        {
            var tracker = new UnreadNoteTracker();
            tracker.Add("hamcho");
            tracker.Add("babbird");

            // The notes are all on cards on the same screen, so one look reads
            // them all; clearing them one at a time would leave rows blinking at
            // someone who has already seen them.
            tracker.ClearAll();

            Assert.That(tracker.Count, Is.Zero);
            Assert.That(tracker.IsUnread("hamcho"), Is.False);
        }

        [Test]
        public void ANoteWithdrawnBeforeAnyoneLooked_StopsBlinking()
        {
            var tracker = new UnreadNoteTracker();
            tracker.Add("hamcho");
            tracker.Add("babbird");

            // Only babbird still has a note; hamcho cleared theirs, went home, or
            // left the team. Either way there is nothing left to read.
            tracker.RetainOnly(new[] { "babbird" });

            Assert.That(tracker.IsUnread("hamcho"), Is.False);
            Assert.That(tracker.IsUnread("babbird"), Is.True);
        }

        [Test]
        public void RetainingAgainstNothing_ClearsEverything()
        {
            var tracker = new UnreadNoteTracker();
            tracker.Add("hamcho");

            tracker.RetainOnly(new string[0]);

            Assert.That(tracker.Count, Is.Zero);
        }

        [Test]
        public void AnEmptyMemberId_IsNeverTracked()
        {
            var tracker = new UnreadNoteTracker();

            Assert.That(tracker.Add(null), Is.False);
            Assert.That(tracker.Add(string.Empty), Is.False);
            Assert.That(tracker.Count, Is.Zero);
        }
    }
}
