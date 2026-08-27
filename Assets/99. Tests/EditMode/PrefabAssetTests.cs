using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TeamOverlay.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.Tests.EditMode
{
    public sealed class PrefabAssetTests
    {
        [Test]
        public void EditableUiPrefabs_HaveCompleteSerializedReferences()
        {
            var card = AssetDatabase.LoadAssetAtPath<TeamMemberCardView>(
                "Assets/02. Prefabs/TeamMemberCard.prefab");
            var main = AssetDatabase.LoadAssetAtPath<TeamOverlayView>(
                "Assets/02. Prefabs/TeamOverlayCanvas.prefab");
            var name = AssetDatabase.LoadAssetAtPath<FirstRunNameView>(
                "Assets/02. Prefabs/FirstRunNameModal.prefab");
            var app = AssetDatabase.LoadAssetAtPath<TeamOverlayApp>(
                "Assets/Resources/TeamOverlay/TeamOverlayApp.prefab");

            Assert.That(card, Is.Not.Null);
            Assert.That(main, Is.Not.Null);
            Assert.That(name, Is.Not.Null);
            Assert.That(app, Is.Not.Null);

            var mainData = new SerializedObject(main);
            var cards = mainData.FindProperty("_cards");
            Assert.That(cards.arraySize, Is.EqualTo(4));
            for (var index = 0; index < cards.arraySize; index++)
                Assert.That(cards.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null);
            AssertReference(mainData, "_checkInButton");
            AssertReference(mainData, "_exitButton");
            AssertReference(mainData, "_switchAccountButton");
            AssertReference(mainData, "_statsButton");
            AssertReference(mainData, "_statisticsPanel");
            var statisticsPanel = mainData.FindProperty("_statisticsPanel").objectReferenceValue;
            var statisticsData = new SerializedObject(statisticsPanel);
            AssertReference(statisticsData, "_dailyTabButton");
            AssertReference(statisticsData, "_rankingTabButton");
            AssertReference(statisticsData, "_summaryText");
            Assert.That(statisticsData.FindProperty("_statRows").arraySize, Is.EqualTo(7));
            Assert.That(statisticsData.FindProperty("_rankingRows").arraySize, Is.EqualTo(4));
            Assert.That(statisticsData.FindProperty("_periodButtons").arraySize, Is.EqualTo(3));
            Assert.That(statisticsData.FindProperty("_metricButtons").arraySize, Is.EqualTo(4));
            foreach (var row in main.GetComponentsInChildren<TeamPeriodStatRowView>(true))
            {
                var rowData = new SerializedObject(row);
                AssertReference(rowData, "_dateLabel");
                AssertReference(rowData, "_workLabel");
                AssertReference(rowData, "_attendanceLabel");
                AssertReference(rowData, "_otherLabel");
                AssertReference(rowData, "_workBar");
                AssertReference(rowData, "_attendanceBar");
            }
            Assert.That(main.GetComponentsInChildren<TeamPeriodStatRowView>(true).Length, Is.EqualTo(7));
            foreach (var row in main.GetComponentsInChildren<TeamRankingRowView>(true))
            {
                var rowData = new SerializedObject(row);
                AssertReference(rowData, "_background");
                AssertReference(rowData, "_rankLabel");
                AssertReference(rowData, "_nameLabel");
                AssertReference(rowData, "_workLabel");
                AssertReference(rowData, "_attendanceLabel");
                AssertReference(rowData, "_workBar");
            }
            Assert.That(main.GetComponentsInChildren<TeamRankingRowView>(true).Length, Is.EqualTo(4));
            Assert.That(main.transform.Find("WindowBackground/StatisticsPanel"), Is.Not.Null);
            Assert.That(main.transform.Find("WindowBackground/MemberCards").GetComponent<RectTransform>().anchorMin.y,
                Is.EqualTo(1f));
            Assert.That(main.transform.Find("WindowBackground/LocalControls").GetComponent<RectTransform>().anchorMin.y,
                Is.EqualTo(1f));
            Assert.That(main.transform.Find("WindowBackground/TopBar/FakeCheckIn"), Is.Null);
            var panelRect = main.transform.Find("WindowBackground/StatisticsPanel").GetComponent<RectTransform>();
            Assert.That(panelRect.anchoredPosition.y, Is.EqualTo(-220f));
            // The window grows by exactly the panel height, so the two constants
            // have to agree or the compact layout comes back clipped.
            Assert.That(panelRect.sizeDelta.y, Is.EqualTo(424f));
            AssertReference(mainData, "_versionLabel");
            AssertReference(mainData, "_statusNoteInput");
            AssertReference(mainData, "_windowDragHandle");

            var nameData = new SerializedObject(name);
            AssertReference(nameData, "_nameInput");
            AssertReference(nameData, "_confirmButton");
            AssertReference(nameData, "_feedbackText");

            var appData = new SerializedObject(app);
            Assert.That(appData.FindProperty("_mainViewPrefab").objectReferenceValue, Is.EqualTo(main));
            Assert.That(appData.FindProperty("_firstRunNamePrefab").objectReferenceValue, Is.EqualTo(name));
        }

        private static void AssertReference(SerializedObject data, string propertyName)
        {
            var property = data.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
        }

        [Test]
        public void TeamOverlayCanvas_YamlHasNoDuplicateOrMissingLocalFileIds()
        {
            const string path = "Assets/02. Prefabs/TeamOverlayCanvas.prefab";
            var yaml = File.ReadAllText(path);
            Assert.That(yaml, Does.Not.Contain("m_Script: {fileID: 0}"));
            var definitions = new HashSet<string>();
            foreach (Match match in Regex.Matches(
                         yaml,
                         @"^--- !u!\d+ &(\d+)",
                         RegexOptions.Multiline))
            {
                Assert.That(definitions.Add(match.Groups[1].Value), Is.True,
                    "Duplicate fileID " + match.Groups[1].Value);
            }

            foreach (Match match in Regex.Matches(yaml, @"\{fileID: (\d+)([^}]*)\}"))
            {
                var fileId = match.Groups[1].Value;
                var suffix = match.Groups[2].Value;
                if (fileId == "0" || suffix.Contains("guid:"))
                {
                    continue;
                }

                Assert.That(definitions.Contains(fileId), Is.True,
                    "Missing local fileID " + fileId);
            }
        }

        [TestCase(0, "00:00")]
        [TestCase(3660, "01:01")]
        [TestCase(90061, "25:01")]
        public void StatisticsDuration_UsesHoursAndMinutesWithoutMixingTotals(
            int seconds,
            string expected)
        {
            Assert.That(TeamPeriodStatRowView.FormatDuration(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void StatisticsRows_ShowTotalAttendanceAndResizeBarsByRatio()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/02. Prefabs/TeamOverlayCanvas.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var daily = instance.GetComponentsInChildren<TeamPeriodStatRowView>(true)[0];
                daily.Bind(
                    new TeamOverlay.Core.MemberPeriodStat(
                        new System.DateTime(2026, 8, 27), new System.DateTime(2026, 8, 27),
                        1800, 900, 300, 600),
                    TeamOverlay.Core.StatisticsBucket.Day,
                    1800,
                    3600);
                var dailyData = new SerializedObject(daily);
                var totalLabel = (Text)dailyData.FindProperty("_attendanceLabel").objectReferenceValue;
                var workBar = (Image)dailyData.FindProperty("_workBar").objectReferenceValue;
                var attendanceBar = (Image)dailyData.FindProperty("_attendanceBar").objectReferenceValue;

                Assert.That(totalLabel.text, Is.EqualTo("\uCD1D 00:30"));
                Assert.That(workBar.rectTransform.anchorMax.x, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(attendanceBar.rectTransform.anchorMax.x, Is.EqualTo(0.5f).Within(0.001f));

                daily.Bind(
                    new TeamOverlay.Core.MemberPeriodStat(
                        new System.DateTime(2026, 8, 27), new System.DateTime(2026, 8, 27),
                        0, 0, 0, 0),
                    TeamOverlay.Core.StatisticsBucket.Day,
                    1800,
                    3600);
                Assert.That(workBar.rectTransform.anchorMax.x, Is.Zero);
                Assert.That(attendanceBar.rectTransform.anchorMax.x, Is.Zero);

                var ranking = instance.GetComponentsInChildren<TeamRankingRowView>(true)[0];
                ranking.Bind(
                    1,
                    new TeamOverlay.Core.TeamRankingEntry("member", "name", 0, 900, 1800, 300, 600),
                    TeamOverlay.Core.RankingMetric.Work,
                    1800,
                    false);
                var rankingData = new SerializedObject(ranking);
                var rankingTotal = (Text)rankingData.FindProperty("_attendanceLabel").objectReferenceValue;
                var rankingBar = (Image)rankingData.FindProperty("_workBar").objectReferenceValue;
                Assert.That(rankingTotal.text, Is.EqualTo("\uCD1D 00:30"));
                Assert.That(rankingBar.rectTransform.anchorMax.x, Is.EqualTo(0.5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void StatisticsPanel_ReordersTheRankingLocallyWhenTheMetricChanges()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/02. Prefabs/TeamOverlayCanvas.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponentInChildren<TeamStatisticsPanelView>(true);
                panel.Initialize();
                panel.Bind(
                    TeamOverlay.Core.StatisticsRange.Resolve(
                        TeamOverlay.Core.StatisticsPeriod.LastSevenDays,
                        new System.DateTime(2026, 8, 27)),
                    new[]
                    {
                        new TeamOverlay.Core.MemberPeriodStat(
                            new System.DateTime(2026, 8, 27), new System.DateTime(2026, 8, 27),
                            3600, 1800, 600, 1200)
                    },
                    new[]
                    {
                        new TeamOverlay.Core.TeamRankingEntry("worker", "일벌레", 0, 3600, 7200, 60, 60),
                        new TeamOverlay.Core.TeamRankingEntry("eater", "먹보", 1, 600, 7200, 60, 3000)
                    },
                    "worker");

                var rows = instance.GetComponentsInChildren<TeamRankingRowView>(true);
                var topName = (Text)new SerializedObject(rows[0])
                    .FindProperty("_nameLabel").objectReferenceValue;
                Assert.That(topName.text, Is.EqualTo("\uC77C\uBC8C\uB808"));

                // Every entry already carries all four numbers, so picking another
                // metric must reorder the rows without asking the server again.
                var mealButton = instance.transform
                    .Find("WindowBackground/StatisticsPanel/RankingContent/MetricMeal")
                    .GetComponent<Button>();
                mealButton.onClick.Invoke();

                Assert.That(topName.text, Is.EqualTo("\uBA39\uBCF4"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
