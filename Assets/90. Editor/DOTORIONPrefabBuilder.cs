using System;
using System.Collections.Generic;
using System.IO;
using DOTORION.Audio;
using DOTORION.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.Editor
{
    public static class DOTORIONPrefabBuilder
    {
        public const string PrefabFolder = "Assets/02. Prefabs";
        public const string CardPath = PrefabFolder + "/TeamMemberCard.prefab";
        public const string MainViewPath = PrefabFolder + "/DOTORIONCanvas.prefab";
        public const string NameViewPath = PrefabFolder + "/FirstRunNameModal.prefab";
        public const string UpdatePromptPath = PrefabFolder + "/UpdatePromptModal.prefab";

        // Resources.Load resolves paths relative to a folder named exactly
        // "Resources", so this one keeps its engine-given name and sits at the
        // Assets root rather than taking a numbered folder.
        public const string ResourceFolder = "Assets/Resources/DOTORION";
        public const string AppPath = ResourceFolder + "/DOTORIONApp.prefab";
        public const string SoundsPath = ResourceFolder + "/DOTORIONSounds.asset";
        public const string AvatarCatalogPath = ResourceFolder + "/TeamAvatarCatalog.asset";

        /// <summary>Where the team drops profile icon images.</summary>
        public const string AvatarSpriteFolder = "Assets/04. Avatars";

        /// <summary>
        /// The avatar picker's own height in the prefab. The window grows upwards
        /// by exactly this much, so it has to match
        /// <c>WindowsOverlayWindow.AvatarPickerPanelHeight</c>; PrefabAssetTests
        /// pins the pair. Two rows of cells plus the heading fit in it.
        /// </summary>
        public const float AvatarPickerPanelHeight = 160f;

        /// <summary>
        /// The mini overlay's size in the prefab. The window is resized to exactly
        /// this, so it has to match <c>WindowsOverlayWindow.MiniWindowWidth</c>
        /// and <c>MiniWindowHeight</c>; PrefabAssetTests pins the pairs. It is
        /// authored in real pixels rather than in the 480 wide reference space,
        /// because the canvas scaler is switched off while the mini overlay shows.
        /// Narrow enough to live down the side of a screen: the rows carry the
        /// name inside the status pill, so there is nothing to put side by side.
        /// </summary>
        public const float MiniPanelWidth = 75f;

        public const float MiniPanelHeight = 130f;

        /// <summary>
        /// The developer dashboard's height in the prefab. The window grows by
        /// exactly this much, so it has to match
        /// <c>WindowsOverlayWindow.DashboardPanelHeight</c>; PrefabAssetTests pins
        /// the pair. Six rows plus a header, a footer and the confirmation.
        /// </summary>
        public const float DashboardPanelHeight = 300f;

        /// <summary>
        /// The settings panel's own height in the prefab. The window grows
        /// downwards by exactly this much, so it has to match
        /// <c>WindowsOverlayWindow.SettingsPanelHeight</c>; PrefabAssetTests pins
        /// the pair. The heading and four rows fit in it.
        /// </summary>
        public const float SettingsPanelHeight = 196f;

        /// <summary>Shared with the migration so the two rows read identically.</summary>
        public const string AutoStartRowLabel = "자동 시작";

        /// <summary>Shared with the migration so the two rows read identically.</summary>
        public const string AutoStartRowHint = "윈도우를 켤 때 같이 실행합니다.";

        /// <summary>Settings panel geometry, in pixels from the top of the panel.</summary>
        private const float SettingsRowTop = 44f;
        private const float SettingsRowHeight = 28f;
        private const float SettingsRowSpacing = 8f;

        /// <summary>
        /// How far down the next settings row starts. Public because the
        /// migration that adds a row to a prefab already in hand has to move the
        /// rows below it by exactly this much, and guessing it twice is how the
        /// two drift apart.
        /// </summary>
        public const float SettingsRowStep = SettingsRowHeight + SettingsRowSpacing;

        private const float DashboardRowTop = 60f;
        private const float DashboardRowHeight = 32f;
        private const float DashboardRowSpacing = 2f;

        /// <summary>Mini overlay geometry, in pixels from the top of the panel.</summary>
        internal const float MiniDragStripHeight = 18f;
        internal const float MiniRowTop = 21f;
        internal const float MiniRowHeight = 25f;
        internal const float MiniRowSpacing = 2f;
        internal const int MiniRowCount = 4;
        /// <summary>
        /// Month calendar geometry, in pixels inside the statistics content area.
        /// Seven columns across the panel's 480 with a pixel between them, and six
        /// rows that fit the 340 the content area has.
        /// </summary>
        private const float CalendarTop = 24f;
        private const float CalendarHeight = 306f;
        private const float CalendarHeaderHeight = 14f;
        private const float CalendarLeft = 9f;
        private const float CalendarCellWidth = 65f;
        private const float CalendarCellHeight = 46f;
        private const float CalendarCellGap = 1f;

        /// <summary>
        /// How large a profile icon is drawn, on the card and in the picker
        /// alike. The icon fills its tile edge to edge, so this is also the tile
        /// size on the card. Pixel art divides into it cleanly at 24 (x2) and 16
        /// (x3); 32 would land on a half pixel.
        /// </summary>
        internal const float AvatarIconSize = 48f;

        /// <summary>
        /// The size one card actually gets on screen, and therefore the size its
        /// artwork is drawn at. Four cards plus three 5px gaps have to land on
        /// whole pixels or the pixel art resamples: 4*113 + 3*5 = 467, which is
        /// why the row is 467 wide inside a 480 window rather than a rounder 468.
        /// </summary>
        internal const float CardWidth = 113f;
        internal const float CardHeight = 138f;
        internal const float CardRowSpacing = 5f;

        /// <summary>Card geometry, in pixels from the top of the card.</summary>
        internal const float CardAvatarTop = 24f;
        internal const float CardNameTop = 73f;
        internal const float CardStatusTop = 94f;
        internal const float CardDetailTop = 110f;

        /// <summary>
        /// The picker cell is a little larger than the icon so the selected
        /// colour survives as a ring around it. An icon that filled the cell
        /// would hide the only mark saying which one is yours.
        /// </summary>
        internal const float AvatarCellPadding = 2f;
        internal const float AvatarCellSize = AvatarIconSize + (AvatarCellPadding * 2f);
        internal const float AvatarCellSpacing = 4f;
        internal const int AvatarGridColumns = 8;

        /// <summary>
        /// Safe to click: it only fills in prefabs that are missing entirely and
        /// never overwrites one that exists.
        /// </summary>
        [MenuItem("DOTORI ON/Create Missing Editable UI Prefabs")]
        public static void CreateMissingPrefabs()
        {
            if (AllPrefabsExist())
            {
                Debug.Log("All DOTORI ON editable UI prefabs already exist. Nothing was overwritten.");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(MainViewPath);
                return;
            }
            BuildAll();
        }

        /// <summary>
        /// Regenerating a prefab throws away everything an artist put into it -
        /// sprites, nine-slice borders, hand-tuned rects - and the prefabs now
        /// carry artwork that exists nowhere else. So the rebuilds are no longer
        /// menu items: nothing in the DOTORI ON menu can destroy that work by
        /// being clicked. They are still callable by name from a script or the
        /// command line for the rare deliberate reset.
        /// </summary>
        public static void RebuildPrefabsFromCommandLine() => BuildAll();

        /// <summary>
        /// Rebuilds the two prefabs a UI change normally touches, and only those.
        /// The name modal and the app prefab are generated too, so a full rebuild
        /// churns their YAML alongside a change that never involved them.
        /// </summary>
        public static void RebuildCardAndMainView()
        {
            BuildMainView(BuildCard());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(MainViewPath);
        }

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
                   File.Exists(NameViewPath) && File.Exists(AppPath) &&
                   File.Exists(UpdatePromptPath);
        }

        private static void BuildAll()
        {
            EnsureFolder("Assets", "02. Prefabs");
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "DOTORION");

            var cardPrefab = BuildCard();
            var mainPrefab = BuildMainView(cardPrefab);
            var namePrefab = BuildNameView();
            BuildApp(mainPrefab, namePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = mainPrefab.gameObject;
            Debug.Log("Created editable DOTORI ON prefabs. Builds will not regenerate or overwrite them.");
        }

        private static TeamMemberCardView BuildCard()
        {
            var rootImage = UiFactory.CreateImage("TeamMemberCard", null, DOTORIONPalette.CardOffline);
            var root = rootImage.gameObject;
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(CardWidth, CardHeight);

            try
            {
                var layout = root.AddComponent<LayoutElement>();
                layout.flexibleWidth = 1f;
                layout.minWidth = 96f;
                var view = root.AddComponent<TeamMemberCardView>();
                var font = PreviewFont();

                var timer = UiFactory.CreateText("ElapsedTimer", root.transform, font, 12,
                    TextAnchor.MiddleCenter, DOTORIONPalette.TextSecondary, FontStyle.Bold);
                UiFactory.AnchorTop(timer.rectTransform, 4f, 5f, 100f, 18f);
                timer.rectTransform.anchorMax = new Vector2(1f, 1f);
                timer.rectTransform.sizeDelta = new Vector2(-8f, 18f);
                timer.text = "00:42:18";

                var avatar = UiFactory.CreateImage("Avatar", root.transform, DOTORIONPalette.Working);
                var avatarRect = avatar.rectTransform;
                avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0.5f, 1f);
                avatarRect.pivot = new Vector2(0.5f, 1f);
                avatarRect.anchoredPosition = new Vector2(0f, -CardAvatarTop);
                avatarRect.sizeDelta = new Vector2(AvatarIconSize, AvatarIconSize);
                AttachAvatarPicking(avatar, out var avatarButton, out var avatarIcon);
                var initial = UiFactory.CreateText("Initial", avatar.transform, font, 17,
                    TextAnchor.MiddleCenter, DOTORIONPalette.TextPrimary, FontStyle.Bold);
                initial.text = "김";
                UiFactory.Stretch(initial.rectTransform);

                var name = UiFactory.CreateText("Name", root.transform, font, 13,
                    TextAnchor.MiddleCenter, DOTORIONPalette.TextPrimary, FontStyle.Bold);
                SetCardLine(name, CardNameTop, 21f);
                name.text = "김햄초";
                // Alone among the labels the name is clicked, so it is the only
                // one that has to be a raycast target. The handle ships disabled:
                // Bind turns it on for the local member's own card, once they
                // have clocked out.
                name.raycastTarget = true;
                var nameDoubleClick = name.gameObject.AddComponent<DoubleClickHandle>();
                nameDoubleClick.enabled = false;
                var status = UiFactory.CreateText("Status", root.transform, font, 11,
                    TextAnchor.MiddleCenter, DOTORIONPalette.Working, FontStyle.Bold);
                SetCardLine(status, CardStatusTop, 16f);
                status.text = "작업중";
                var nudge = UiFactory.CreateButton("Nudge", root.transform, font, "\uCF55");
                nudge.GetComponentInChildren<Text>().fontSize = 9;
                var nudgeRect = nudge.GetComponent<RectTransform>();
                nudgeRect.anchorMin = nudgeRect.anchorMax = new Vector2(1f, 1f);
                nudgeRect.pivot = new Vector2(1f, 1f);
                nudgeRect.anchoredPosition = new Vector2(-3f, -3f);
                nudgeRect.sizeDelta = new Vector2(22f, 18f);

                var detail = UiFactory.CreateText("Detail", root.transform, font, 9,
                    TextAnchor.MiddleCenter, DOTORIONPalette.TextSecondary);
                SetCardLine(detail, CardDetailTop, 26f);
                detail.horizontalOverflow = HorizontalWrapMode.Wrap;
                detail.text = "출근 09:00";

                Assign(view,
                    ("_background", rootImage), ("_avatarBackground", avatar),
                    ("_avatarIcon", avatarIcon), ("_avatarButton", avatarButton),
                    ("_avatarText", initial), ("_timerText", timer), ("_nameText", name),
                    ("_statusText", status), ("_detailText", detail), ("_nudgeButton", nudge),
                    ("_nudgeRoot", nudge.gameObject),
                    ("_nameDoubleClick", nameDoubleClick));
                return PrefabUtility.SaveAsPrefabAsset(root, CardPath).GetComponent<TeamMemberCardView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static DOTORIONView BuildMainView(TeamMemberCardView cardPrefab)
        {
            var root = new GameObject("DOTORIONCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DOTORIONView));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                ConfigureScaler(root.GetComponent<CanvasScaler>());
                var font = PreviewFont();
                var background = UiFactory.CreateImage("WindowBackground", root.transform, DOTORIONPalette.Window);
                UiFactory.Stretch(background.rectTransform);
                var topBar = UiFactory.CreateImage("TopBar", background.transform, DOTORIONPalette.TopBar);
                topBar.rectTransform.anchorMin = new Vector2(0f, 1f);
                topBar.rectTransform.anchorMax = new Vector2(1f, 1f);
                topBar.rectTransform.pivot = new Vector2(0.5f, 1f);
                topBar.rectTransform.sizeDelta = new Vector2(0f, 32f);

                var dragArea = UiFactory.CreateImage("WindowDragArea", topBar.transform, new Color(1f, 1f, 1f, 0.001f));
                UiFactory.Stretch(dragArea.rectTransform, 0f, 0f, 271f, 0f);
                var dragHandle = dragArea.gameObject.AddComponent<WindowDragHandle>();
                var title = UiFactory.CreateText("Title", dragArea.transform, font, 12,
                    TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary, FontStyle.Bold);
                title.text = "DOTORI ON";
                UiFactory.Stretch(title.rectTransform, 10f, 0f, 0f, 0f);
                var version = UiFactory.CreateText("Version", dragArea.transform, font, 9,
                    TextAnchor.MiddleLeft, DOTORIONPalette.TextSecondary);
                version.text = "v0.0";
                UiFactory.AnchorTop(version.rectTransform, 82f, 0f, 40f, 32f);

                Button fake = null;
                var teamNudge = TopButton(topBar.transform, font, "TeamNudge", "전체호출", 105f, 52f);
                teamNudge.GetComponentInChildren<Text>().fontSize = 9;
                // Inside the drag area, which is the only part of the bar with
                // room left: everything to the right of it is buttons, edge to
                // edge, and the first attempt at this sat underneath 소형. A
                // Button consumes its own pointer-down, so the window still drags
                // from everywhere around it.
                var dailyCheckIn = TopButtonAt(dragArea.transform, font, "DailyCheckIn", "출석",
                    126f, 36f, DOTORIONPalette.Accent);
                dailyCheckIn.GetComponentInChildren<Text>().fontSize = 9;
                var checkInPoints = UiFactory.CreateText("DailyCheckInPoints", dragArea.transform, font, 9,
                    TextAnchor.MiddleLeft, DOTORIONPalette.Accent, FontStyle.Bold);
                checkInPoints.text = "0P";
                UiFactory.AnchorTop(checkInPoints.rectTransform, 166f, 5f, 40f, 22f);
                // Takes over the slot and the width the rename button had, so the
                // rest of the bar keeps the offsets it was tuned with. Renaming
                // moved onto the name it changes, where a double click does it.
                var miniMode = TopButton(topBar.transform, font, "MiniMode", "소형", 161f, 54f);
                var stats = TopButton(topBar.transform, font, "Statistics", "\uD1B5\uACC4", 219f, 48f);
                var settings = TopButton(topBar.transform, font, "Settings", "설정", 63f, 38f);
                settings.GetComponentInChildren<Text>().fontSize = 11;
                var minimize = TopButton(topBar.transform, font, "Minimize", "—", 32f, 28f);
                var exit = TopButton(topBar.transform, font, "Exit", "×", 3f, 27f, DOTORIONPalette.Danger);

                var cardsRoot = UiFactory.CreateRect("MemberCards", background.transform);
                var cardsRect = cardsRoot.GetComponent<RectTransform>();
                cardsRect.anchorMin = new Vector2(0f, 1f);
                cardsRect.anchorMax = new Vector2(1f, 1f);
                cardsRect.pivot = new Vector2(0.5f, 1f);
                // 6px in on the left, 7px on the right: the odd 13 is what makes
                // each card exactly CardWidth instead of 113.25, and the half
                // pixel is spent on the margin where nothing is drawn.
                cardsRect.anchoredPosition = new Vector2(-0.5f, -36f);
                cardsRect.sizeDelta = new Vector2(
                    -(480f - ((CardWidth * 4f) + (CardRowSpacing * 3f))),
                    CardHeight);
                var horizontal = cardsRoot.AddComponent<HorizontalLayoutGroup>();
                horizontal.spacing = CardRowSpacing;
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

                var controls = UiFactory.CreateImage("LocalControls", background.transform, DOTORIONPalette.ControlBar);
                controls.rectTransform.anchorMin = new Vector2(0f, 1f);
                controls.rectTransform.anchorMax = new Vector2(1f, 1f);
                controls.rectTransform.pivot = new Vector2(0.5f, 1f);
                controls.rectTransform.anchoredPosition = new Vector2(0f, -177f);
                controls.rectTransform.sizeDelta = new Vector2(0f, 43f);
                var checkIn = ControlButton(controls.transform, font, "CheckIn", "출근", -54f, 108f);
                var checkOut = ControlButton(controls.transform, font, "CheckOut", "퇴근", -153f, 66f, DOTORIONPalette.Danger);
                var working = ControlButton(controls.transform, font, "Working", "작업중", -81f, 70f);
                var rest = ControlButton(controls.transform, font, "Break", "쉬는중", -5f, 70f);
                var meal = ControlButton(controls.transform, font, "Meal", "식사중", 71f, 70f);
                var noteBackground = UiFactory.CreateImage("StatusNoteInput", controls.transform, DOTORIONPalette.Window);
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
                noteInput.caretColor = DOTORIONPalette.TextPrimary;
                var noteText = UiFactory.CreateText("Text", noteBackground.transform, font, 10,
                    TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary);
                UiFactory.Stretch(noteText.rectTransform, 8f, 2f, 8f, 2f);
                var notePlaceholder = UiFactory.CreateText("Placeholder", noteBackground.transform, font, 10,
                    TextAnchor.MiddleLeft, new Color(DOTORIONPalette.TextSecondary.r,
                        DOTORIONPalette.TextSecondary.g, DOTORIONPalette.TextSecondary.b, 0.68f));
                notePlaceholder.text = "메모";
                notePlaceholder.fontStyle = FontStyle.Italic;
                UiFactory.Stretch(notePlaceholder.rectTransform, 8f, 2f, 8f, 2f);
                noteInput.textComponent = noteText;
                noteInput.placeholder = notePlaceholder;

                var feedback = UiFactory.CreateText("Feedback", controls.transform, font, 8,
                    TextAnchor.LowerCenter, DOTORIONPalette.TextSecondary);
                feedback.text = "Supabase Auth 연결 · 팀 상태 Mock";
                UiFactory.Stretch(feedback.rectTransform, 4f, 0f, 4f, 31f);

                var statisticsPanel = BuildStatisticsPanel(background.transform, font);
                // A child of the window background like the statistics panel, and
                // it unfolds downwards the same way.
                var settingsPanel = BuildSettingsPanel(background.transform, font);
                // A sibling of the window background rather than a child of it:
                // the picker owns the top strip of the canvas and the background
                // is pushed down under it, which is what lets the window grow
                // upwards without moving anything inside the compact layout.
                var avatarPicker = BuildAvatarPickerPanel(root.transform, font);
                // Also a sibling of the window background rather than a child of
                // it, for a different reason: the mini overlay replaces the whole
                // overlay instead of folding out of it.
                var miniPanel = BuildMiniPanel(root.transform, font);
                // A child of the window background, like the statistics panel: it
                // unfolds downwards under the overlay rather than replacing it.
                var dashboard = BuildDashboardPanel(background.transform, font);
                var view = root.GetComponent<DOTORIONView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_cards").arraySize = cards.Length;
                for (var i = 0; i < cards.Length; i++) serialized.FindProperty("_cards").GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
                Set(serialized, "_checkInButton", checkIn);
                Set(serialized, "_checkOutButton", checkOut);
                Set(serialized, "_workingButton", working);
                Set(serialized, "_breakButton", rest);
                Set(serialized, "_mealButton", meal);
                Set(serialized, "_fakeEventButton", fake);
                Set(serialized, "_settingsButton", settings);
                Set(serialized, "_minimizeButton", minimize);
                Set(serialized, "_exitButton", exit);
                Set(serialized, "_miniModeButton", miniMode);
                Set(serialized, "_statusNoteInput", noteInput);
                Set(serialized, "_statsButton", stats);
                Set(serialized, "_feedbackText", feedback);
                Set(serialized, "_versionLabel", version);
                Set(serialized, "_teamNudgeButton", teamNudge);
                Set(serialized, "_dailyCheckInButton", dailyCheckIn);
                Set(serialized, "_dailyCheckInPointsLabel", checkInPoints);
                Set(serialized, "_windowDragHandle", dragHandle);
                Set(serialized, "_statisticsPanel", statisticsPanel);
                Set(serialized, "_settingsPanel", settingsPanel);
                Set(serialized, "_windowBackground", background.rectTransform);
                Set(serialized, "_avatarPickerPanel", avatarPicker);
                Set(serialized, "_miniPanel", miniPanel);
                Set(serialized, "_dashboardPanel", dashboard);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, MainViewPath).GetComponent<DOTORIONView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// Makes a card's avatar tile clickable and gives it somewhere to draw a
        /// picked icon. Shared with the migration command so a prefab that was
        /// hand-tweaked can gain the feature without being regenerated.
        /// </summary>
        internal static void AttachAvatarPicking(Image avatar, out Button button, out Image icon)
        {
            // The tile keeps its status colour, so the button must not tint it: a
            // hover that repainted the tile would read as a status change. The
            // picked icon is inset instead of filling the tile, which leaves the
            // status colour as a frame around it.
            button = avatar.GetComponent<Button>();
            if (button == null)
            {
                button = avatar.gameObject.AddComponent<Button>();
            }

            button.transition = Selectable.Transition.None;
            button.targetGraphic = avatar;

            var existing = avatar.transform.Find("Icon");
            icon = existing != null
                ? existing.GetComponent<Image>()
                : UiFactory.CreateImage("Icon", avatar.transform, Color.white);
            // Edge to edge: a margin inside the tile reads as the picture being
            // smaller than the space made for it.
            UiFactory.Stretch(icon.rectTransform);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;
            // Above the tile but under the initial, which is what shows when
            // nobody has picked an icon yet.
            icon.transform.SetSiblingIndex(0);
        }

        /// <summary>
        /// The icon grid. The cells are not built here: the catalog is an asset
        /// the team keeps adding to, so the panel clones one template at runtime
        /// as many times as the catalog is long.
        /// </summary>
        internal static AvatarPickerPanelView BuildAvatarPickerPanel(Transform parent, Font font)
        {
            var panel = UiFactory.CreateImage("AvatarPickerPanel", parent, DOTORIONPalette.TopBar);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(0f, AvatarPickerPanelHeight);
            var panelView = panel.gameObject.AddComponent<AvatarPickerPanelView>();

            var heading = UiFactory.CreateText("Heading", panel.transform, font, 12,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary, FontStyle.Bold);
            heading.text = "\uD504\uB85C\uD544 \uC544\uC774\uCF58";
            UiFactory.AnchorTop(heading.rectTransform, 12f, 8f, 200f, 20f);

            var confirm = UiFactory.CreateButton("Confirm", panel.transform, font, "\uD655\uC778",
                null, DOTORIONPalette.Accent);
            UiFactory.AnchorRight(confirm.GetComponent<RectTransform>(), 10f, 6f, 56f, 24f);

            // RectMask2D clips without needing a mask sprite, and the scroll rect
            // uses the viewport as its own so there is one fewer object to keep
            // in sync with the panel height.
            var viewport = UiFactory.CreateImage("Viewport", panel.transform, new Color(0f, 0f, 0f, 0.001f));
            UiFactory.Stretch(viewport.rectTransform, 10f, 8f, 10f, 34f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UiFactory.CreateRect("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(AvatarCellSize, AvatarCellSize);
            grid.spacing = new Vector2(AvatarCellSpacing, AvatarCellSpacing);
            grid.padding = new RectOffset(2, 2, 2, 2);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = AvatarGridColumns;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            var template = UiFactory.CreateButton("OptionTemplate", content.transform, font, string.Empty);
            UnityEngine.Object.DestroyImmediate(template.transform.Find("Label").gameObject);
            var templateIcon = UiFactory.CreateImage("Icon", template.transform, Color.white);
            // Same drawn size as the card tile, so pixel art lands on whole
            // pixels in both places instead of being resampled in one of them.
            UiFactory.Stretch(
                templateIcon.rectTransform,
                AvatarCellPadding, AvatarCellPadding, AvatarCellPadding, AvatarCellPadding);
            templateIcon.raycastTarget = false;
            templateIcon.preserveAspect = true;
            template.gameObject.SetActive(false);

            var feedback = UiFactory.CreateText("Feedback", panel.transform, font, 10,
                TextAnchor.MiddleCenter, DOTORIONPalette.TextSecondary);
            feedback.horizontalOverflow = HorizontalWrapMode.Wrap;
            feedback.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Stretch(feedback.rectTransform, 16f, 8f, 16f, 34f);
            feedback.gameObject.SetActive(false);

            Assign(panelView,
                ("_grid", contentRect), ("_optionTemplate", template),
                ("_confirmButton", confirm), ("_feedbackText", feedback));
            return panelView;
        }

        /// <summary>
        /// The mini overlay: a drag strip and four name-and-status lines, sized
        /// to about one member card. It ships switched off, and the app swaps it
        /// for the window background rather than showing both.
        /// </summary>
        private static MiniOverlayPanelView BuildMiniPanel(Transform parent, Font font)
        {
            var panel = UiFactory.CreateImage("MiniOverlayPanel", parent, DOTORIONPalette.Window);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(MiniPanelWidth, MiniPanelHeight);
            var panelView = panel.gameObject.AddComponent<MiniOverlayPanelView>();

            // The strip is the only part that drags, because a body that started
            // a native window drag on pointer down would swallow the first half
            // of the double click that brings the full overlay back.
            var strip = UiFactory.CreateImage("MiniDragStrip", panel.transform, DOTORIONPalette.TopBar);
            strip.rectTransform.anchorMin = new Vector2(0f, 1f);
            strip.rectTransform.anchorMax = new Vector2(1f, 1f);
            strip.rectTransform.pivot = new Vector2(0.5f, 1f);
            strip.rectTransform.anchoredPosition = Vector2.zero;
            strip.rectTransform.sizeDelta = new Vector2(0f, MiniDragStripHeight);
            var dragHandle = strip.gameObject.AddComponent<WindowDragHandle>();
            var title = UiFactory.CreateText("Title", strip.transform, font, 9,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextSecondary, FontStyle.Bold);
            title.text = "DOTORI ON";
            UiFactory.Stretch(title.rectTransform, 6f, 0f, 6f, 0f);

            var rows = new MiniMemberRowView[MiniRowCount];
            for (var index = 0; index < rows.Length; index++)
            {
                rows[index] = BuildMiniRow(panel.transform, font, index);
            }

            var panelData = new SerializedObject(panelView);
            var rowsProperty = panelData.FindProperty("_rows");
            rowsProperty.arraySize = rows.Length;
            for (var index = 0; index < rows.Length; index++)
                rowsProperty.GetArrayElementAtIndex(index).objectReferenceValue = rows[index];
            Set(panelData, "_dragHandle", dragHandle);
            panelData.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
            return panelView;
        }

        /// <summary>
        /// One mini line. Nothing in it is a raycast target: the whole body has
        /// to reach the panel's double click handler, and a pill that ate the
        /// click would leave dead spots you cannot restore the overlay from.
        /// </summary>
        private static MiniMemberRowView BuildMiniRow(Transform parent, Font font, int index)
        {
            var row = UiFactory.CreateRect("MiniRow_" + (index + 1), parent);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition =
                new Vector2(0f, -(MiniRowTop + (index * (MiniRowHeight + MiniRowSpacing))));
            rowRect.sizeDelta = new Vector2(0f, MiniRowHeight);
            var rowView = row.AddComponent<MiniMemberRowView>();

            // The pill is the row. At 75px wide there is nothing to put beside
            // it, so the name goes on top of the status colour instead of
            // competing with it for the width.
            // Anchored from the top with whole-pixel offsets rather than
            // centred: 18 inside 25, and 7 inside 18, both land on a half pixel
            // if you centre them, and a half pixel is where a pixel font blurs.
            var pill = UiFactory.CreateImage("Pill", row.transform, DOTORIONPalette.Working);
            var pillRect = pill.rectTransform;
            pillRect.anchorMin = pillRect.anchorMax = new Vector2(0f, 1f);
            pillRect.pivot = new Vector2(0f, 1f);
            pillRect.anchoredPosition = new Vector2(6f, -3f);
            pillRect.sizeDelta = new Vector2(62f, 18f);
            pill.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
            pill.type = Image.Type.Sliced;
            pill.raycastTarget = false;

            var dot = UiFactory.CreateImage("Dot", pill.transform, DOTORIONPalette.TextPrimary);
            var dotRect = dot.rectTransform;
            dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, 1f);
            dotRect.pivot = new Vector2(0f, 1f);
            dotRect.anchoredPosition = new Vector2(4f, -5f);
            dotRect.sizeDelta = new Vector2(7f, 7f);
            dot.sprite = BuiltinSprite("UI/Skin/Knob.psd");
            dot.raycastTarget = false;

            var name = UiFactory.CreateText("Name", pill.transform, font, 11,
                TextAnchor.MiddleLeft, DOTORIONPalette.Window, FontStyle.Bold);
            UiFactory.Stretch(name.rectTransform, 14f, 0f, 4f, 0f);
            name.text = "김햄초";
            name.raycastTarget = false;

            Assign(rowView, ("_nameText", name), ("_pill", pill), ("_dot", dot));
            return rowView;
        }

        /// <summary>
        /// The rounded pill and its dot are the only shaped graphics in the UI,
        /// which is otherwise flat rectangles. Unity ships both, so neither costs
        /// the project an imported sprite.
        /// </summary>
        private static Sprite BuiltinSprite(string path)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
        }

        private static TeamStatisticsPanelView BuildStatisticsPanel(Transform parent, Font font)
        {
            var panel = UiFactory.CreateImage("StatisticsPanel", parent, DOTORIONPalette.Window);
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
                TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary, FontStyle.Bold);
            heading.text = "\uD300 \uD1B5\uACC4";
            UiFactory.AnchorTop(heading.rectTransform, 14f, 8f, 150f, 24f);
            var period = UiFactory.CreateText("Period", panel.transform, font, 10,
                TextAnchor.MiddleRight, DOTORIONPalette.TextSecondary);
            period.text = "2026.08.21 - 2026.08.27";
            UiFactory.AnchorTop(period.rectTransform, 236f, 8f, 230f, 24f);

            var dailyTab = UiFactory.CreateButton("DailyTab", panel.transform, font, "\uB0B4 \uD1B5\uACC4", null,
                DOTORIONPalette.Accent);
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
                    index == 0 ? DOTORIONPalette.Accent : DOTORIONPalette.Button);
                periodButtons[index].GetComponentInChildren<Text>().fontSize = 11;
                UiFactory.AnchorTop(
                    periodButtons[index].GetComponent<RectTransform>(), 206f + index * 88f, 39f, 84f, 28f);
            }

            var dailyContent = CreateStatisticsContent("DailyContent", panel.transform);
            var summary = UiFactory.CreateText("Summary", dailyContent.transform, font, 10,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextSecondary);
            summary.text = "\uD569\uACC4 \uC791\uC5C5 00:00";
            UiFactory.AnchorTop(summary.rectTransform, 10f, 0f, 460f, 20f);
            var statRows = new TeamPeriodStatRowView[7];
            for (var index = 0; index < statRows.Length; index++)
            {
                statRows[index] = BuildPeriodStatRow(dailyContent.transform, font, 24f + index * 42f);
            }

            // Shares the space the rows use, because the month and the other
            // periods are two readings of the same daily buckets and only one of
            // them is ever on screen.
            var calendar = BuildCalendar(dailyContent.transform, font);

            var rankingContent = CreateStatisticsContent("RankingContent", panel.transform);
            var metricButtons = new Button[4];
            var metricLabels = new[] { "\uC791\uC5C5", "\uCD1D\uC2DC\uAC04", "\uD734\uC2DD", "\uC2DD\uC0AC" };
            var metricNames = new[] { "MetricWork", "MetricAttendance", "MetricBreak", "MetricMeal" };
            for (var index = 0; index < metricButtons.Length; index++)
            {
                metricButtons[index] = UiFactory.CreateButton(
                    metricNames[index], rankingContent.transform, font, metricLabels[index], null,
                    index == 0 ? DOTORIONPalette.Working : DOTORIONPalette.Button);
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
                TextAnchor.MiddleCenter, DOTORIONPalette.TextSecondary);
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
            Set(serialized, "_calendar", calendar);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return panelView;
        }

        /// <summary>
        /// The settings panel. The switches that used to cost a slot each on the
        /// top bar live here as labelled rows, which is also the only place with
        /// room to say what each one does.
        /// </summary>
        private static SettingsPanelView BuildSettingsPanel(Transform parent, Font font)
        {
            var panel = UiFactory.CreateImage("SettingsPanel", parent, DOTORIONPalette.Window);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -220f);
            panelRect.sizeDelta = new Vector2(0f, SettingsPanelHeight);
            var panelView = panel.gameObject.AddComponent<SettingsPanelView>();

            var heading = UiFactory.CreateText("Heading", panel.transform, font, 14,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary, FontStyle.Bold);
            heading.text = "설정";
            UiFactory.AnchorTop(heading.rectTransform, 14f, 8f, 200f, 24f);

            var alwaysOnTop = SettingsSwitchRow(panel.transform, font, "AlwaysOnTop",
                "항상 위", "다른 창 위에 계속 띄워 둡니다.", SettingsRowTop);
            var mute = SettingsSwitchRow(panel.transform, font, "Mute",
                "알림음", "출근과 호출을 소리로 알립니다.",
                SettingsRowTop + SettingsRowStep);
            var autoStart = SettingsSwitchRow(panel.transform, font, "AutoStart",
                AutoStartRowLabel, AutoStartRowHint,
                SettingsRowTop + (SettingsRowStep * 2f));

            var versionTop = SettingsRowTop + (SettingsRowStep * 3f);
            SettingsRowLabel(panel.transform, font, "VersionLabel", "버전", versionTop);
            var version = UiFactory.CreateText("VersionValue", panel.transform, font, 11,
                TextAnchor.MiddleRight, DOTORIONPalette.TextSecondary);
            version.text = "DOTORI ON v0.0";
            UiFactory.AnchorRight(version.rectTransform, 14f, versionTop, 300f, SettingsRowHeight);

            var serialized = new SerializedObject(panelView);
            Set(serialized, "_alwaysOnTopButton", alwaysOnTop);
            Set(serialized, "_alwaysOnTopValue", alwaysOnTop.GetComponentInChildren<Text>());
            Set(serialized, "_muteButton", mute);
            Set(serialized, "_muteValue", mute.GetComponentInChildren<Text>());
            Set(serialized, "_autoStartButton", autoStart);
            Set(serialized, "_autoStartValue", autoStart.GetComponentInChildren<Text>());
            Set(serialized, "_versionText", version);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return panelView;
        }

        /// <summary>
        /// A name, a line saying what it does, and the switch itself on the right.
        /// The switch label is the value, so the panel writes 켜짐 and 꺼짐 into
        /// the button's own text rather than keeping a second label beside it.
        /// </summary>
        private static Button SettingsSwitchRow(
            Transform parent,
            Font font,
            string name,
            string label,
            string hint,
            float top)
        {
            SettingsRowLabel(parent, font, name + "Label", label, top);
            var hintText = UiFactory.CreateText(name + "Hint", parent, font, 10,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextSecondary);
            hintText.text = hint;
            UiFactory.AnchorTop(hintText.rectTransform, 96f, top, 280f, SettingsRowHeight);

            var toggle = UiFactory.CreateButton(name + "Toggle", parent, font, "켜짐");
            toggle.GetComponentInChildren<Text>().fontSize = 11;
            UiFactory.AnchorRight(toggle.GetComponent<RectTransform>(), 14f, top, 76f, SettingsRowHeight);
            return toggle;
        }

        private static Text SettingsRowLabel(
            Transform parent,
            Font font,
            string name,
            string label,
            float top)
        {
            var text = UiFactory.CreateText(name, parent, font, 12,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary, FontStyle.Bold);
            text.text = label;
            UiFactory.AnchorTop(text.rectTransform, 14f, top, 80f, SettingsRowHeight);
            return text;
        }

        /// <summary>
        /// Six rows of seven squares under a weekday header, Monday first. The
        /// sixth row is there for the months that need it - a 31 day month
        /// starting on a Saturday - and stays switched off the rest of the time.
        /// </summary>
        private static TeamCalendarView BuildCalendar(Transform parent, Font font)
        {
            var calendar = UiFactory.CreateRect("Calendar", parent);
            var calendarRect = calendar.GetComponent<RectTransform>();
            calendarRect.anchorMin = new Vector2(0f, 1f);
            calendarRect.anchorMax = new Vector2(1f, 1f);
            calendarRect.pivot = new Vector2(0.5f, 1f);
            calendarRect.anchoredPosition = new Vector2(0f, -CalendarTop);
            calendarRect.sizeDelta = new Vector2(0f, CalendarHeight);
            var calendarView = calendar.AddComponent<TeamCalendarView>();

            var weekdays = new[] { "월", "화", "수", "목", "금", "토", "일" };
            for (var column = 0; column < weekdays.Length; column++)
            {
                var label = UiFactory.CreateText("Weekday_" + weekdays[column], calendar.transform, font, 9,
                    TextAnchor.MiddleCenter, DOTORIONPalette.TextSecondary, FontStyle.Bold);
                label.text = weekdays[column];
                UiFactory.AnchorTop(
                    label.rectTransform,
                    CalendarLeft + (column * (CalendarCellWidth + CalendarCellGap)),
                    0f,
                    CalendarCellWidth,
                    CalendarHeaderHeight);
            }

            var cells = new TeamCalendarDayView[TeamCalendarView.CellCount];
            for (var index = 0; index < cells.Length; index++)
            {
                var column = index % TeamCalendarView.DaysPerWeek;
                var row = index / TeamCalendarView.DaysPerWeek;
                cells[index] = BuildCalendarDay(
                    calendar.transform,
                    font,
                    index,
                    CalendarLeft + (column * (CalendarCellWidth + CalendarCellGap)),
                    CalendarHeaderHeight + 2f + (row * (CalendarCellHeight + CalendarCellGap)));
            }

            var serialized = new SerializedObject(calendarView);
            SetArray(serialized, "_cells", cells);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            calendar.SetActive(false);
            return calendarView;
        }

        private static TeamCalendarDayView BuildCalendarDay(
            Transform parent,
            Font font,
            int index,
            float left,
            float top)
        {
            var background = UiFactory.CreateImage(
                "CalendarDay_" + (index + 1), parent, DOTORIONPalette.Card);
            UiFactory.AnchorTop(
                background.rectTransform, left, top, CalendarCellWidth, CalendarCellHeight);
            // The squares are the grid's only control: clicking any of them swaps
            // what every square shows, so they have to take the raycast.
            background.raycastTarget = true;
            var cell = background.gameObject.AddComponent<TeamCalendarDayView>();

            var day = UiFactory.CreateText("Day", background.transform, font, 9,
                TextAnchor.UpperLeft, DOTORIONPalette.TextSecondary);
            day.text = "1";
            UiFactory.AnchorTop(day.rectTransform, 4f, 3f, 24f, 13f);

            var duration = UiFactory.CreateText("Duration", background.transform, font, 11,
                TextAnchor.MiddleCenter, DOTORIONPalette.TextPrimary, FontStyle.Bold);
            duration.text = "00:00";
            // Tall enough for the three stacked lines of the breakdown, and
            // allowed to overflow so a tight fit clips nothing.
            UiFactory.AnchorTop(duration.rectTransform, 0f, 13f, CalendarCellWidth, 30f);
            duration.lineSpacing = 0.85f;
            duration.verticalOverflow = VerticalWrapMode.Overflow;
            duration.raycastTarget = false;

            Assign(cell,
                ("_background", background), ("_dayLabel", day), ("_durationLabel", duration));
            return cell;
        }

        /// <summary>
        /// The developer dashboard: the roster as rows of numbers. Plain on
        /// purpose - it is read far more often than it is acted on, and the one
        /// thing it can destroy is behind a confirmation that names its target.
        /// </summary>
        private static DeveloperDashboardView BuildDashboardPanel(Transform parent, Font font)
        {
            var panel = UiFactory.CreateImage("DashboardPanel", parent, DOTORIONPalette.Window);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -220f);
            panelRect.sizeDelta = new Vector2(0f, DashboardPanelHeight);
            var panelView = panel.gameObject.AddComponent<DeveloperDashboardView>();

            var heading = UiFactory.CreateText("Heading", panel.transform, font, 14,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary, FontStyle.Bold);
            heading.text = "\uAC1C\uBC1C\uC790 \uB300\uC2DC\uBCF4\uB4DC";
            UiFactory.AnchorTop(heading.rectTransform, 14f, 8f, 200f, 24f);

            var refresh = UiFactory.CreateButton("Refresh", panel.transform, font, "\uC0C8\uB85C\uACE0\uCE68");
            refresh.GetComponentInChildren<Text>().fontSize = 10;
            UiFactory.AnchorRight(refresh.GetComponent<RectTransform>(), 156f, 8f, 72f, 24f);
            var signOut = UiFactory.CreateButton(
                "SignOut", panel.transform, font, "\uB2E4\uB978 \uC774\uB984\uC73C\uB85C \uB85C\uADF8\uC778");
            signOut.GetComponentInChildren<Text>().fontSize = 9;
            UiFactory.AnchorRight(signOut.GetComponent<RectTransform>(), 42f, 8f, 110f, 24f);
            var close = UiFactory.CreateButton("Close", panel.transform, font, "\u00D7");
            UiFactory.AnchorRight(close.GetComponent<RectTransform>(), 12f, 8f, 26f, 24f);

            var rows = new DeveloperDashboardRowView[DeveloperDashboardView.RowCount];
            for (var index = 0; index < rows.Length; index++)
            {
                rows[index] = BuildDashboardRow(
                    panel.transform,
                    font,
                    index,
                    DashboardRowTop + (index * (DashboardRowHeight + DashboardRowSpacing)));
            }

            var feedback = UiFactory.CreateText("DashboardFeedback", panel.transform, font, 10,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextSecondary);
            feedback.text = "\uBD88\uB7EC\uC624\uB294 \uC911\u2026";
            UiFactory.AnchorTop(feedback.rectTransform, 14f, 268f, 452f, 20f);

            var confirm = BuildDashboardConfirm(panel.transform, font,
                out var confirmText, out var confirmDelete, out var cancelDelete);

            var serialized = new SerializedObject(panelView);
            SetArray(serialized, "_rows", rows);
            Set(serialized, "_feedbackText", feedback);
            Set(serialized, "_refreshButton", refresh);
            Set(serialized, "_signOutButton", signOut);
            Set(serialized, "_closeButton", close);
            Set(serialized, "_confirmPanel", confirm);
            Set(serialized, "_confirmText", confirmText);
            Set(serialized, "_confirmDeleteButton", confirmDelete);
            Set(serialized, "_cancelDeleteButton", cancelDelete);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return panelView;
        }

        private static DeveloperDashboardRowView BuildDashboardRow(
            Transform parent,
            Font font,
            int index,
            float top)
        {
            var background = UiFactory.CreateImage(
                "DashboardRow_" + (index + 1), parent, DOTORIONPalette.CardOffline);
            UiFactory.AnchorTop(background.rectTransform, 14f, top, 452f, DashboardRowHeight);
            var row = background.gameObject.AddComponent<DeveloperDashboardRowView>();

            var name = UiFactory.CreateText("Name", background.transform, font, 11,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary, FontStyle.Bold);
            UiFactory.AnchorTop(name.rectTransform, 10f, 0f, 120f, DashboardRowHeight);
            name.text = "김햄초";
            var sessions = DashboardCell(background.transform, font, "Sessions", 134f, 60f, "12");
            var attendance = DashboardCell(background.transform, font, "Attendance", 198f, 80f, "48:20");
            var points = DashboardCell(background.transform, font, "Points", 282f, 60f, "120P");
            var lastSeen = DashboardCell(background.transform, font, "LastSeen", 346f, 90f, "08/27 19:02");

            var delete = UiFactory.CreateButton("Delete", background.transform, font,
                "\uC0AD\uC81C", null, DOTORIONPalette.Danger);
            delete.GetComponentInChildren<Text>().fontSize = 9;
            UiFactory.AnchorRight(delete.GetComponent<RectTransform>(), 6f, 5f, 42f, 22f);

            Assign(row,
                ("_background", background), ("_nameLabel", name), ("_sessionsLabel", sessions),
                ("_attendanceLabel", attendance), ("_pointsLabel", points),
                ("_lastSeenLabel", lastSeen), ("_deleteButton", delete));
            return row;
        }

        private static Text DashboardCell(
            Transform parent,
            Font font,
            string name,
            float left,
            float width,
            string sample)
        {
            var text = UiFactory.CreateText(name, parent, font, 10,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextSecondary);
            UiFactory.AnchorTop(text.rectTransform, left, 0f, width, DashboardRowHeight);
            text.text = sample;
            return text;
        }

        /// <summary>
        /// Covers the whole panel while it is up, so the list underneath cannot be
        /// clicked while a question about one of its rows is unanswered.
        /// </summary>
        private static GameObject BuildDashboardConfirm(
            Transform parent,
            Font font,
            out Text message,
            out Button confirm,
            out Button cancel)
        {
            var backdrop = UiFactory.CreateImage(
                "DeleteConfirm", parent, new Color(0.02f, 0.03f, 0.05f, 0.92f));
            UiFactory.Stretch(backdrop.rectTransform);

            message = UiFactory.CreateText("Message", backdrop.transform, font, 12,
                TextAnchor.MiddleCenter, DOTORIONPalette.TextPrimary, FontStyle.Bold);
            message.text = "\uACC4\uC815\uACFC \uBAA8\uB4E0 \uAE30\uB85D\uC744 \uC9C0\uC6C1\uB2C8\uB2E4.";
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.AnchorTop(message.rectTransform, 40f, 110f, 400f, 44f);

            confirm = UiFactory.CreateButton("ConfirmDelete", backdrop.transform, font,
                "\uC9C0\uC6C1\uB2C8\uB2E4", null, DOTORIONPalette.Danger);
            UiFactory.AnchorTop(confirm.GetComponent<RectTransform>(), 130f, 164f, 100f, 32f);
            cancel = UiFactory.CreateButton("CancelDelete", backdrop.transform, font, "\uCDE8\uC18C");
            UiFactory.AnchorTop(cancel.GetComponent<RectTransform>(), 250f, 164f, 100f, 32f);

            backdrop.gameObject.SetActive(false);
            return backdrop.gameObject;
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
            var background = UiFactory.CreateImage("StatRow", parent, DOTORIONPalette.Card);
            UiFactory.AnchorTop(background.rectTransform, 10f, top, 460f, 38f);
            var view = background.gameObject.AddComponent<TeamPeriodStatRowView>();
            var date = UiFactory.CreateText("Date", background.transform, font, 9,
                TextAnchor.MiddleCenter, DOTORIONPalette.TextPrimary, FontStyle.Bold);
            UiFactory.AnchorTop(date.rectTransform, 6f, 0f, 70f, 38f);
            var work = UiFactory.CreateText("Work", background.transform, font, 9,
                TextAnchor.MiddleLeft, DOTORIONPalette.Working, FontStyle.Bold);
            UiFactory.AnchorTop(work.rectTransform, 80f, 1f, 72f, 17f);
            var attendance = UiFactory.CreateText("Attendance", background.transform, font, 9,
                TextAnchor.MiddleLeft, DOTORIONPalette.Accent, FontStyle.Bold);
            UiFactory.AnchorTop(attendance.rectTransform, 274f, 1f, 91f, 17f);
            var other = UiFactory.CreateText("Other", background.transform, font, 9,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextSecondary);
            UiFactory.AnchorTop(other.rectTransform, 80f, 19f, 248f, 16f);
            var workBar = CreateFilledBar("WorkBar", background.transform, 154f, 7f, 110f,
                DOTORIONPalette.Working);
            var attendanceBar = CreateFilledBar("AttendanceBar", background.transform, 368f, 7f, 82f,
                DOTORIONPalette.Accent);
            Assign(view,
                ("_dateLabel", date), ("_workLabel", work), ("_attendanceLabel", attendance),
                ("_otherLabel", other), ("_workBar", workBar), ("_attendanceBar", attendanceBar));
            return view;
        }

        private static TeamRankingRowView BuildRankingRow(Transform parent, Font font, float top)
        {
            var background = UiFactory.CreateImage("RankingRow", parent, DOTORIONPalette.Card);
            UiFactory.AnchorTop(background.rectTransform, 10f, top, 460f, 50f);
            var view = background.gameObject.AddComponent<TeamRankingRowView>();
            var rank = UiFactory.CreateText("Rank", background.transform, font, 18,
                TextAnchor.MiddleCenter, DOTORIONPalette.Accent, FontStyle.Bold);
            UiFactory.AnchorTop(rank.rectTransform, 8f, 0f, 32f, 50f);
            var name = UiFactory.CreateText("Name", background.transform, font, 12,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary, FontStyle.Bold);
            UiFactory.AnchorTop(name.rectTransform, 46f, 3f, 104f, 22f);
            // Under the name, where a second line about the person fits without
            // crowding the numbers the ranking is actually sorted by.
            var points = UiFactory.CreateText("Points", background.transform, font, 9,
                TextAnchor.UpperLeft, DOTORIONPalette.Accent);
            UiFactory.AnchorTop(points.rectTransform, 46f, 24f, 104f, 18f);
            points.text = "0P";
            var work = UiFactory.CreateText("Work", background.transform, font, 10,
                TextAnchor.MiddleLeft, DOTORIONPalette.Working, FontStyle.Bold);
            UiFactory.AnchorTop(work.rectTransform, 158f, 3f, 96f, 20f);
            var attendance = UiFactory.CreateText("Attendance", background.transform, font, 10,
                TextAnchor.MiddleLeft, DOTORIONPalette.Accent);
            UiFactory.AnchorTop(attendance.rectTransform, 265f, 3f, 110f, 20f);
            var workBar = CreateFilledBar("WorkBar", background.transform, 158f, 31f, 284f,
                DOTORIONPalette.Working);
            Assign(view,
                ("_background", background), ("_rankLabel", rank), ("_nameLabel", name),
                ("_pointsLabel", points), ("_workLabel", work),
                ("_attendanceLabel", attendance), ("_workBar", workBar));
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
            var track = UiFactory.CreateImage(name + "Track", parent, DOTORIONPalette.Button);
            UiFactory.AnchorTop(track.rectTransform, left, top, width, 6f);
            var fill = UiFactory.CreateImage(name, track.transform, color);
            UiFactory.Stretch(fill.rectTransform);
            fill.type = Image.Type.Simple;
            fill.rectTransform.anchorMax = new Vector2(0.65f, 1f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.rectTransform.sizeDelta = Vector2.zero;
            return fill;
        }

        /// <summary>
        /// A top-bar button placed from the left. The usual one measures from the
        /// right, which is what the packed right-hand side of the bar needs.
        /// </summary>
        private static Button TopButtonAt(Transform parent, Font font, string name, string label,
            float left, float width, Color? color = null)
        {
            var button = UiFactory.CreateButton(name, parent, font, label, null, color);
            UiFactory.AnchorTop(button.GetComponent<RectTransform>(), left, 4f, width, 24f);
            return button;
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
                var panel = UiFactory.CreateImage("NamePanel", backdrop.transform, DOTORIONPalette.Card);
                var panelRect = panel.rectTransform;
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(372f, 174f);
                var accent = UiFactory.CreateImage("Accent", panel.transform, DOTORIONPalette.Accent);
                UiFactory.AnchorTop(accent.rectTransform, 0f, 0f, 372f, 3f);
                var title = UiFactory.CreateText("Title", panel.transform, font, 16, TextAnchor.MiddleLeft,
                    DOTORIONPalette.TextPrimary, FontStyle.Bold);
                title.text = "팀에서 사용할 이름을 알려주세요";
                UiFactory.AnchorTop(title.rectTransform, 18f, 13f, 336f, 25f);
                var description = UiFactory.CreateText("Description", panel.transform, font, 10,
                    TextAnchor.UpperLeft, DOTORIONPalette.TextSecondary);
                description.text = "다른 팀원에게 표시되는 이름입니다. 한글 이름도 사용할 수 있어요.";
                description.horizontalOverflow = HorizontalWrapMode.Wrap;
                UiFactory.AnchorTop(description.rectTransform, 18f, 42f, 336f, 28f);

                var inputBackground = UiFactory.CreateImage("NameInput", panel.transform, DOTORIONPalette.ControlBar);
                UiFactory.AnchorTop(inputBackground.rectTransform, 18f, 76f, 248f, 36f);
                var input = inputBackground.gameObject.AddComponent<InputField>();
                input.targetGraphic = inputBackground;
                input.lineType = InputField.LineType.SingleLine;
                input.characterLimit = 32;
                input.caretColor = DOTORIONPalette.TextPrimary;
                var inputText = UiFactory.CreateText("Text", inputBackground.transform, font, 13,
                    TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary);
                UiFactory.Stretch(inputText.rectTransform, 11f, 2f, 9f, 2f);
                var placeholder = UiFactory.CreateText("Placeholder", inputBackground.transform, font, 12,
                    TextAnchor.MiddleLeft, new Color(DOTORIONPalette.TextSecondary.r, DOTORIONPalette.TextSecondary.g, DOTORIONPalette.TextSecondary.b, 0.68f));
                placeholder.text = "예: 김햄초";
                placeholder.fontStyle = FontStyle.Italic;
                UiFactory.Stretch(placeholder.rectTransform, 11f, 2f, 9f, 2f);
                input.textComponent = inputText;
                input.placeholder = placeholder;
                var confirm = UiFactory.CreateButton("Confirm", panel.transform, font, "확인", null, DOTORIONPalette.Accent);
                UiFactory.AnchorTop(confirm.GetComponent<RectTransform>(), 274f, 76f, 80f, 36f);
                // Only a rename shows this. The first run has nothing behind it
                // to go back to, so it stays hidden there.
                var cancel = UiFactory.CreateButton("Cancel", panel.transform, font, "×");
                UiFactory.AnchorRight(cancel.GetComponent<RectTransform>(), 12f, 10f, 26f, 26f);
                cancel.gameObject.SetActive(false);
                // The title is the only line that speaks: it invites, and then it
                // says what was wrong with what was typed. A separate hint under
                // the field would only be a second voice to contradict it.
                Assign(root.GetComponent<FirstRunNameView>(), ("_nameInput", input),
                    ("_confirmButton", confirm), ("_cancelButton", cancel),
                    ("_messageText", title));
                return PrefabUtility.SaveAsPrefabAsset(root, NameViewPath).GetComponent<FirstRunNameView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// The "there is a newer version" modal. Laid out like the first-run name
        /// modal because it interrupts the same way, and sits above it in the
        /// sorting order for the one case where both could want the screen.
        /// </summary>
        public static UpdatePromptView BuildUpdatePrompt()
        {
            var root = new GameObject("UpdatePromptModal", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UpdatePromptView));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 2100;
                ConfigureScaler(root.GetComponent<CanvasScaler>());
                var font = PreviewFont();

                var backdrop = UiFactory.CreateImage(
                    "ModalBackdrop", root.transform, new Color(0.025f, 0.035f, 0.055f, 0.88f));
                UiFactory.Stretch(backdrop.rectTransform);

                var panel = UiFactory.CreateImage("UpdatePanel", backdrop.transform, DOTORIONPalette.Card);
                var panelRect = panel.rectTransform;
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(372f, 164f);

                var accent = UiFactory.CreateImage("Accent", panel.transform, DOTORIONPalette.Accent);
                UiFactory.AnchorTop(accent.rectTransform, 0f, 0f, 372f, 3f);

                var message = UiFactory.CreateText("Message", panel.transform, font, 15,
                    TextAnchor.UpperLeft, DOTORIONPalette.TextPrimary, FontStyle.Bold);
                message.text = "새로운 버전이 나왔습니다.\n업데이트 할까요?";
                message.horizontalOverflow = HorizontalWrapMode.Wrap;
                message.verticalOverflow = VerticalWrapMode.Overflow;
                UiFactory.AnchorTop(message.rectTransform, 18f, 22f, 336f, 48f);

                var status = UiFactory.CreateText("Status", panel.transform, font, 11,
                    TextAnchor.UpperLeft, DOTORIONPalette.TextSecondary);
                status.text = string.Empty;
                status.horizontalOverflow = HorizontalWrapMode.Wrap;
                UiFactory.AnchorTop(status.rectTransform, 18f, 78f, 336f, 24f);

                // 네 carries the accent because it is the answer being offered;
                // 나중에 is the quiet one beside it rather than a second shout.
                var confirm = UiFactory.CreateButton(
                    "Confirm", panel.transform, font, "네", null, DOTORIONPalette.Accent);
                UiFactory.AnchorTop(confirm.GetComponent<RectTransform>(), 274f, 110f, 80f, 36f);

                var later = UiFactory.CreateButton("Later", panel.transform, font, "나중에");
                UiFactory.AnchorTop(later.GetComponent<RectTransform>(), 186f, 110f, 80f, 36f);

                Assign(root.GetComponent<UpdatePromptView>(),
                    ("_messageText", message),
                    ("_statusText", status),
                    ("_confirmButton", confirm),
                    ("_laterButton", later));
                return PrefabUtility.SaveAsPrefabAsset(root, UpdatePromptPath).GetComponent<UpdatePromptView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildApp(DOTORIONView mainPrefab, FirstRunNameView namePrefab)
        {
            var root = new GameObject("DOTORIONApp", typeof(DOTORIONApp));
            try
            {
                Assign(
                    root.GetComponent<DOTORIONApp>(),
                    ("_mainViewPrefab", mainPrefab),
                    ("_firstRunNamePrefab", namePrefab),
                    ("_updatePromptPrefab", BuildUpdatePrompt()),
                    ("_sounds", EnsureSoundsAsset()),
                    ("_avatarCatalog", EnsureAvatarCatalogAsset()));
                PrefabUtility.SaveAsPrefabAsset(root, AppPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// Creates the sound settings asset the first time and never touches it
        /// again: it holds hand-picked clips, so a rebuild must not reset it the
        /// way it resets the generated prefabs.
        /// </summary>
        [MenuItem("DOTORI ON/Create Missing Sound Settings Asset")]
        public static DOTORIONSounds EnsureSoundsAsset()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "DOTORION");
            var existing = AssetDatabase.LoadAssetAtPath<DOTORIONSounds>(SoundsPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                return existing;
            }

            var created = ScriptableObject.CreateInstance<DOTORIONSounds>();
            AssetDatabase.CreateAsset(created, SoundsPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = created;
            Debug.Log("Created " + SoundsPath + ". Drop the team's audio clips into it.");
            return created;
        }

        /// <summary>
        /// Creates the icon catalog the first time and never touches it again, for
        /// the same reason as the sound asset: it holds hand-picked artwork, so a
        /// prefab rebuild must not empty it.
        /// </summary>
        [MenuItem("DOTORI ON/Create Missing Avatar Catalog Asset")]
        public static TeamAvatarCatalog EnsureAvatarCatalogAsset()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "DOTORION");
            var existing = AssetDatabase.LoadAssetAtPath<TeamAvatarCatalog>(AvatarCatalogPath);
            if (existing != null)
            {
                return existing;
            }

            var created = ScriptableObject.CreateInstance<TeamAvatarCatalog>();
            AssetDatabase.CreateAsset(created, AvatarCatalogPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created " + AvatarCatalogPath + ". Drop the team's profile icons into it.");
            return created;
        }

        /// <summary>
        /// Fills the catalog with every sprite in <see cref="AvatarSpriteFolder"/>,
        /// so adding an icon is dropping a file in a folder rather than also
        /// remembering to drag it into a list.
        /// </summary>
        [MenuItem("DOTORI ON/Refresh Avatar Catalog From Folder")]
        public static void RefreshAvatarCatalogFromFolder()
        {
            var catalog = EnsureAvatarCatalogAsset();
            if (!AssetDatabase.IsValidFolder(AvatarSpriteFolder))
            {
                Debug.LogWarning(AvatarSpriteFolder + " does not exist yet. Create it and drop the icons in.");
                return;
            }

            var sprites = new List<Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { AvatarSpriteFolder }))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }

            sprites.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            var serialized = new SerializedObject(catalog);
            var icons = serialized.FindProperty("_icons");
            icons.arraySize = sprites.Count;
            for (var index = 0; index < sprites.Count; index++)
            {
                icons.GetArrayElementAtIndex(index).objectReferenceValue = sprites[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            catalog.Refresh();

            foreach (var problem in catalog.Problems())
            {
                Debug.LogWarning("\uc544\ubc14\ud0c0 \uce74\ud0c8\ub85c\uadf8: " + problem);
            }

            Debug.Log("Avatar catalog now lists " + catalog.Count + " icon(s) from " + AvatarSpriteFolder + ".");
        }

        /// <summary>
        /// The pixel font the UI is drawn in. Falls back to Unity's built-in
        /// face only if the asset is missing, so a rebuild in a checkout that
        /// lost the font still produces readable prefabs instead of blank ones.
        /// </summary>
        internal static Font PreviewFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>
        /// The body face. Gulim carries hand-drawn bitmaps for 11px through
        /// 25px, so any whole size in that range is crisp and anything below it
        /// is not. The builder only knows this one face; a rebuild is a reset,
        /// not a way to reproduce the typography.
        /// </summary>
        internal const string UiFontPath = "Assets/GULIM.TTC";
        private static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480f, 220f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
        }
        internal static void SetCardLine(Text text, float top, float height)
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
        internal static void Assign(UnityEngine.Object target, params (string name, UnityEngine.Object value)[] values)
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
