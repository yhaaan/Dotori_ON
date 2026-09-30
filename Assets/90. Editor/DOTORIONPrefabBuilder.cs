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

        // Resources.Load는 이름이 정확히 "Resources"인 폴더를 기준으로 경로를 찾으므로,
        // 이 폴더만 엔진이 정한 이름을 그대로 쓰고 번호 없이 Assets 바로 아래에 둡니다.
        public const string ResourceFolder = "Assets/Resources/DOTORION";
        public const string AppPath = ResourceFolder + "/DOTORIONApp.prefab";
        public const string SoundsPath = ResourceFolder + "/DOTORIONSounds.asset";
        public const string AvatarCatalogPath = ResourceFolder + "/TeamAvatarCatalog.asset";
        public const string ThemePath = ResourceFolder + "/DarkTheme.asset";

        /// <summary>팀이 프로필 아이콘 이미지를 넣어 두는 폴더입니다.</summary>
        public const string AvatarSpriteFolder = "Assets/04. Avatars";
        public const string DailyGiftSpritePath = "Assets/05. Sprites/DailyGift.png";

        /// <summary>
        /// 프리팹 안에서 아바타 선택창 자체의 높이. 창이 정확히 이만큼 위로 늘어나므로
        /// <c>WindowsOverlayWindow.AvatarPickerPanelHeight</c>와 같아야 합니다. 셀 두 줄과
        /// 제목이 들어가는 높이입니다.
        /// </summary>
        public const float AvatarPickerPanelHeight = 160f;

        /// <summary>
        /// 프리팹 안에서 소형 오버레이의 크기. 창이 정확히 이 크기로 바뀌므로
        /// <c>WindowsOverlayWindow.MiniWindowWidth</c>·<c>MiniWindowHeight</c>와 같아야 합니다.
        /// 기준 해상도 480 폭이 아니라 실제 픽셀로 만듭니다. 소형 오버레이가 보이는 동안
        /// 캔버스 스케일러가 꺼지기 때문입니다. 화면 옆에 세워 둘 수 있을 만큼 좁습니다.
        /// 각 줄이 상태 pill 안에 이름을 담고 있어서 나란히 놓을 것이 없습니다.
        /// </summary>
        public const float MiniPanelWidth = 75f;

        public const float MiniPanelHeight = 130f;

        /// <summary>
        /// 프리팹 안에서 개발자 대시보드의 높이. 창이 정확히 이만큼 늘어나므로
        /// <c>WindowsOverlayWindow.DashboardPanelHeight</c>와 같아야 합니다. 여섯 줄과
        /// 머리글, 바닥글, 확인 문구가 들어가는 높이입니다.
        /// </summary>
        public const float DashboardPanelHeight = 300f;

        /// <summary>
        /// 프리팹 안에서 설정 패널 자체의 높이. 창이 정확히 이만큼 아래로 늘어나므로
        /// <c>WindowsOverlayWindow.SettingsPanelHeight</c>와 같아야 합니다. 제목과 여섯 줄이
        /// 들어가는 높이입니다.
        /// </summary>
        public const float SettingsPanelHeight = 268f;

        public const string AutoStartRowLabel = "자동 시작";

        public const string AutoStartRowHint = "윈도우를 켤 때 같이 실행합니다.";

        public const string HideFromTaskbarRowLabel = "작업표시줄";

        public const string HideFromTaskbarRowHint = "켜면 시스템 트레이에만 표시합니다.";

        public const string UiScaleRowLabel = "UI 크기";

        public const string UiScaleRowHint = "4K 모니터에서 전체 화면을 확대합니다.";

        /// <summary>설정 패널 배치. 패널 위쪽에서 잰 픽셀입니다.</summary>
        private const float SettingsRowTop = 44f;
        private const float SettingsRowHeight = 28f;
        private const float SettingsRowSpacing = 8f;

        /// <summary>다음 설정 줄이 시작하는 간격입니다.</summary>
        public const float SettingsRowStep = SettingsRowHeight + SettingsRowSpacing;

        private const float DashboardRowTop = 60f;
        private const float DashboardRowHeight = 32f;
        private const float DashboardRowSpacing = 2f;

        /// <summary>소형 오버레이 배치. 패널 위쪽에서 잰 픽셀입니다.</summary>
        internal const float MiniDragStripHeight = 18f;
        internal const float MiniRowTop = 21f;
        internal const float MiniRowHeight = 25f;
        internal const float MiniRowSpacing = 2f;
        internal const int MiniRowCount = 4;
        /// <summary>
        /// 월 달력 배치. 통계 내용 영역 안의 픽셀입니다. 패널 폭 480에 일곱 칸을 1px 간격으로
        /// 놓고, 내용 영역 340에 맞는 여섯 줄을 둡니다.
        /// </summary>
        private const float CalendarTop = 24f;
        private const float CalendarHeight = 306f;
        private const float CalendarHeaderHeight = 14f;
        private const float CalendarLeft = 9f;
        private const float CalendarCellWidth = 65f;
        private const float CalendarCellHeight = 46f;
        private const float CalendarCellGap = 1f;

        /// <summary>
        /// 카드와 선택창에서 프로필 아이콘이 그려지는 크기. 아이콘이 타일을 가장자리까지
        /// 채우므로 카드의 타일 크기이기도 합니다. 픽셀아트는 24(2배)와 16(3배)에서 깔끔하게
        /// 나눠 떨어지고, 32는 반 픽셀에 걸립니다.
        /// </summary>
        internal const float AvatarIconSize = 48f;

        /// <summary>
        /// 카드 한 장이 화면에서 실제로 받는 크기이자 그림이 그려지는 크기. 카드 네 장과 5px
        /// 간격 세 개가 정수 픽셀에 떨어져야 픽셀아트가 리샘플링되지 않습니다.
        /// 4*113 + 3*5 = 467이라 480 창 안의 줄이 468처럼 둥근 값이 아니라 467 폭입니다.
        /// </summary>
        internal const float CardWidth = 113f;
        internal const float CardHeight = 138f;
        internal const float CardRowSpacing = 5f;

        /// <summary>카드 배치. 카드 위쪽에서 잰 픽셀입니다.</summary>
        internal const float CardAvatarTop = 24f;
        internal const float CardNameTop = 73f;
        internal const float CardStatusTop = 94f;
        internal const float CardDetailTop = 110f;

        /// <summary>
        /// 선택창의 셀은 아이콘보다 조금 크게 잡아, 선택된 색이 아이콘 둘레의 고리로
        /// 남게 합니다. 아이콘이 셀을 꽉 채우면 어느 것이 내 것인지 알려 주는 유일한
        /// 표시가 가려집니다.
        /// </summary>
        internal const float AvatarCellPadding = 2f;
        internal const float AvatarCellSize = AvatarIconSize + (AvatarCellPadding * 2f);
        internal const float AvatarCellSpacing = 4f;
        internal const int AvatarGridColumns = 8;

        /// <summary>
        /// 프리팹을 다시 만들면 아티스트가 넣은 스프라이트, 9-slice 경계, 손으로 맞춘 rect가
        /// 전부 사라지고, 지금 프리팹에는 다른 곳에 없는 아트가 들어 있습니다. 그래서
        /// 재생성은 메뉴에서 뺐습니다. DOTORI ON 메뉴의 어떤 항목도 클릭만으로 그 작업을
        /// 망칠 수 없습니다. 정말 초기화해야 할 때는 스크립트나 명령줄에서 이름으로 부를 수
        /// 있습니다.
        /// </summary>
        public static void RebuildPrefabsFromCommandLine() => BuildAll();

        /// <summary>
        /// UI를 고칠 때 보통 건드리는 프리팹 둘만 다시 만듭니다. 이름 모달과 앱 프리팹도
        /// 생성 대상이라, 전체를 다시 만들면 관련 없는 변경에 그쪽 YAML까지 흔들립니다.
        /// </summary>
        public static void RebuildCardAndMainView()
        {
            SeedPaletteFromThemeAsset();
            BuildMainView(BuildCard());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(MainViewPath);
        }

        public static void RebuildMainViewFromCommandLine()
        {
            SeedPaletteFromThemeAsset();
            var cardPrefab = AssetDatabase.LoadAssetAtPath<TeamMemberCardView>(CardPath);
            if (cardPrefab == null) throw new InvalidOperationException("멤버 카드 프리팹이 없습니다.");
            var mainPrefab = BuildMainView(cardPrefab);
            AssetDatabase.SaveAssets();
            Selection.activeObject = mainPrefab.gameObject;
        }

        /// <summary>
        /// 무엇이든 만들기 전에 팔레트가 테마 에셋을 가리키게 합니다.
        ///
        /// 빌더는 자기가 쓰는 프리팹에 색을 구워 넣는데, 이게 없으면 내장 기본값을 굽게 되어
        /// 에셋에서 다듬은 색이 다음 재생성에서 조용히 되돌아갑니다.
        /// </summary>
        private static void SeedPaletteFromThemeAsset()
        {
            DOTORIONPalette.Use(EnsureThemeAsset());
        }

        private static void BuildAll()
        {
            EnsureFolder("Assets", "02. Prefabs");
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "DOTORION");
            SeedPaletteFromThemeAsset();

            var cardPrefab = BuildCard();
            var mainPrefab = BuildMainView(cardPrefab);
            var namePrefab = BuildNameView();
            BuildApp(mainPrefab, namePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = mainPrefab.gameObject;
            Debug.Log("편집 가능한 DOTORI ON 프리팹을 만들었습니다. 빌드는 이 프리팹을 다시 만들거나 덮어쓰지 않습니다.");
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
                // 라벨 중 이름만 클릭되므로 raycast target이어야 하는 것도 이것뿐입니다.
                // 핸들은 꺼진 채로 나가고, 본인 카드에서 퇴근한 뒤에 Bind가 켭니다.
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
                // 드래그 영역 안에 둡니다. 막대에서 자리가 남은 곳이 여기뿐이고, 오른쪽은 버튼이
                // 가장자리까지 빈틈없이 차 있습니다. 처음 시도는 소형 버튼 뒤에 깔렸습니다.
                // Button은 자기 pointer-down을 소비하므로 그 주변 어디서나 창은 여전히 드래그됩니다.
                var dailyCheckIn = TopButtonAt(dragArea.transform, font, "DailyCheckIn", "출석",
                    126f, 36f, DOTORIONPalette.Accent);
                dailyCheckIn.GetComponentInChildren<Text>().fontSize = 9;
                var checkInPoints = UiFactory.CreateText("DailyCheckInPoints", dragArea.transform, font, 9,
                    TextAnchor.MiddleLeft, DOTORIONPalette.Accent, FontStyle.Bold);
                checkInPoints.text = "0P";
                UiFactory.AnchorTop(checkInPoints.rectTransform, 166f, 5f, 40f, 22f);
                // 이름 변경 버튼이 차지하던 자리와 폭을 이어받아 나머지 막대가 맞춰 둔 오프셋을
                // 그대로 유지합니다. 이름 변경은 바뀌는 이름 위로 옮겨 갔고, 더블클릭으로 합니다.
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
                // 왼쪽 6px, 오른쪽 7px: 홀수 13이 각 카드를 113.25가 아니라 정확히 CardWidth로
                // 만들고, 남는 반 픽셀은 아무것도 그려지지 않는 여백에 씁니다.
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
                // member_current_state.status_note의 24자 검사와 맞춥니다.
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
                // 통계 패널처럼 창 배경의 자식이고, 같은 방식으로 아래로 펼쳐집니다.
                var settingsPanel = BuildSettingsPanel(background.transform, font);
                // 창 배경의 자식이 아니라 형제입니다. 선택창이 캔버스 맨 위 띠를 차지하고 배경이
                // 그 아래로 밀리는 구조라, 기본 레이아웃 안의 어떤 것도 옮기지 않고 창이 위로
                // 늘어날 수 있습니다.
                var avatarPicker = BuildAvatarPickerPanel(root.transform, font);
                // 이것도 창 배경의 형제인데 이유가 다릅니다. 소형 오버레이는 전체 오버레이에서
                // 펼쳐지는 것이 아니라 그것을 통째로 대체합니다.
                var miniPanel = BuildMiniPanel(root.transform, font);
                // 통계 패널처럼 창 배경의 자식입니다. 오버레이를 대체하지 않고 그 아래로 펼쳐집니다.
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
        /// 카드의 아바타 타일을 클릭할 수 있게 하고, 고른 아이콘이 그려질 자리를 만듭니다.
        /// </summary>
        internal static void AttachAvatarPicking(Image avatar, out Button button, out Image icon)
        {
            // 타일은 상태색을 유지하므로 버튼이 색을 덧입히면 안 됩니다. hover로 타일이 다시
            // 칠해지면 상태가 바뀐 것처럼 읽힙니다. 고른 아이콘은 타일을 채우지 않고 안쪽에
            // 들어가서, 상태색이 아이콘 둘레의 테두리로 남습니다.
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
            // 가장자리까지 채웁니다. 타일 안에 여백이 있으면 그림이 자리보다 작게 보입니다.
            UiFactory.Stretch(icon.rectTransform);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;
            // 타일 위, 글자 머리글자 아래입니다. 아직 아무도 아이콘을 고르지 않았을 때 보이는 것이
            // 머리글자입니다.
            icon.transform.SetSiblingIndex(0);
        }

        /// <summary>
        /// 아이콘 격자. 셀은 여기서 만들지 않습니다. 카탈로그는 팀이 계속 늘리는 에셋이라,
        /// 패널이 실행 중에 템플릿 하나를 카탈로그 길이만큼 복제합니다.
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

            // RectMask2D는 마스크 스프라이트 없이 잘라 내고, 스크롤 rect는 viewport를 자기 것으로
            // 써서 패널 높이와 맞춰 둘 오브젝트가 하나 줄어듭니다.
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
            // 카드 타일과 같은 그려지는 크기라서 픽셀아트가 두 곳 모두 정수 픽셀에 놓이고, 한쪽에서만
            // 리샘플링되는 일이 없습니다.
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
        /// 소형 오버레이: 드래그 띠와 이름·상태 줄 네 개로, 멤버 카드 한 장 정도 크기입니다.
        /// 꺼진 채로 나가며, 앱이 창 배경과 둘을 함께 보여 주지 않고 서로 바꿔 끼웁니다.
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

            // 드래그하는 곳은 띠뿐입니다. 본문이 pointer down에서 네이티브 창 드래그를 시작하면
            // 전체 오버레이로 돌아오는 더블클릭의 첫 번째 클릭이 사라집니다.
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
        /// 소형 오버레이의 한 줄. 안의 어떤 것도 raycast target이 아닙니다. 본문 전체가 패널의
        /// 더블클릭 처리기에 닿아야 하고, 클릭을 먹는 pill이 있으면 오버레이를 되돌릴 수 없는
        /// 죽은 영역이 생깁니다.
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

            // pill이 곧 줄입니다. 폭 75px에서는 옆에 놓을 자리가 없어서, 이름이 상태색과 폭을
            // 다투지 않고 그 위에 올라앉습니다.
            // 가운데 정렬 대신 위에서 잰 정수 픽셀 오프셋으로 놓습니다. 25 안의 18, 18 안의 7은
            // 가운데에 놓으면 반 픽셀에 걸리고, 픽셀 폰트가 흐려지는 곳이 반 픽셀입니다.
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
        /// 둥근 pill과 점은 평평한 사각형뿐인 UI에서 모양이 있는 유일한 그래픽입니다. 둘 다
        /// Unity가 기본 제공하므로 프로젝트에 스프라이트를 따로 들이지 않습니다.
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
            // WindowsOverlayWindow.StatisticsPanelHeight와 맞춰야 합니다. 패널이 열릴 때 창이
            // 정확히 이만큼 늘어납니다.
            panelRect.sizeDelta = new Vector2(0f, 424f);
            var panelView = panel.gameObject.AddComponent<TeamStatisticsPanelView>();

            var heading = UiFactory.CreateText("Heading", panel.transform, font, 14,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary);
            heading.text = "\uD300 \uD1B5\uACC4";
            UiFactory.AnchorTop(heading.rectTransform, 14f, 8f, 150f, 24f);
            var period = UiFactory.CreateText("Period", panel.transform, font, 10,
                TextAnchor.MiddleRight, DOTORIONPalette.TextSecondary);
            period.text = "2026.08.21 - 2026.08.27";
            UiFactory.AnchorTop(period.rectTransform, 236f, 8f, 230f, 24f);

            var dailyTab = UiFactory.CreateButton("DailyTab", panel.transform, font, "\uB0B4 \uD1B5\uACC4", null,
                DOTORIONPalette.Accent);
            dailyTab.GetComponentInChildren<Text>().fontStyle = FontStyle.Normal;
            UiFactory.AnchorTop(dailyTab.GetComponent<RectTransform>(), 14f, 39f, 88f, 28f);
            var rankingTab = UiFactory.CreateButton("RankingTab", panel.transform, font, "\uB7AD\uD0B9");
            rankingTab.GetComponentInChildren<Text>().fontStyle = FontStyle.Normal;
            UiFactory.AnchorTop(rankingTab.GetComponent<RectTransform>(), 108f, 39f, 88f, 28f);

            // 기간은 두 탭에 모두 적용되므로 탭 옆에 둡니다.
            var periodButtons = new Button[3];
            var periodLabels = new[] { "7\uC77C", "\uC774\uBC88 \uB2EC", "\uB204\uC801" };
            var periodNames = new[] { "PeriodSevenDays", "PeriodThisMonth", "PeriodAllTime" };
            for (var index = 0; index < periodButtons.Length; index++)
            {
                periodButtons[index] = UiFactory.CreateButton(
                    periodNames[index], panel.transform, font, periodLabels[index], null,
                    index == 0 ? DOTORIONPalette.Accent : DOTORIONPalette.Button);
                var periodText = periodButtons[index].GetComponentInChildren<Text>();
                periodText.fontSize = 11;
                periodText.fontStyle = FontStyle.Normal;
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

            // 행이 쓰는 자리를 함께 씁니다. 월 달력과 다른 기간은 같은 일별 집계를 읽는 두
            // 방식이고 둘 중 하나만 화면에 나오기 때문입니다.
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
                var metricText = metricButtons[index].GetComponentInChildren<Text>();
                metricText.fontSize = 11;
                metricText.fontStyle = FontStyle.Normal;
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
        /// 설정 패널. 상단바에서 하나씩 자리를 차지하던 스위치들이 이름 붙은 줄로 여기 있고,
        /// 각각이 무엇을 하는지 설명을 적을 자리도 여기뿐입니다.
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
            var hideFromTaskbar = SettingsSwitchRow(panel.transform, font, "HideFromTaskbar",
                HideFromTaskbarRowLabel, HideFromTaskbarRowHint,
                SettingsRowTop + (SettingsRowStep * 3f));
            var uiScale = SettingsSwitchRow(panel.transform, font, "UiScale",
                UiScaleRowLabel, UiScaleRowHint,
                SettingsRowTop + (SettingsRowStep * 4f));
            uiScale.GetComponentInChildren<Text>().text = "100%";

            var versionTop = SettingsRowTop + (SettingsRowStep * 5f);
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
            Set(serialized, "_hideFromTaskbarButton", hideFromTaskbar);
            Set(serialized, "_hideFromTaskbarValue", hideFromTaskbar.GetComponentInChildren<Text>());
            Set(serialized, "_uiScaleButton", uiScale);
            Set(serialized, "_uiScaleValue", uiScale.GetComponentInChildren<Text>());
            Set(serialized, "_versionText", version);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return panelView;
        }

        /// <summary>
        /// 이름, 하는 일을 설명하는 줄, 오른쪽의 스위치. 스위치 라벨이 곧 값이라서 옆에 라벨을
        /// 따로 두지 않고 패널이 버튼 자신의 글자에 켜짐·꺼짐을 씁니다.
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
        /// 요일 머리글 아래 일곱 칸짜리 여섯 줄, 월요일부터. 여섯째 줄은 필요한 달을 위한
        /// 것이고(토요일에 시작하는 31일 달) 나머지 때는 꺼 둡니다.
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
                    TextAnchor.MiddleCenter, DOTORIONPalette.TextSecondary);
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
            Set(serialized, "_dailyGiftUnclaimedSprite", LoadSprite(DailyGiftSpritePath, "DailyGift_0"));
            Set(serialized, "_dailyGiftClaimedSprite", LoadSprite(DailyGiftSpritePath, "DailyGift_1"));
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
            // 여기에는 팔레트 색이 없습니다. 빈 날의 기본색은 프리팹이 정하고, 실행 중 출석
            // 음영은 Image에 저장된 값에서 시작합니다.
            var background = UiFactory.CreateImage(
                "CalendarDay_" + (index + 1), parent);
            UiFactory.AnchorTop(
                background.rectTransform, left, top, CalendarCellWidth, CalendarCellHeight);
            // 칸은 격자의 유일한 컨트롤입니다. 어느 칸을 눌러도 모든 칸이 보여 주는 내용이
            // 바뀌므로 raycast를 받아야 합니다.
            background.raycastTarget = true;
            var cell = background.gameObject.AddComponent<TeamCalendarDayView>();

            var day = UiFactory.CreateText("Day", background.transform, font, 9,
                TextAnchor.UpperLeft, DOTORIONPalette.TextSecondary);
            day.text = "1";
            UiFactory.AnchorTop(day.rectTransform, 4f, 3f, 24f, 13f);

            var duration = UiFactory.CreateText("Duration", background.transform, font, 11,
                TextAnchor.MiddleCenter, DOTORIONPalette.TextPrimary);
            duration.text = "00:00";
            // 세부 내역의 쌓인 세 줄이 들어갈 높이이고, 빠듯해서 잘리지 않도록 넘침을 허용합니다.
            UiFactory.AnchorTop(duration.rectTransform, 0f, 13f, CalendarCellWidth, 30f);
            duration.lineSpacing = 0.85f;
            duration.verticalOverflow = VerticalWrapMode.Overflow;
            duration.raycastTarget = false;

            var dailyGift = UiFactory.CreateImage("DailyGift", background.transform);
            dailyGift.raycastTarget = false;
            dailyGift.preserveAspect = true;
            dailyGift.gameObject.SetActive(false);
            var dailyGiftRect = dailyGift.rectTransform;
            dailyGiftRect.anchorMin = Vector2.one;
            dailyGiftRect.anchorMax = Vector2.one;
            dailyGiftRect.pivot = Vector2.one;
            dailyGiftRect.anchoredPosition = new Vector2(-2f, -2f);

            Assign(cell,
                ("_background", background), ("_dayLabel", day), ("_durationLabel", duration),
                ("_dailyGiftImage", dailyGift));
            return cell;
        }

        private static Sprite LoadSprite(string assetPath, string spriteName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            throw new InvalidOperationException(
                "스프라이트 '" + spriteName + "'을(를) " + assetPath + "에서 찾지 못했습니다.");
        }

        /// <summary>
        /// 개발자 대시보드: 명단을 숫자 줄로 보여 줍니다. 일부러 꾸미지 않았습니다. 조작하는
        /// 일보다 읽는 일이 훨씬 많고, 여기서 파괴할 수 있는 유일한 것은 대상 이름을 말해
        /// 주는 확인 뒤에 있습니다.
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
        /// 떠 있는 동안 패널 전체를 덮습니다. 한 줄에 대한 질문에 답하기 전에는 아래 목록을
        /// 누를 수 없게 하기 위해서입니다.
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
                TextAnchor.MiddleCenter, DOTORIONPalette.TextPrimary);
            UiFactory.AnchorTop(date.rectTransform, 6f, 0f, 70f, 38f);
            var work = UiFactory.CreateText("Work", background.transform, font, 9,
                TextAnchor.MiddleLeft, DOTORIONPalette.Working);
            UiFactory.AnchorTop(work.rectTransform, 80f, 1f, 72f, 17f);
            var attendance = UiFactory.CreateText("Attendance", background.transform, font, 9,
                TextAnchor.MiddleLeft, DOTORIONPalette.Accent);
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
                TextAnchor.MiddleCenter, DOTORIONPalette.Accent);
            UiFactory.AnchorTop(rank.rectTransform, 8f, 0f, 32f, 50f);
            var name = UiFactory.CreateText("Name", background.transform, font, 12,
                TextAnchor.MiddleLeft, DOTORIONPalette.TextPrimary);
            UiFactory.AnchorTop(name.rectTransform, 46f, 3f, 104f, 22f);
            // 이름 아래, 사람에 대한 둘째 줄이 들어갈 자리입니다. 랭킹이 실제로 정렬되는 숫자를
            // 비좁게 하지 않습니다.
            var points = UiFactory.CreateText("Points", background.transform, font, 9,
                TextAnchor.UpperLeft, DOTORIONPalette.Accent);
            UiFactory.AnchorTop(points.rectTransform, 46f, 24f, 104f, 18f);
            points.text = "0P";
            var work = UiFactory.CreateText("Work", background.transform, font, 10,
                TextAnchor.MiddleLeft, DOTORIONPalette.Working);
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
        /// 왼쪽에서 재는 상단바 버튼. 보통은 오른쪽에서 재는데, 상단바 오른쪽은 빈틈없이 차
        /// 있어서 그쪽이 필요합니다.
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
                // 이름 변경일 때만 보입니다. 처음 실행에는 되돌아갈 곳이 없어서 숨겨 둡니다.
                var cancel = UiFactory.CreateButton("Cancel", panel.transform, font, "×");
                UiFactory.AnchorRight(cancel.GetComponent<RectTransform>(), 12f, 10f, 26f, 26f);
                cancel.gameObject.SetActive(false);
                // 제목이 유일하게 말하는 줄입니다. 입력을 권하고, 입력이 잘못되었으면 무엇이
                // 잘못이었는지 말합니다. 입력칸 아래에 힌트를 따로 두면 제목과 엇갈리는 두 번째
                // 목소리가 될 뿐입니다.
                Assign(root.GetComponent<FirstRunNameView>(), ("_nameInput", input),
                    ("_confirmButton", confirm), ("_cancelButton", cancel),
                    ("_messageText", title));
                return PrefabUtility.SaveAsPrefabAsset(root, NameViewPath).GetComponent<FirstRunNameView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// "새 버전이 있습니다" 모달. 같은 방식으로 화면을 가로막기 때문에 처음 실행 이름
        /// 모달과 같은 모양으로 만들고, 둘이 동시에 화면을 원하는 경우를 위해 정렬 순서에서
        /// 그 위에 둡니다.
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

                // 네는 제안되는 답이라 강조색을 받고, 나중에는 또 하나의 외침이 아니라 그 옆의
                // 조용한 버튼입니다.
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
                    ("_avatarCatalog", EnsureAvatarCatalogAsset()),
                    ("_theme", EnsureThemeAsset()));
                PrefabUtility.SaveAsPrefabAsset(root, AppPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// 효과음 설정 에셋을 처음에만 만들고 이후에는 건드리지 않습니다. 손으로 고른 클립이
        /// 들어 있어서, 프리팹처럼 재생성하면서 초기화되면 안 됩니다.
        /// </summary>
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
            Debug.Log(SoundsPath + "을(를) 만들었습니다. 팀의 오디오 클립을 여기에 넣으세요.");
            return created;
        }

        /// <summary>
        /// 배포하는 색 구성을 처음에만 만들고 이후에는 건드리지 않습니다. 효과음 에셋과 같은
        /// 이유로, 누가 색을 다듬어 두었다면 프리팹 재생성이 기본값을 되돌려서는 안 됩니다.
        ///
        /// 새 에셋은 내장 구성을 그대로 담고 시작합니다. 값이 <see cref="DOTORIONTheme"/>의
        /// 필드 기본값이라서 에셋을 만든다고 오버레이 모습이 달라지지 않습니다.
        /// </summary>
        public static DOTORIONTheme EnsureThemeAsset()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "DOTORION");
            var existing = AssetDatabase.LoadAssetAtPath<DOTORIONTheme>(ThemePath);
            if (existing != null)
            {
                return existing;
            }

            var created = ScriptableObject.CreateInstance<DOTORIONTheme>();
            AssetDatabase.CreateAsset(created, ThemePath);
            AssetDatabase.SaveAssets();
            Debug.Log(ThemePath + "을(를) 만들었습니다. 내장 색 구성을 담고 있습니다.");
            return created;
        }

        /// <summary>
        /// 아이콘 카탈로그를 처음에만 만들고 이후에는 건드리지 않습니다. 효과음 에셋과 같은
        /// 이유로, 손으로 고른 그림이 들어 있어서 프리팹 재생성이 비우면 안 됩니다.
        /// </summary>
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
            Debug.Log(AvatarCatalogPath + "을(를) 만들었습니다. 팀의 프로필 아이콘을 여기에 넣으세요.");
            return created;
        }

        /// <summary>
        /// <see cref="AvatarSpriteFolder"/>의 모든 스프라이트로 카탈로그를 채웁니다. 아이콘을
        /// 더하는 일이 폴더에 파일을 넣는 것으로 끝나고, 목록에 따로 끌어다 놓을 필요가
        /// 없습니다.
        /// </summary>
        [MenuItem("DOTORI ON/아바타 카탈로그 폴더에서 갱신")]
        public static void RefreshAvatarCatalogFromFolder()
        {
            var catalog = EnsureAvatarCatalogAsset();
            if (!AssetDatabase.IsValidFolder(AvatarSpriteFolder))
            {
                Debug.LogWarning(AvatarSpriteFolder + " 폴더가 아직 없습니다. 폴더를 만들고 아이콘을 넣으세요.");
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

            Debug.Log("아바타 카탈로그에 " + AvatarSpriteFolder + "의 아이콘 " + catalog.Count + "개를 담았습니다.");
        }

        /// <summary>
        /// UI를 그리는 픽셀 폰트. 에셋이 없을 때만 Unity 내장 폰트로 대체하므로, 폰트를
        /// 잃은 체크아웃에서 재생성해도 빈 프리팹이 아니라 읽을 수 있는 프리팹이 나옵니다.
        /// </summary>
        internal static Font PreviewFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>
        /// 본문 폰트. 굴림은 11px부터 25px까지 손으로 그린 비트맵을 갖고 있어서, 그 범위의
        /// 정수 크기는 또렷하고 그 아래는 그렇지 않습니다. 빌더는 이 폰트 하나만 알고,
        /// 재생성은 초기화이지 타이포그래피를 재현하는 수단이 아닙니다.
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
            if (property == null) throw new InvalidOperationException("직렬화된 필드가 없습니다: " + name);
            property.objectReferenceValue = value;
        }
        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
