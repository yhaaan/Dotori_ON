using NUnit.Framework;
using DOTORION.Core;

namespace DOTORION.Tests.EditMode
{
    public sealed class IdleActivityPolicyTests
    {
        private const double BreakAfter = 600d;

        private static IdleActivityPolicy Policy() => new IdleActivityPolicy(BreakAfter);

        [Test]
        public void QuietDesk_MovesToBreakOnceAndThenLeavesItAlone()
        {
            var policy = Policy();

            Assert.That(
                policy.Evaluate(BreakAfter, ActivityStatus.Working),
                Is.EqualTo(IdleActivityAction.StartBreak));
            // Every later tick of the same absence has nothing left to do; asking
            // the server again on each one would be a request every few seconds
            // for as long as the person is away.
            Assert.That(
                policy.Evaluate(BreakAfter * 4d, ActivityStatus.Break),
                Is.EqualTo(IdleActivityAction.None));
        }

        [Test]
        public void ShortAbsence_LeavesTheStatusAlone()
        {
            Assert.That(
                Policy().Evaluate(BreakAfter - 1d, ActivityStatus.Working),
                Is.EqualTo(IdleActivityAction.None));
        }

        [Test]
        public void ComingBack_ResumesOnlyTheBreakItStarted()
        {
            var policy = Policy();
            policy.Evaluate(BreakAfter, ActivityStatus.Working);

            Assert.That(
                policy.Evaluate(0d, ActivityStatus.Break),
                Is.EqualTo(IdleActivityAction.ResumeWork));
            Assert.That(policy.IsEngaged, Is.False);
        }

        [Test]
        public void ABreakSomeoneChose_IsNeverResumed()
        {
            var policy = Policy();

            // Already on a break of their own, so the absence changes nothing and
            // there is nothing to undo when the keyboard is touched again.
            Assert.That(
                policy.Evaluate(BreakAfter * 2d, ActivityStatus.Break),
                Is.EqualTo(IdleActivityAction.None));
            Assert.That(
                policy.Evaluate(0d, ActivityStatus.Break),
                Is.EqualTo(IdleActivityAction.None));
        }

        [Test]
        public void PickingAnotherStatusWhileAway_OutranksTheResume()
        {
            var policy = Policy();
            policy.Evaluate(BreakAfter, ActivityStatus.Working);

            // Back at the desk and eating there. Resuming work over that would
            // overrule something the person said about themselves.
            Assert.That(
                policy.Evaluate(0d, ActivityStatus.Meal),
                Is.EqualTo(IdleActivityAction.None));
            Assert.That(policy.IsEngaged, Is.False);
        }

        [Test]
        public void AMealIsNotInterruptedByAQuietDesk()
        {
            Assert.That(
                Policy().Evaluate(BreakAfter * 2d, ActivityStatus.Meal),
                Is.EqualTo(IdleActivityAction.None));
        }

        [Test]
        public void EndingTheSession_ForgetsABreakInProgress()
        {
            var policy = Policy();
            policy.Evaluate(BreakAfter, ActivityStatus.Working);
            policy.Reset();

            // Clocking in again starts on a clean sheet: the new session's break,
            // if it has one, was not this policy's doing.
            Assert.That(
                policy.Evaluate(0d, ActivityStatus.Break),
                Is.EqualTo(IdleActivityAction.None));
        }

        [Test]
        public void AbsenceAfterAFailedChange_IsTriedAgainOnTheNextOne()
        {
            var policy = Policy();
            policy.Evaluate(BreakAfter, ActivityStatus.Working);

            // The request never landed, so the status is still 작업중 when they
            // come back. Nothing to resume, and the next absence asks again.
            Assert.That(
                policy.Evaluate(0d, ActivityStatus.Working),
                Is.EqualTo(IdleActivityAction.None));
            Assert.That(
                policy.Evaluate(BreakAfter, ActivityStatus.Working),
                Is.EqualTo(IdleActivityAction.StartBreak));
        }
    }
}
