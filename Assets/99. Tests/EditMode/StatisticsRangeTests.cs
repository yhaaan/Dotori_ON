using System;
using NUnit.Framework;
using TeamOverlay.Core;

namespace TeamOverlay.Tests.EditMode
{
    public sealed class StatisticsRangeTests
    {
        [Test]
        public void LastSevenDays_CoversTodayAndTheSixDaysBeforeItByDay()
        {
            var range = StatisticsRange.Resolve(
                StatisticsPeriod.LastSevenDays,
                new DateTime(2026, 8, 27));

            Assert.That(range.FromLocalDate, Is.EqualTo(new DateTime(2026, 8, 21)));
            Assert.That(range.ToLocalDate, Is.EqualTo(new DateTime(2026, 8, 27)));
            Assert.That(range.Bucket, Is.EqualTo(StatisticsBucket.Day));
        }

        [Test]
        public void ThisMonth_StartsOnTheFirstAndKeepsDays()
        {
            // The month is drawn as a calendar, so every day needs its own bucket.
            // A grid has room for thirty-one squares where a list had room for
            // seven rows.
            var range = StatisticsRange.Resolve(
                StatisticsPeriod.ThisMonth,
                new DateTime(2026, 8, 27, 23, 30, 0));

            Assert.That(range.FromLocalDate, Is.EqualTo(new DateTime(2026, 8, 1)));
            Assert.That(range.ToLocalDate, Is.EqualTo(new DateTime(2026, 8, 27)));
            Assert.That(range.Bucket, Is.EqualTo(StatisticsBucket.Day));
        }

        [Test]
        public void AllTime_LeavesTheStartToTheServerAndGroupsByMonth()
        {
            // The client has no way to know when the team started; a hardcoded
            // epoch would silently drop everything before it.
            var range = StatisticsRange.Resolve(
                StatisticsPeriod.AllTime,
                new DateTime(2026, 8, 27));

            Assert.That(range.FromLocalDate, Is.Null);
            Assert.That(range.ToLocalDate, Is.EqualTo(new DateTime(2026, 8, 27)));
            Assert.That(range.Bucket, Is.EqualTo(StatisticsBucket.Month));
        }

        [Test]
        public void Resolve_DropsTheTimeOfDaySoARequestNeverStraddlesTwoDates()
        {
            var range = StatisticsRange.Resolve(
                StatisticsPeriod.LastSevenDays,
                new DateTime(2026, 1, 1, 0, 30, 0));

            Assert.That(range.ToLocalDate, Is.EqualTo(new DateTime(2026, 1, 1)));
            Assert.That(range.FromLocalDate, Is.EqualTo(new DateTime(2025, 12, 26)));
        }

        [Test]
        public void RankingEntry_ReportsTheSecondsOfEveryMetric()
        {
            var entry = new TeamRankingEntry("member", "이름", 2, 900, 3600, 300, 600);

            Assert.That(entry.SecondsFor(RankingMetric.Work), Is.EqualTo(900));
            Assert.That(entry.SecondsFor(RankingMetric.Attendance), Is.EqualTo(3600));
            Assert.That(entry.SecondsFor(RankingMetric.Break), Is.EqualTo(300));
            Assert.That(entry.SecondsFor(RankingMetric.Meal), Is.EqualTo(600));
        }
    }
}
