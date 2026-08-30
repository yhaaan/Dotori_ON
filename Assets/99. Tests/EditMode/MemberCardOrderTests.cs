using System;
using System.Linq;
using DOTORION.Core;
using DOTORION.UI;
using NUnit.Framework;

namespace DOTORION.Tests.EditMode
{
    public sealed class MemberCardOrderTests
    {
        private static readonly DateTimeOffset Morning =
            new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.FromHours(9d));

        [Test]
        public void NobodyIsOn_TheRowKeepsItsSettledOrder()
        {
            Assert.That(Names(Away("A", 0), Away("B", 1), Away("C", 2), Away("D", 3)),
                Is.EqualTo(new[] { "A", "B", "C", "D" }));
        }

        [Test]
        public void ArrivingMovesYouToTheFront()
        {
            Assert.That(
                Names(Away("A", 0), Away("B", 1), Away("C", 2), Here("D", 3, Morning)),
                Is.EqualTo(new[] { "D", "A", "B", "C" }));
        }

        [Test]
        public void TheSecondToArrive_SitsBehindTheFirst()
        {
            // D at nine, B at ten: the row reads as the order they turned up in,
            // not as the order they were listed in.
            Assert.That(
                Names(
                    Away("A", 0),
                    Here("B", 1, Morning.AddHours(1d)),
                    Away("C", 2),
                    Here("D", 3, Morning)),
                Is.EqualTo(new[] { "D", "B", "A", "C" }));
        }

        [Test]
        public void LeavingMovesYouToTheEnd()
        {
            // D goes home. B is still on, and D falls behind the two who have not
            // been on at all: they are absent, D has just finished.
            Assert.That(
                Names(
                    Away("A", 0),
                    Here("B", 1, Morning.AddHours(1d)),
                    Away("C", 2),
                    Left("D", 3, Morning.AddHours(2d))),
                Is.EqualTo(new[] { "B", "A", "C", "D" }));
        }

        [Test]
        public void WhoeverLeftLast_IsFurthestRight()
        {
            Assert.That(
                Names(
                    Left("A", 0, Morning.AddHours(3d)),
                    Left("B", 1, Morning.AddHours(1d)),
                    Away("C", 2),
                    Left("D", 3, Morning.AddHours(2d))),
                Is.EqualTo(new[] { "C", "B", "D", "A" }));
        }

        [Test]
        public void TheRowNeverGrowsPastTheTeam()
        {
            var five = new[] { Away("A", 0), Away("B", 1), Away("C", 2), Away("D", 3), Away("E", 4) };
            Assert.That(MemberCardOrder.Sort(five, 4).Length, Is.EqualTo(4));
        }

        private static string[] Names(params MemberState[] members) =>
            MemberCardOrder.Sort(members, 4).Select(member => member.DisplayName).ToArray();

        private static MemberState Here(string name, int sortOrder, DateTimeOffset checkedInAt) =>
            new MemberState(name, name, null, sortOrder, AttendanceStatus.ClockedIn,
                ActivityStatus.Working, ConnectionStatus.Connected, checkedInAt, checkedInAt,
                checkedInAt, null, checkedInAt);

        private static MemberState Away(string name, int sortOrder) =>
            new MemberState(name, name, null, sortOrder, AttendanceStatus.ClockedOut,
                null, ConnectionStatus.Disconnected, null, null, null, null, Morning);

        private static MemberState Left(string name, int sortOrder, DateTimeOffset checkedOutAt) =>
            new MemberState(name, name, null, sortOrder, AttendanceStatus.ClockedOut,
                null, ConnectionStatus.Disconnected, null, null, null, checkedOutAt, checkedOutAt);
    }
}
