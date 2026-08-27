using System;
using System.IO;
using TeamOverlay.Audio;
using TeamOverlay.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.Editor
{
    public static class TeamOverlayPrefabBuilder
    {
        public const string PrefabFolder = "Assets/02. Prefabs";
        public const string CardPath = PrefabFolder + "/TeamMemberCard.prefab";
        public const string MainViewPath = PrefabFolder + "/TeamOverlayCanvas.prefab";
        public const string NameViewPath = PrefabFolder + "/FirstRunNameModal.prefab";

        // Resources.Load resolves paths relative to a folder named exactly
        // "Resources", so this one keeps its engine-given name and sits at the
        // Assets root rather than taking a numbered folder.
        public const string ResourceFolder = "Assets/Resources/TeamOverlay";
        public const string AppPath = ResourceFolder + "/TeamOverlayApp.prefab";
        public const string SoundsPath = ResourceFolder + "/TeamOverlaySounds.asset";

        [MenuItem("Team Overlay/Create Missing Editable UI Prefabs")]
        public static void CreateMissingPrefabs()
        {
            if (AllPrefabsExist())
            {
                Debug.Log("All Team Overlay editable UI prefabs already exist. Nothing was overwritten.");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(MainViewPath);
                return;
            }
            BuildAll();
        }

        [MenuItem("Team Overlay/Rebuild Editable UI Prefabs...")]
        public static void RebuildPrefabsWithConfirmation()
        {
            if (!EditorUtility.DisplayDialog("Rebuild Team Overlay UI prefabs?",
                    "This replaces manual Inspector/layout changes in all four generated prefabs.",
                    "Rebuild", "Cancel")) return;
            BuildAll();
        }

        public static void RebuildPrefabsFromCommandLine() => BuildAll();

        public static void RebuildMainViewFromCommandLine()
        {
            var cardPrefab = AssetDatabase.LoadAssetAtPath<TeamMemberCardView>(CardPath);
            if (cardPrefab == null) throw new InvalidOperationException("Missing member card prefab.");
            var mainPrefab = BuildMainView(cardPrefab);
            AssetDatabase.SaveAssets();
            Selection.activeObject = mainPrefab.gameObject;
        }

        public static bool AllPrefabsExist()
        {
            return File.Exists(CardPath) && File.Exists(MainViewPath) &&
                   File.Exists(NameViewPath) && File.Exists(AppPath);
        }

        private static void BuildAll()
        {
            EnsureFolder("Assets", "02. Prefabs");
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "TeamOverlay");

            var cardPrefab = BuildCard();
            var mainPrefab = BuildMainView(cardPrefab);
            var namePrefab = BuildNameView();
            BuildApp(mainPrefab, namePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = mainPrefab.gameObject;
            Debug.Log("Created editable Team Overlay prefabs. Builds will not regenerate or overwrite them.");
        }

        private static TeamMemberCardView BuildCard()
        {
            var rootImage = UiFactory.CreateImage("TeamMemberCard", null, TeamOverlayPalette.CardOffline);
            var root = rootImage.gameObject;
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(100f, 100f);

            try
            {
                var layout = root.AddComponent<LayoutElement>();
                layout.flexibleWidth = 1f;
                layout.minWidth = 96f;
                var view = root.AddComponent<TeamMemberCardView>();
                var font = PreviewFont();

                var timer = UiFactory.CreateText("ElapsedTimer", root.transform, font, 12,
                    TextAnchor.MiddleCenter, TeamOverlayPalette.TextSecondary, FontStyle.Bold);
                UiFactory.AnchorTop(timer.rectTransform, 4f, 5f, 100f, 18f);
                timer.rectTransform.anchorMax = new Vector2(1f, 1f);
                timer.rectTransform.sizeDelta = new Vector2(-8f, 18f);
                timer.text = "00:42:18";

                var avatar = UiFactory.CreateImage("Avatar", root.transform, TeamOverlayPalette.Working);
                var avatarRect = avatar.rectTransform;
                avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0.5f, 1f);
                avatarRect.pivot = new Vector2(0.5f, 1f);
                avatarRect.anchoredPosition = new Vector2(0f, -25f);
                avatarRect.sizeDelta = new Vector2(38f, 38f);
                var initial = UiFactory.CreateText("Initial", avatar.transform, font, 17,
                    TextAnchor.MiddleCenter, TeamOverlayPalette.TextPrimary, FontStyle.Bold);
                initial.text = "김";
                UiFactory.Stretch(initial.rectTransform);

                var name = UiFactory.CreateText("Name", root.transform, font, 13,
                    TextAnchor.MiddleCenter, TeamOverlayPalette.TextPrimary, FontStyle.Bold);
                SetCardLine(name, 66f, 18f);
                name.text = "김하늘";
                var status = UiFactory.CreateText("Status", root.transform, font, 11,
                    TextAnchor.MiddleCenter, TeamOverlayPalette.Working, FontStyle.Bold);
                SetCardLine(status, 84f, 18f);
                status.text = "작업중";
                var nudge = UiFactory.CreateButton("Nudge", root.transform, font, "\uCF55");
                nudge.GetComponentInChildren<Text>().fontSize = 9;
                var nudgeRect = nudge.GetComponent<RectTransform>();
                nudgeRect.anchorMin = nudgeRect.anchorMax = new Vector2(1f, 1f);
                nudgeRect.pivot = new Vector2(1f, 1f);
                nudgeRect.anchoredPosition = new Vector2(-3f, -3f);
                nudgeRect.sizeDelta = new Vector2(22f, 18f);

                var detail = UiFactory.CreateText("Detail", root.transform, font, 9,
                    TextAnchor.MiddleCenter, TeamOverlayPalette.TextSecondary);
                SetCardLine(detail, 103f, 27f);
                detail.horizontalOverflow = HorizontalWrapMode.Wrap;
                detail.text = "출근 09:00";

                Assign(view,
                    ("_background", rootImage), ("_avatarBackground", avatar),
                    ("_avatarText", initial), ("_timerText", timer), ("_nameText", name),
                    ("_statusText", status), ("_detailText", detail), ("_nudgeButton", nudge));
                return PrefabUtility.SaveAsPrefabAsset(root, CardPath).GetComponent<TeamMemberCardView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static TeamOverlayView BuildMainView(TeamMemberCardView cardPrefab)
        {
            var root = new GameObject("TeamOverlayCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TeamOverlayView));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                ConfigureScaler(root.GetComponent<CanvasScaler>());
                var font = PreviewFont();
                var background = UiFactory.CreateImage("WindowBackground", root.transform, TeamOverlayPalette.Window);
                UiFactory.Stretch(background.rectTransform);
                var topBar = UiFactory.CreateImage("TopBar", background.transform, TeamOverlayPalette.TopBar);
                topBar.rectTransform.anchorMin = new Vector2(0f, 1f);
                topBar.rectTransform.anchorMax = new Vector2(1f, 1f);
                topBar.rectTransform.pivot = new Vector2(0.5f, 1f);
                topBar.rectTransform.sizeDelta = new Vector2(0f, 32f);

                var dragArea = UiFactory.CreateImage("WindowDragArea", topBar.transform, new Color(1f, 1f, 1f, 0.001f));
                UiFactory.Stretch(dragArea.rectTransform, 0f, 0f, 271f, 0f);
                var dragHandle = dragArea.gameObject.AddComponent<WindowDragHandle>();
                var title = UiFactory.CreateText("Title", dragArea.transform, font, 12,
                    TextAnchor.MiddleLeft, TeamOverlayPalette.TextPrimary, FontStyle.Bold);
                title.text = "Dotori ON";
                UiFactory.Stretch(title.rectTransform, 10f, 0f, 0f, 0f);
                var version = UiFactory.CreateText("Version", dragArea.transform, font, 9,
                    TextAnchor.MiddleLeft, TeamOverlayPalette.TextSecondary);
                version.text = "v0.0";
                UiFactory.AnchorTop(version.rectTransform, 82f, 0f, 60f, 32f);

                Button fake = null;
                var teamNudge = TopButton(topBar.transform, font, "TeamNudge", "전체호출", 105f, 52f);
                teamNudge.GetComponentInChildren<Text>().fontSize = 9;
                var switchAccount = TopButton(topBar.transform, font, "SwitchAccount", "이름변경", 161f, 54f);
                var stats = TopButton(topBar.transform, font, "Statistics", "\uD1B5\uACC4", 219f, 48f);
                switchAccount.GetComponentInChildren<Text>().fontSize = 9;
                var topmost = TopButton(topBar.transform, font, "AlwaysOnTop", "TOP", 63f, 38f);
                var minimize = TopButton(topBar.transform, font, "Minimize", "—", 32f, 28f);
                var exit = TopButton(topBar.transform, font, "Exit", "×", 3f, 27f, TeamOverlayPalette.Danger);

                var cardsRoot = UiFactory.CreateRect("MemberCards", background.transform);
                var cardsRect = cardsRoot.GetComponent<RectTransform>();
                cardsRect.anchorMin = new Vector2(0f, 1f);
                cardsRect.anchorMax = new Vector2(1f, 1f);
                cardsRect.pivot = new Vector2(0.5f, 1f);
                cardsRect.anchoredPosition = new Vector2(0f, -36f);
                cardsRect.sizeDelta = new Vector2(-12f, 138f);
                var horizontal = cardsRoot.AddComponent<HorizontalLayoutGroup>();
                horizontal.spacing = 5f;
                horizontal.childAlignment = TextAnchor.UpperCenter;
                horizontal.childControlWidth = horizontal.childControlHeight = true;
                horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = true;
                var cards = new TeamMemberCardView[4];
                for (var i = 0; i < cards.Length; i++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab.gameObject, cardsRoot.transform);
                    instance.name = "MemberCard_" + (i + 1);
                    cards[i] = instance.GetComponent<TeamMemberCardView>();
                }

                var controls = UiFactory.CreateImage("LocalControls", background.transform, TeamOverlayPalette.ControlBar);
                controls.rectTransform.anchorMin = new Vector2(0f, 1f);
                controls.rectTransform.anchorMax = new Vector2(1f, 1f);
                controls.rectTransform.pivot = new Vector2(0.5f, 1f);
                controls.rectTransform.anchoredPosition = new Vector2(0f, -177f);
                controls.rectTransform.sizeDelta = new Vector2(0f, 43f);
                var checkIn = ControlButton(controls.transform, font, "CheckIn", "출근", -54f, 108f);
                var checkOut = ControlButton(controls.transform, font, "CheckOut", "퇴근", -153f, 66f, TeamOverlayPalette.Danger);
                var working = ControlButton(controls.transform, font, "Working", "작업중", -81f, 70f);
                var rest = ControlButton(controls.transform, font, "Break", "쉬는중", -5f, 70f);
                var meal = ControlButton(controls.transform, font, "Meal", "식사중", 71f, 70f);
                var noteBackground = UiFactory.CreateImage("StatusNoteInput", controls.transform, TeamOverlayPalette.Window);
                var noteRect = noteBackground.rectTransform;
                noteRect.anchorMin = noteRect.anchorMax = new Vector2(0.5f, 1f);
                noteRect.pivot = new Vector2(0f, 1f);
                noteRect.anchoredPosition = new Vector2(145f, -4f);
                noteRect.sizeDelta = new Vector2(92f, 27f);
                var noteInput = noteBackground.gameObject.AddComponent<InputField>();
                noteInput.targetGraphic = noteBackground;
                noteInput.lineType = InputField.LineType.SingleLine;
                // Matches the 24-character check on member_current_state.status_note.
                noteInput.characterLimit = 24;
                noteInput.caretColor = TeamOverlayPalette.TextPrimary;
                var noteText = UiFactory.CreateText("Text", noteBackground.transform, font, 10,
                    TextAnchor.MiddleLeft, TeamOverlayPalette.TextPrimary);
                UiFactory.Stretch(noteText.rectTransform, 8f, 2f, 8f, 2f);
                var notePlaceholder = UiFactory.CreateText("Placeholder", noteBackground.transform, font, 10,
                    TextAnchor.MiddleLeft, new Color(TeamOverlayPalette.TextSecondary.r,
                        TeamOverlayPalette.TextSecondary.g, TeamOverlayPalette.TextSecondary.b, 0.68f));
                notePlaceholder.text = "메모";
                notePlaceholder.fontStyle = FontStyle.Italic;
                UiFactory.Stretch(notePlaceholder.rectTransform, 8f, 2f, 8f, 2f);
                noteInput.textComponent = noteText;
                noteInput.placeholder = notePlaceholder;

                var feedback = UiFactory.CreateText("Feedback", controls.transform, font, 8,
                    TextAnchor.LowerCenter, TeamOverlayPalette.TextSecondary);
                feedback.text = "Supabase Auth 연결 · 팀 상태 Mock";
                UiFactory.Stretch(feedback.rectTransform, 4f, 0f, 4f, 31f);

                var statisticsPanel = BuildStatisticsPanel(background.transform, font);
                var view = root.GetComponent<TeamOverlayView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_cards").arraySize = cards.Length;
                for (var i = 0; i < cards.Length; i++) serialized.FindProperty("_cards").GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
                Set(serialized, "_checkInButton", checkIn);
                Set(serialized, "_checkOutButton", checkOut);
                Set(serialized, "_workingButton", working);
                Set(serialized, "_breakButton", rest);
                Set(serialized, "_mealButton", meal);
                Set(serialized, "_fakeEventButton", fake);
                Set(serialized, "_topmostButton", topmost);
                Set(serialized, "_minimizeButton", minimize);
                Set(serialized, "_exitButton", exit);
                Set(serialized, "_switchAccountButton", switchAccount);
                Set(serialized, "_statusNoteInput", noteInput);
                Set(serialized, "_statsButton", stats);
                Set(serialized, "_topmostLabel", topmost.GetComponentInChildren<Text>());
                Set(serialized, "_feedbackText", feedback);
                Set(serialized, "_versionLabel", version);
                Set(serialized, "_teamNudgeButton", teamNudge);
                Set(serialized, "_windowDragHandle", dragHandle);
                Set(serialized, "_statisticsPanel", statisticsPanel);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, MainViewPath).GetComponent<TeamOverlayView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static TeamStatisticsPanelView BuildStatisticsPanel(Transform parent, Font font)
        {
            var panel = UiFactory.CreateImage("StatisticsPanel", parent, TeamOverlayPalette.Window);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -220f);
            // Keep in sync with WindowsOverlayWindow.StatisticsPanelHeight: the
            // window grows by exactly this much when the panel opens.
            panelRect.sizeDelta = new Vector2(0f, 424f);
            var panelView = panel.gameObject.AddComponent<TeamStatisticsPanelView>();

            var heading = UiFactory.CreateText("Heading", panel.transform, font, 14,
                TextAnchor.MiddleLeft, TeamOverlayPalette.TextPrimary, FontStyle.Bold);
            heading.text = "\uD300 \uD1B5\uACC4";
            UiFactory.AnchorTop(heading.rectTransform, 14f, 8f, 150f, 24f);
            var period = UiFactory.CreateText("Period", panel.transform, font, 10,
                TextAnchor.MiddleRight, TeamOverlayPalette.TextSecondary);
            period.text = "2026.08.21 - 2026.08.27";
            UiFactory.AnchorTop(period.rectTransform, 236f, 8f, 230f, 24f);

            var dailyTab = UiFactory.CreateButton("DailyTab", panel.transform, font, "\uB0B4 \uD1B5\uACC4", null,
                TeamOverlayPalette.Accent);
            UiFactory.AnchorTop(dailyTab.GetComponent<RectTransform>(), 14f, 39f, 88f, 28f);
            var rankingTab = UiFactory.CreateButton("RankingTab", panel.transform, font, "\uB7AD\uD0B9");
            UiFactory.AnchorTop(rankingTab.GetComponent<RectTransform>(), 108f, 39f, 88f, 28f);

            // Period sits next to the tabs because it applies to both of them.
            var periodButtons = new Button[3];
            var periodLabels = new[] { "7\uC77C", "\uC774\uBC88 \uB2EC", "\uB204\uC801" };
            var periodNames = new[] { "PeriodSevenDays", "PeriodThisMonth", "PeriodAllTime" };
            for (var index = 0; index < periodButtons.Length; index++)
            {
                periodButtons[index] = UiFactory.CreateButton(
                    periodNames[index], panel.transform, font, periodLabels[index], null,
                    index == 0 ? TeamOverlayPalette.Accent : TeamOverlayPalette.Button);
                periodButtons[index].GetComponentInChildren<Text>().fontSize = 11;
                UiFactory.AnchorTop(
                    periodButtons[index].GetComponent<RectTransform>(), 206f + index * 88f, 39f, 84f, 28f);
            }

            var dailyContent = CreateStatisticsContent("DailyContent", panel.transform);
            var summary = UiFactory.CreateText("Summary", dailyContent.transform, font, 10,
                TextAnchor.MiddleLeft, TeamOverlayPalette.TextSecondary);
            summary.text = "\uD569\uACC4 \uC791\uC5C5 00:00";
            UiFactory.AnchorTop(summary.rectTransform, 10f, 0f, 460f, 20f);
            var statRows = new TeamPeriodStatRowView[7];
            for (var index = 0; index < statRows.Length; index++)
            {
                statRows[index] = BuildPeriodStatRow(dailyContent.transform, font, 24f + index * 42f);
            }

            var rankingContent = CreateStatisticsContent("RankingContent", panel.transform);
            var metricButtons = new Button[4];
            var metricLabels = new[] { "\uC791\uC5C5", "\uCD1D\uC2DC\uAC04", "\uD734\uC2DD", "\uC2DD\uC0AC" };
            var metricNames = new[] { "MetricWork", "MetricAttendance", "MetricBreak", "MetricMeal" };
            for (var index = 0; index < metricButtons.Length; index++)
            {
                metricButtons[index] = UiFactory.CreateButton(
                    metricNames[index], rankingContent.transform, font, metricLabels[index], null,
                    index == 0 ? TeamOverlayPalette.Working : TeamOverlayPalette.Button);
                metricButtons[index].GetComponentInChildren<Text>().fontSize = 11;
                UiFactory.AnchorTop(
                    metricButtons[index].GetComponent<RectTransform>(), 10f + index * 115f, 0f, 111f, 24f);
            }

            var rankingRows = new TeamRankingRowView[4];
            for (var index = 0; index < rankingRows.Length; index++)
            {
                rankingRows[index] = BuildRankingRow(rankingContent.transform, font, 32f + index * 58f);
            }
            rankingContent.SetActive(false);

            var feedback = UiFactory.CreateText("StatisticsFeedback", panel.transform, font, 11,
                TextAnchor.MiddleCenter, TeamOverlayPalette.TextSecondary);
            feedback.horizontalOverflow = HorizontalWrapMode.Wrap;
            feedback.text = "\uD1B5\uACC4\uB97C \uBD88\uB7EC\uC624\uB294 \uC911\u2026";
            UiFactory.AnchorTop(feedback.rectTransform, 30f, 150f, 420f, 70f);
            feedback.gameObject.SetActive(false);

            var serialized = new SerializedObject(panelView);
            Set(serialized, "_dailyTabButton", dailyTab);
            Set(serialized, "_rankingTabButton", rankingTab);
            Set(serialized, "_dailyContent", dailyContent);
            Set(serialized, "_rankingContent", rankingContent);
            Set(serialized, "_periodLabel", period);
            Set(serialized, "_summaryText", summary);
            Set(serialized, "_feedbackText", feedback);
            SetArray(serialized, "_periodButtons", periodButtons);
            SetArray(serialized, "_metricButtons", metricButtons);
            SetArray(serialized, "_statRows", statRows);
            SetArray(serialized, "_rankingRows", rankingRows);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return panelView;
        }

        private static GameObject CreateStatisticsContent(string name, Transform parent)
        {
            var content = UiFactory.CreateRect(name, parent);
            var rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -76f);
            rect.sizeDelta = new Vector2(0f, 340f);
            return content;
        }

        private static TeamPeriodStatRowView BuildPeriodStatRow(Transform parent, Font font, float top)
        {
            var background = UiFactory.CreateImage("StatRow", parent, TeamOverlayPalette.Card);
            UiFactory.AnchorTop(background.rectTransform, 10f, top, 460f, 38f);
            var view = background.gameObject.AddComponent<TeamPeriodStatRowView>();
            var date = UiFactory.CreateText("Date", background.transform, font, 9,
                TextAnchor.MiddleCenter, TeamOverlayPalette.TextPrimary, FontStyle.Bold);
            UiFactory.AnchorTop(date.rectTransform, 6f, 0f, 70f, 38f);
            var work = UiFactory.CreateText("Work", background.transform, font, 9,
                TextAnchor.MiddleLeft, TeamOverlayPalette.Working, FontStyle.Bold);
            UiFactory.AnchorTop(work.rectTransform, 80f, 1f, 72f, 17f);
            var attendance = UiFactory.CreateText("Attendance", background.transform, font, 9,
                TextAnchor.MiddleLeft, TeamOverlayPalette.Accent, FontStyle.Bold);
            UiFactory.AnchorTop(attendance.rectTransform, 274f, 1f, 91f, 17f);
            var other = UiFactory.CreateText("Other", background.transform, font, 9,
                TextAnchor.MiddleLeft, TeamOverlayPalette.TextSecondary);
            UiFactory.AnchorTop(other.rectTransform, 80f, 19f, 248f, 16f);
            var workBar = CreateFilledBar("WorkBar", background.transform, 154f, 7f, 110f,
                TeamOverlayPalette.Working);
            var attendanceBar = CreateFilledBar("AttendanceBar", background.transform, 368f, 7f, 82f,
                TeamOverlayPalette.Accent);
            Assign(view,
                ("_dateLabel", date), ("_workLabel", work), ("_attendanceLabel", attendance),
                ("_otherLabel", other), ("_workBar", workBar), ("_attendanceBar", attendanceBar));
            return view;
        }

        private static TeamRankingRowView BuildRankingRow(Transform parent, Font font, float top)
        {
            var background = UiFactory.CreateImage("RankingRow", parent, TeamOverlayPalette.Card);
            UiFactory.AnchorTop(background.rectTransform, 10f, top, 460f, 50f);
            var view = background.gameObject.AddComponent<TeamRankingRowView>();
            var rank = UiFactory.CreateText("Rank", background.transform, font, 18,
                TextAnchor.MiddleCenter, TeamOverlayPalette.Accent, FontStyle.Bold);
            UiFactory.AnchorTop(rank.rectTransform, 8f, 0f, 32f, 50f);
            var name = UiFactory.CreateText("Name", background.transform, font, 12,
                TextAnchor.MiddleLeft, TeamOverlayPalette.TextPrimary, FontStyle.Bold);
            UiFactory.AnchorTop(name.rectTransform, 46f, 3f, 104f, 22f);
            var work = UiFactory.CreateText("Work", background.transform, font, 10,
                TextAnchor.MiddleLeft, TeamOverlayPalette.Working, FontStyle.Bold);
            UiFactory.AnchorTop(work.rectTransform, 158f, 3f, 96f, 20f);
            var attendance = UiFactory.CreateText("Attendance", background.transform, font, 10,
                TextAnchor.MiddleLeft, TeamOverlayPalette.Accent);
            UiFactory.AnchorTop(attendance.rectTransform, 265f, 3f, 110f, 20f);
            var workBar = CreateFilledBar("WorkBar", background.transform, 158f, 31f, 284f,
                TeamOverlayPalette.Working);
            Assign(view,
                ("_background", background), ("_rankLabel", rank), ("_nameLabel", name),
                ("_workLabel", work), ("_attendanceLabel", attendance), ("_workBar", workBar));
            return view;
        }

        private static Image CreateFilledBar(
            string name,
            Transform parent,
            float left,
            float top,
            float width,
            Color color)
        {
            var track = UiFactory.CreateImage(name + "Track", parent, TeamOverlayPalette.Button);
            UiFactory.AnchorTop(track.rectTransform, left, top, width, 6f);
            var fill = UiFactory.CreateImage(name, track.transform, color);
            UiFactory.Stretch(fill.rectTransform);
            fill.type = Image.Type.Simple;
            fill.rectTransform.anchorMax = new Vector2(0.65f, 1f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.rectTransform.sizeDelta = Vector2.zero;
            return fill;
        }

        private static void SetArray<T>(SerializedObject serialized, string name, T[] values)
            where T : UnityEngine.Object
        {
            var property = serialized.FindProperty(name);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }
        private static FirstRunNameView BuildNameView()
        {
            var root = new GameObject("FirstRunNameModal", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(FirstRunNameView));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 2000;
                ConfigureScaler(root.GetComponent<CanvasScaler>());
                var font = PreviewFont();
                var backdrop = UiFactory.CreateImage("ModalBackdrop", root.transform, new Color(0.025f, 0.035f, 0.055f, 0.88f));
                UiFactory.Stretch(backdrop.rectTransform);
                var panel = UiFactory.CreateImage("NamePanel", backdrop.transform, TeamOverlayPalette.Card);
                var panelRect = panel.rectTransform;
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(372f, 174f);
                var accent = UiFactory.CreateImage("Accent", panel.transform, TeamOverlayPalette.Accent);
                UiFactory.AnchorTop(accent.rectTransform, 0f, 0f, 372f, 3f);
                var title = UiFactory.CreateText("Title", panel.transform, font, 16, TextAnchor.MiddleLeft,
                    TeamOverlayPalette.TextPrimary, FontStyle.Bold);
                title.text = "팀에서 사용할 이름을 알려주세요";
                UiFactory.AnchorTop(title.rectTransform, 18f, 13f, 336f, 25f);
                var description = UiFactory.CreateText("Description", panel.transform, font, 10,
                    TextAnchor.UpperLeft, TeamOverlayPalette.TextSecondary);
                description.text = "다른 팀원에게 표시되는 이름입니다. 한글 이름도 사용할 수 있어요.";
                description.horizontalOverflow = HorizontalWrapMode.Wrap;
                UiFactory.AnchorTop(description.rectTransform, 18f, 42f, 336f, 28f);

                var inputBackground = UiFactory.CreateImage("NameInput", panel.transform, TeamOverlayPalette.ControlBar);
                UiFactory.AnchorTop(inputBackground.rectTransform, 18f, 76f, 248f, 36f);
                var input = inputBackground.gameObject.AddComponent<InputField>();
                input.targetGraphic = inputBackground;
                input.lineType = InputField.LineType.SingleLine;
                input.characterLimit = 32;
                input.caretColor = TeamOverlayPalette.TextPrimary;
                var inputText = UiFactory.CreateText("Text", inputBackground.transform, font, 13,
                    TextAnchor.MiddleLeft, TeamOverlayPalette.TextPrimary);
                UiFactory.Stretch(inputText.rectTransform, 11f, 2f, 9f, 2f);
                var placeholder = UiFactory.CreateText("Placeholder", inputBackground.transform, font, 12,
                    TextAnchor.MiddleLeft, new Color(TeamOverlayPalette.TextSecondary.r, TeamOverlayPalette.TextSecondary.g, TeamOverlayPalette.TextSecondary.b, 0.68f));
                placeholder.text = "예: 김하늘";
                placeholder.fontStyle = FontStyle.Italic;
                UiFactory.Stretch(placeholder.rectTransform, 11f, 2f, 9f, 2f);
                input.textComponent = inputText;
                input.placeholder = placeholder;
                var confirm = UiFactory.CreateButton("Confirm", panel.transform, font, "확인", null, TeamOverlayPalette.Accent);
                UiFactory.AnchorTop(confirm.GetComponent<RectTransform>(), 274f, 76f, 80f, 36f);
                var feedback = UiFactory.CreateText("Feedback", panel.transform, font, 10,
                    TextAnchor.UpperLeft, TeamOverlayPalette.TextSecondary);
                feedback.text = "이름을 입력하면 팀 오버레이를 시작할 수 있어요.";
                feedback.horizontalOverflow = HorizontalWrapMode.Wrap;
                feedback.verticalOverflow = VerticalWrapMode.Overflow;
                UiFactory.AnchorTop(feedback.rectTransform, 18f, 119f, 336f, 35f);
                Assign(root.GetComponent<FirstRunNameView>(), ("_nameInput", input),
                    ("_confirmButton", confirm), ("_confirmLabel", confirm.GetComponentInChildren<Text>()),
                    ("_feedbackText", feedback));
                return PrefabUtility.SaveAsPrefabAsset(root, NameViewPath).GetComponent<FirstRunNameView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildApp(TeamOverlayView mainPrefab, FirstRunNameView namePrefab)
        {
            var root = new GameObject("TeamOverlayApp", typeof(TeamOverlayApp));
            try
            {
                Assign(
                    root.GetComponent<TeamOverlayApp>(),
                    ("_mainViewPrefab", mainPrefab),
                    ("_firstRunNamePrefab", namePrefab),
                    ("_sounds", EnsureSoundsAsset()));
                PrefabUtility.SaveAsPrefabAsset(root, AppPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// Creates the sound settings asset the first time and never touches it
        /// again: it holds hand-picked clips, so a rebuild must not reset it the
        /// way it resets the generated prefabs.
        /// </summary>
        [MenuItem("Team Overlay/Create Missing Sound Settings Asset")]
        public static TeamOverlaySounds EnsureSoundsAsset()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "TeamOverlay");
            var existing = AssetDatabase.LoadAssetAtPath<TeamOverlaySounds>(SoundsPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                return existing;
            }

            var created = ScriptableObject.CreateInstance<TeamOverlaySounds>();
            AssetDatabase.CreateAsset(created, SoundsPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = created;
            Debug.Log("Created " + SoundsPath + ". Drop the team's audio clips into it.");
            return created;
        }

        private static Font PreviewFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480f, 220f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
        }
        private static void SetCardLine(Text text, float top, float height)
        {
            UiFactory.AnchorTop(text.rectTransform, 4f, top, 100f, height);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.sizeDelta = new Vector2(-8f, height);
        }
        private static Button TopButton(Transform parent, Font font, string name, string label,
            float right, float width, Color? color = null)
        {
            var button = UiFactory.CreateButton(name, parent, font, label, null, color);
            UiFactory.AnchorRight(button.GetComponent<RectTransform>(), right, 4f, width, 24f);
            return button;
        }
        private static Button ControlButton(Transform parent, Font font, string name, string label,
            float x, float width, Color? color = null)
        {
            var button = UiFactory.CreateButton(name, parent, font, label, null, color);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -4f);
            rect.sizeDelta = new Vector2(width, 27f);
            return button;
        }
        private static void Assign(UnityEngine.Object target, params (string name, UnityEngine.Object value)[] values)
        {
            var serialized = new SerializedObject(target);
            foreach (var value in values) Set(serialized, value.name, value.value);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException("Missing serialized field " + name);
            property.objectReferenceValue = value;
        }
        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
