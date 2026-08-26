using System;
using System.IO;
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

        // Resources must keep its exact folder name for Resources.Load, so it is
        // nested here instead of taking a numbered folder of its own.
        public const string AppPath = PrefabFolder + "/Resources/TeamOverlay/TeamOverlayApp.prefab";

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

        public static bool AllPrefabsExist()
        {
            return File.Exists(CardPath) && File.Exists(MainViewPath) &&
                   File.Exists(NameViewPath) && File.Exists(AppPath);
        }

        private static void BuildAll()
        {
            EnsureFolder("Assets", "02. Prefabs");
            EnsureFolder(PrefabFolder, "Resources");
            EnsureFolder(PrefabFolder + "/Resources", "TeamOverlay");

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
                var detail = UiFactory.CreateText("Detail", root.transform, font, 9,
                    TextAnchor.MiddleCenter, TeamOverlayPalette.TextSecondary);
                SetCardLine(detail, 103f, 27f);
                detail.horizontalOverflow = HorizontalWrapMode.Wrap;
                detail.text = "출근 09:00";

                Assign(view,
                    ("_background", rootImage), ("_avatarBackground", avatar),
                    ("_avatarText", initial), ("_timerText", timer), ("_nameText", name),
                    ("_statusText", status), ("_detailText", detail));
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
                UiFactory.Stretch(dragArea.rectTransform, 0f, 0f, 280f, 0f);
                var dragHandle = dragArea.gameObject.AddComponent<WindowDragHandle>();
                var title = UiFactory.CreateText("Title", dragArea.transform, font, 12,
                    TextAnchor.MiddleLeft, TeamOverlayPalette.TextPrimary, FontStyle.Bold);
                title.text = "TEAM OVERLAY";
                UiFactory.Stretch(title.rectTransform, 10f, 0f, 0f, 0f);

                var fake = TopButton(topBar.transform, font, "FakeCheckIn", "가짜 출근", 204f, 72f);
                var switchAccount = TopButton(topBar.transform, font, "SwitchAccount", "이름변경", 150f, 54f);
                switchAccount.GetComponentInChildren<Text>().fontSize = 9;
                var topmost = TopButton(topBar.transform, font, "AlwaysOnTop", "TOP", 108f, 38f);
                var minimize = TopButton(topBar.transform, font, "Minimize", "—", 77f, 28f);
                var tray = TopButton(topBar.transform, font, "HideToTray", "숨김", 32f, 42f);
                tray.GetComponentInChildren<Text>().fontSize = 9;
                var exit = TopButton(topBar.transform, font, "Exit", "×", 3f, 27f, TeamOverlayPalette.Danger);

                var cardsRoot = UiFactory.CreateRect("MemberCards", background.transform);
                UiFactory.Stretch(cardsRoot.GetComponent<RectTransform>(), 6f, 46f, 6f, 36f);
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
                controls.rectTransform.anchorMin = Vector2.zero;
                controls.rectTransform.anchorMax = new Vector2(1f, 0f);
                controls.rectTransform.pivot = new Vector2(0.5f, 0f);
                controls.rectTransform.sizeDelta = new Vector2(0f, 43f);
                var checkIn = ControlButton(controls.transform, font, "CheckIn", "출근", -54f, 108f);
                var checkOut = ControlButton(controls.transform, font, "CheckOut", "퇴근", -153f, 66f, TeamOverlayPalette.Danger);
                var working = ControlButton(controls.transform, font, "Working", "작업중", -81f, 70f);
                var rest = ControlButton(controls.transform, font, "Break", "쉬는중", -5f, 70f);
                var meal = ControlButton(controls.transform, font, "Meal", "식사중", 71f, 70f);
                var feedback = UiFactory.CreateText("Feedback", controls.transform, font, 8,
                    TextAnchor.LowerCenter, TeamOverlayPalette.TextSecondary);
                feedback.text = "Supabase Auth 연결 · 팀 상태 Mock";
                UiFactory.Stretch(feedback.rectTransform, 4f, 0f, 4f, 31f);

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
                Set(serialized, "_hideToTrayButton", tray);
                Set(serialized, "_exitButton", exit);
                Set(serialized, "_switchAccountButton", switchAccount);
                Set(serialized, "_topmostLabel", topmost.GetComponentInChildren<Text>());
                Set(serialized, "_feedbackText", feedback);
                Set(serialized, "_windowDragHandle", dragHandle);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, MainViewPath).GetComponent<TeamOverlayView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
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
                Assign(root.GetComponent<TeamOverlayApp>(), ("_mainViewPrefab", mainPrefab), ("_firstRunNamePrefab", namePrefab));
                PrefabUtility.SaveAsPrefabAsset(root, AppPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Font PreviewFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480f, 220f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
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
