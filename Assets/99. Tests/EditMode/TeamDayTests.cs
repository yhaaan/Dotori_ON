using System;
using NUnit.Framework;
using DOTORION.Core;

namespace DOTORION.Tests.EditMode
{
    public sealed class TeamDayTests
    {
        [Test]
        public void SixInTheMorning_IsWhereTheNewDayBegins()
        {
            Assert.That(
                TeamDay.DateFor(new DateTime(2026, 8, 29, 6, 0, 0)),
                Is.EqualTo(new DateTime(2026, 8, 29)));
            // One minute earlier still belongs to the night before.
            Assert.That(
                TeamDay.DateFor(new DateTime(2026, 8, 29, 5, 59, 0)),
                Is.EqualTo(new DateTime(2026, 8, 28)));
        }

        [Test]
        public void TheSmallHours_BelongToTheDayThatStartedThem()
        {
            // The case the boundary was moved for: still at the desk at three in
            // the morning, which is the 28th's evening rather than the 29th.
            Assert.That(
                TeamDay.DateFor(new DateTime(2026, 8, 29, 3, 0, 0)),
                Is.EqualTo(new DateTime(2026, 8, 28)));
            Assert.That(
                TeamDay.DateFor(new DateTime(2026, 8, 29, 0, 0, 1)),
                Is.EqualTo(new DateTime(2026, 8, 28)));
        }

        [Test]
        public void DaytimeAndEvening_ReadAsTheCalendarDay()
        {
            Assert.That(
                TeamDay.DateFor(new DateTime(2026, 8, 29, 14, 0, 0)),
                Is.EqualTo(new DateTime(2026, 8, 29)));
            Assert.That(
                TeamDay.DateFor(new DateTime(2026, 8, 29, 23, 59, 59)),
                Is.EqualTo(new DateTime(2026, 8, 29)));
        }

        [Test]
        public void TheBoundaryMovesMonthsAndYearsToo()
        {
            // 03:00 on the first is the last day of the month before, which is
            // what decides which month the calendar asks the server for.
            Assert.That(
                TeamDay.DateFor(new DateTime(2026, 9, 1, 3, 0, 0)),
                Is.EqualTo(new DateTime(2026, 8, 31)));
            Assert.That(
                TeamDay.DateFor(new DateTime(2027, 1, 1, 4, 30, 0)),
                Is.EqualTo(new DateTime(2026, 12, 31)));
        }

        [Test]
        public void TheClientAgreesWithTheServersOffset()
        {
            // team_day_offset() is interval '6 hours'. If one side moves the
            // other has to, or the app asks about a day the server files
            // somewhere else.
            Assert.That(TeamDay.StartHour, Is.EqualTo(6));
        }
    }
}
