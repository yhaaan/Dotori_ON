using System;
using NUnit.Framework;
using DOTORION.Core;
using DOTORION.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DOTORION.Tests.EditMode
{
    public sealed class TeamCalendarViewTests
    {
        // August 2026 starts on a Saturday, which is the sixth column of a
        // Monday-first week and the case a Sunday-first grid gets wrong.
        private static readonly DateTime August = new DateTime(2026, 8, 1);

        private static GameObject Canvas()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/02. Prefabs/DOTORIONCanvas.prefab");
            Assert.That(prefab, Is.Not.Null);
            return UnityEngine.Object.Instantiate(prefab);
        }

        private static TeamCalendarDayView[] Cells(GameObject canvas)
        {
            var calendar = canvas.GetComponentInChildren<TeamCalendarView>(true);
            Assert.That(calendar, Is.Not.Null);
            var cells = new SerializedObject(calendar).FindProperty("_cells");
            var result = new TeamCalendarDayView[cells.arraySize];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = (TeamCalendarDayView)cells.GetArrayElementAtIndex(index).objectReferenceValue;
            }

            return result;
        }

        private static string DayLabel(TeamCalendarDayView cell)
        {
            return ((Text)new SerializedObject(cell).FindProperty("_dayLabel").objectReferenceValue).text;
        }

        private static string DurationLabel(TeamCalendarDayView cell)
        {
            return ((Text)new SerializedObject(cell).FindProperty("_durationLabel").objectReferenceValue).text;
        }

        private static MemberPeriodStat Day(int dayOfMonth, int workSeconds)
        {
            var date = new DateTime(2026, 8, dayOfMonth);
            // Attendance first: the square shows the total until it is clicked.
            return new MemberPeriodStat(date, date, workSeconds, workSeconds, 0, 0);
        }

        [Test]
        public void TheFirstOfTheMonth_LandsUnderItsWeekday()
        {
            var canvas = Canvas();
            try
            {
                var calendar = canvas.GetComponentInChildren<TeamCalendarView>(true);
                calendar.Bind(
                    StatisticsRange.Resolve(StatisticsPeriod.ThisMonth, new DateTime(2026, 8, 27)),
                    new[] { Day(1, 3600) });

                var cells = Cells(canvas);
                // 월 화 수 목 금 토 일, so a Saturday is column five and the five
                // squares before it belong to July.
                Assert.That(cells[5].gameObject.activeSelf, Is.True);
                Assert.That(DayLabel(cells[5]), Is.EqualTo("1"));
                for (var index = 0; index < 5; index++)
                {
                    Assert.That(cells[index].gameObject.activeSelf, Is.False, "cell " + index);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void TheMonthStopsAtItsLastDay()
        {
            var canvas = Canvas();
            try
            {
                var calendar = canvas.GetComponentInChildren<TeamCalendarView>(true);
                calendar.Bind(
                    StatisticsRange.Resolve(StatisticsPeriod.ThisMonth, new DateTime(2026, 8, 27)),
                    Array.Empty<MemberPeriodStat>());

                var cells = Cells(canvas);
                // Offset five plus thirty-one days ends on square thirty-five.
                Assert.That(DayLabel(cells[35]), Is.EqualTo("31"));
                Assert.That(cells[36].gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void ADayWithNothingOnIt_IsLeftBlankRatherThanZeroed()
        {
            var canvas = Canvas();
            try
            {
                var calendar = canvas.GetComponentInChildren<TeamCalendarView>(true);
                calendar.Bind(
                    StatisticsRange.Resolve(StatisticsPeriod.ThisMonth, new DateTime(2026, 8, 27)),
                    new[] { Day(3, 7200) });

                var cells = Cells(canvas);
                // The 3rd is a Monday: offset five plus two squares.
                Assert.That(DurationLabel(cells[7]), Is.EqualTo("02:00"));
                // The 2nd has no record, and 00:00 everywhere would bury the days
                // that did happen.
                Assert.That(DurationLabel(cells[6]), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void ABucketWiderThanADay_DrawsNothingRatherThanGuessing()
        {
            var canvas = Canvas();
            try
            {
                var calendar = canvas.GetComponentInChildren<TeamCalendarView>(true);
                // A weekly total has no single square it belongs in.
                calendar.Bind(
                    StatisticsRange.Resolve(StatisticsPeriod.AllTime, new DateTime(2026, 8, 27)),
                    new[] { Day(1, 3600) });

                foreach (var cell in Cells(canvas))
                {
                    Assert.That(cell.gameObject.activeSelf, Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void ClickingASquare_SwapsEverySquareBetweenTheTotalAndTheBreakdown()
        {
            var canvas = Canvas();
            try
            {
                var calendar = canvas.GetComponentInChildren<TeamCalendarView>(true);
                calendar.Initialize();
                var date = new DateTime(2026, 8, 3);
                calendar.Bind(
                    StatisticsRange.Resolve(StatisticsPeriod.ThisMonth, new DateTime(2026, 8, 27)),
                    new[] { new MemberPeriodStat(date, date, 32400, 20400, 4200, 3000) });

                var cells = Cells(canvas);
                // The 3rd is a Monday: offset five plus two squares. The square
                // opens on the total, which is the whole open-to-close span.
                Assert.That(DurationLabel(cells[7]), Is.EqualTo("09:00"));

                cells[7].OnPointerClick(new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Left
                });

                // Work, break and meal, coloured rather than labelled because
                // three labels do not fit a square this size.
                var breakdown = DurationLabel(cells[7]);
                Assert.That(breakdown, Does.Contain("05:40"));
                Assert.That(breakdown, Does.Contain("01:10"));
                Assert.That(breakdown, Does.Contain("00:50"));
                Assert.That(breakdown, Does.Not.Contain("09:00"));

                // Any square toggles, so the same one puts it back.
                cells[7].OnPointerClick(new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Left
                });
                Assert.That(DurationLabel(cells[7]), Is.EqualTo("09:00"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void EveryMonthFitsTheGrid()
        {
            // The worst case is a 31 day month starting on a Sunday: offset six
            // plus thirty-one squares needs all six rows.
            for (var month = 1; month <= 12; month++)
            {
                var first = new DateTime(2026, month, 1);
                var offset = ((int)first.DayOfWeek + 6) % TeamCalendarView.DaysPerWeek;
                Assert.That(
                    offset + DateTime.DaysInMonth(2026, month),
                    Is.LessThanOrEqualTo(TeamCalendarView.CellCount),
                    first.ToString("yyyy-MM"));
            }
        }

        [Test]
        public void AugustTwentySixStartsOnSaturday()
        {
            // The month the cases above are pinned to. If this ever fails they are
            // testing a different calendar than the one they describe.
            Assert.That(August.DayOfWeek, Is.EqualTo(DayOfWeek.Saturday));
        }
    }
}
