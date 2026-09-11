using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DOTORION.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.Editor
{
    public sealed class DOTORIONThemeEditor : EditorWindow
    {
        public const string CatalogPath = "Assets/Resources/DOTORION/ThemeCatalog.asset";
        private DOTORIONTheme _theme;
        private string _search = "";
        private Vector2 _scroll;
        private int _filter;
        private bool _overridesOnly;
        private int _tab;

        [MenuItem("DOTORI ON/Theme Editor")]
        public static void Open() => GetWindow<DOTORIONThemeEditor>("Theme Editor");
        public static void Open(DOTORIONTheme theme)
        {
            var window = GetWindow<DOTORIONThemeEditor>("Theme Editor");
            window._theme = theme;
            window._tab = 0;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("기본 팔레트로 공통 색을 바꾸고, 이미지 교체 탭에서 원본별 새 이미지를 지정하세요. 특정 UI만 다르게 꾸밀 때 개별 예외를 사용합니다.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("기준 UI 수집 / 새 항목 추가")) Collect();
                if (GUILayout.Button("새 테마")) Create(false);
                using (new EditorGUI.DisabledScope(_theme == null))
                    if (GUILayout.Button("테마 복제")) Create(true);
            }
            var next = (DOTORIONTheme)EditorGUILayout.ObjectField("편집할 테마", _theme, typeof(DOTORIONTheme), false);
            if (next != _theme) _theme = next;
            if (_theme == null) return;
            if (_theme.Catalog == null)
            {
                if (GUILayout.Button("기준 목록 연결"))
                {
                    Undo.RecordObject(_theme, "Assign catalog");
                    _theme.Catalog = AssetDatabase.LoadAssetAtPath<DOTORIONThemeCatalog>(CatalogPath);
                    EditorUtility.SetDirty(_theme);
                }
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("저장")) AssetDatabase.SaveAssets();
                if (GUILayout.Button("기본 테마로 지정")) SetDefault();
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    if (GUILayout.Button("실행 중 적용")) { DOTORIONPalette.Use(_theme); foreach (var b in UnityEngine.Object.FindObjectsByType<DOTORIONThemeBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None)) b.Apply(); }
            }
            _tab = GUILayout.Toolbar(_tab, new[] { "기본 팔레트", "이미지 교체", "개별 예외 (고급)" });
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_tab == 0) DrawPalette(_theme);
            else if (_tab == 1) DrawSprites();
            else
            {
            EditorGUILayout.HelpBox("일반적인 테마 제작은 기본 팔레트와 이미지 교체만 사용하면 됩니다. 여기서 지정한 값이 공통 설정보다 우선합니다.", MessageType.Info);
            _search = EditorGUILayout.TextField("항목 검색", _search);
            _filter = EditorGUILayout.Popup("종류", _filter, new[] { "전체", "이미지 / 글자", "상태 색상", "상태 이미지", "버튼 상태" });
            _overridesOnly = EditorGUILayout.Toggle("변경한 항목만", _overridesOnly);
            foreach (var slot in _theme.Catalog.Slots)
            {
                if (slot.Label.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var value = _theme.Find(slot.Id);
                if (_overridesOnly && (value == null || value.Mode == ThemeMode.Original)) continue;
                if (_filter == 1 && slot.Target != ThemeTarget.Graphic) continue;
                if (_filter == 2 && slot.Target != ThemeTarget.ColorField) continue;
                if (_filter == 3 && slot.Target != ThemeTarget.SpriteField) continue;
                if (_filter == 4 && slot.Target != ThemeTarget.ButtonColor && slot.Target != ThemeTarget.ButtonSprite) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(slot.Label, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("공통 역할: " + ThemeRoleRules.Label(ThemeRoleRules.RoleOf(slot)), EditorStyles.miniLabel);
                    if (value != null && GUILayout.Button("개별 설정 해제 · 공통 설정 따르기"))
                    {
                        Undo.RecordObject(_theme, "Clear theme exception");
                        _theme.Overrides.Remove(value);
                        EditorUtility.SetDirty(_theme);
                        continue;
                    }
                    if (value == null)
                    {
                        EditorGUILayout.LabelField("공통 설정 사용 중", EditorStyles.miniLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            Preview(slot.Sprite, slot.Color);
                            Preview(_theme.ResolveSprite(slot), _theme.ResolveColor(slot));
                        }
                        if (GUILayout.Button("이 항목만 개별 설정"))
                        {
                            Undo.RecordObject(_theme, "Add theme exception");
                            _theme.Overrides.Add(new ThemeOverride
                            {
                                Id = slot.Id,
                                Mode = slot.AllowsSprite ? ThemeMode.SpriteSwap : ThemeMode.Tint,
                                Sprite = _theme.ResolveSprite(slot),
                                Color = _theme.ResolveColor(slot)
                            });
                            EditorUtility.SetDirty(_theme);
                        }
                        continue;
                    }
                    if (slot.RuntimeColor || slot.RuntimeSprite)
                        EditorGUILayout.LabelField("상태 코드 관리: " + (slot.RuntimeColor ? "색상 " : "") + (slot.RuntimeSprite ? "이미지" : ""), EditorStyles.miniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.ObjectField("기준", slot.Sprite, typeof(Sprite), false);
                            EditorGUILayout.ColorField(slot.Color, GUILayout.Width(80));
                        }
                    }
                    var mode = value != null ? value.Mode : ThemeMode.Original;
                    EditorGUI.BeginChangeCheck();
                    bool colorOnly = !slot.AllowsSprite;
                    bool spriteOnly = slot.Target == ThemeTarget.SpriteField || slot.Target == ThemeTarget.ButtonSprite;
                    if (colorOnly)
                        mode = EditorGUILayout.Popup("적용 방식", mode == ThemeMode.Original ? 0 : 1, new[] { "Original · 기준 유지", "Tint · 색상 지정" }) == 0 ? ThemeMode.Original : ThemeMode.Tint;
                    else if (spriteOnly)
                        mode = EditorGUILayout.Popup("적용 방식", mode == ThemeMode.Original ? 0 : 1, new[] { "Original · 기준 유지", "SpriteSwap · 이미지 교체" }) == 0 ? ThemeMode.Original : ThemeMode.SpriteSwap;
                    else
                        mode = (ThemeMode)EditorGUILayout.EnumPopup("적용 방식", mode);
                    var color = value != null ? value.Color : slot.Color;
                    var sprite = value != null ? value.Sprite : slot.Sprite;
                    if (mode == ThemeMode.SpriteSwap && !colorOnly && !slot.RuntimeSprite)
                        sprite = (Sprite)EditorGUILayout.ObjectField("새 이미지", sprite, typeof(Sprite), false);
                    if (mode != ThemeMode.Original && (!slot.RuntimeColor || slot.Label.Contains("Calendar")) && slot.Target != ThemeTarget.SpriteField && slot.Target != ThemeTarget.ButtonSprite)
                        color = EditorGUILayout.ColorField("새 색상", color);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_theme, "Edit theme slot");
                        if (value == null) { value = new ThemeOverride { Id = slot.Id }; _theme.Overrides.Add(value); }
                        if (mode == ThemeMode.SpriteSwap && value.Mode != mode && !colorOnly) color = Color.white;
                        value.Mode = mode; value.Sprite = sprite; value.Color = color;
                        EditorUtility.SetDirty(_theme);
                    }
                    if (mode == ThemeMode.SpriteSwap && sprite == null) EditorGUILayout.HelpBox("이미지 미할당: 기준 이미지를 사용합니다.", MessageType.Warning);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Preview(slot.Sprite, slot.Color);
                        Preview(mode == ThemeMode.SpriteSwap && sprite != null ? sprite : slot.Sprite, mode == ThemeMode.Original ? slot.Color : color);
                    }
                }
            }
            }
            EditorGUILayout.EndScrollView();
        }

        public static void DrawPalette(DOTORIONTheme theme)
        {
            EditorGUILayout.HelpBox("19개 역할의 색을 한 번씩 설정합니다. 수정하지 않은 역할은 기존 색을 유지합니다. 투명도와 원색 이미지의 흰 틴트는 보존됩니다.", MessageType.None);
            foreach (ThemeColorRole role in Enum.GetValues(typeof(ThemeColorRole)))
            {
                if (role == ThemeColorRole.None) continue;
                var entry = theme.Palette.Find(item => item.Role == role);
                var slots = theme.Catalog != null ? theme.Catalog.Slots.Where(slot => ThemeRoleRules.RoleOf(slot) == role).ToArray() : Array.Empty<ThemeSlot>();
                var baseline = slots.GroupBy(slot => ColorUtility.ToHtmlStringRGB(slot.Color)).OrderByDescending(group => group.Count()).FirstOrDefault()?.First().Color
                    ?? LegacyColor(theme, role);
                var color = entry != null ? entry.Color : baseline;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    color = EditorGUILayout.ColorField(new GUIContent(ThemeRoleRules.Label(role), slots.Length + "개 연결 + 코드 팔레트"), color, true, false, false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(theme, "Change shared palette");
                        if (entry == null) { entry = new ThemePaletteOverride { Role = role }; theme.Palette.Add(entry); }
                        entry.Color = color;
                        EditorUtility.SetDirty(theme);
                    }
                    using (new EditorGUI.DisabledScope(entry == null))
                    {
                        if (GUILayout.Button("복원", GUILayout.Width(45)))
                        {
                            Undo.RecordObject(theme, "Restore shared palette");
                            theme.Palette.Remove(entry);
                            EditorUtility.SetDirty(theme);
                        }
                    }
                }
            }
            var count = theme.Overrides.Count;
            if (count > 0) EditorGUILayout.HelpBox("개별 예외 " + count + "개는 공통 설정보다 우선합니다. 필요하면 고급 탭에서 해제하세요.", MessageType.Info);
        }

        private static Color LegacyColor(DOTORIONTheme theme, ThemeColorRole role)
        {
            var property = typeof(DOTORIONTheme).GetProperty(role.ToString());
            if (property != null && property.PropertyType == typeof(Color)) return (Color)property.GetValue(theme);
            if (role == ThemeColorRole.ButtonPressed) return new Color(.72f, .78f, .88f);
            if (role == ThemeColorRole.ButtonDisabled) return new Color(.48f, .52f, .58f);
            return Color.black;
        }

        private void DrawSprites()
        {
            var groups = _theme.Catalog.Slots.Where(slot => slot.Sprite != null && !slot.RuntimeSprite).GroupBy(slot => slot.Sprite).OrderBy(group => group.Key.name).ToArray();
            EditorGUILayout.HelpBox("원본 이미지 " + groups.Length + "종만 표시합니다. 한 번 교체하면 같은 원본을 쓰는 UI에 함께 적용됩니다. 사용자 아바타는 제외합니다.", MessageType.None);
            foreach (var group in groups)
            {
                var entry = _theme.Sprites.Find(item => item.Original == group.Key);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(group.Key.name + " · " + group.Count() + "개 연결", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("원본", group.Key, typeof(Sprite), false);
                    EditorGUI.BeginChangeCheck();
                    var replacement = (Sprite)EditorGUILayout.ObjectField("새 이미지", entry?.Replacement, typeof(Sprite), false);
                    var tint = EditorGUILayout.Toggle("팔레트 / 틴트 유지", entry == null || entry.PreserveTint);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_theme, "Replace shared sprite");
                        if (entry == null) { entry = new ThemeSpriteOverride { Original = group.Key }; _theme.Sprites.Add(entry); }
                        entry.Replacement = replacement; entry.PreserveTint = tint;
                        EditorUtility.SetDirty(_theme);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Preview(group.Key, Color.white);
                        Preview(replacement != null ? replacement : group.Key, Color.white);
                    }
                }
            }
        }

        private static void Preview(Sprite sprite, Color color)
        {
            var rect = GUILayoutUtility.GetRect(100, 54);
            EditorGUI.DrawRect(rect, new Color(.22f, .22f, .22f));
            if (sprite == null) { EditorGUI.DrawRect(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8), color); return; }
            var texture = AssetPreview.GetAssetPreview(sprite);
            if (texture == null) texture = AssetPreview.GetMiniThumbnail(sprite);
            if (texture == null) return;
            var old = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        private void Create(bool duplicate)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DOTORIONThemeCatalog>(CatalogPath);
            if (catalog == null) { Collect(); catalog = AssetDatabase.LoadAssetAtPath<DOTORIONThemeCatalog>(CatalogPath); }
            var path = EditorUtility.SaveFilePanelInProject("테마 저장", duplicate ? _theme.name + " Copy" : "NewTheme", "asset", "테마 이름과 저장 위치를 선택하세요.");
            if (string.IsNullOrEmpty(path)) return;
            _theme = duplicate ? Instantiate(_theme) : CreateInstance<DOTORIONTheme>();
            _theme.Catalog = catalog;
            AssetDatabase.CreateAsset(_theme, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = _theme;
        }

        private void SetDefault()
        {
            const string path = "Assets/Resources/DOTORION/DOTORIONApp.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var data = new SerializedObject(root.GetComponent<DOTORIONApp>());
                data.FindProperty("_theme").objectReferenceValue = _theme;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("DOTORI ON/Themes/Collect Baseline UI")]
        public static void Collect()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before collecting the baseline.");
            var catalog = AssetDatabase.LoadAssetAtPath<DOTORIONThemeCatalog>(CatalogPath);
            if (catalog == null) { catalog = CreateInstance<DOTORIONThemeCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            var slots = new List<ThemeSlot>();
            var usedIds = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/02. Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var dynamicColors = new HashSet<Graphic>();
                    var dynamicSprites = new HashSet<Graphic>();
                    foreach (var view in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (view == null || view is DOTORIONThemeBinding) continue;
                        var script = MonoScript.FromMonoBehaviour(view);
                        var source = script != null ? script.text : "";
                        foreach (var field in view.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                        {
                            if (!(field.GetValue(view) is Graphic graphic)) continue;
                            if (Regex.IsMatch(source, Regex.Escape(field.Name) + @"\.color\s*=")) dynamicColors.Add(graphic);
                            if (Regex.IsMatch(source, Regex.Escape(field.Name) + @"\.sprite\s*=")) dynamicSprites.Add(graphic);
                        }
                    }
                    // These views write through local variables rather than their serialized Graphic field.
                    foreach (var view in root.GetComponentsInChildren<DOTORIONView>(true))
                    {
                        var data = new SerializedObject(view);
                        var button = data.FindProperty("_dailyCheckInButton")?.objectReferenceValue as Button;
                        if (button != null && button.targetGraphic != null) dynamicSprites.Add(button.targetGraphic);
                    }
                    foreach (var view in root.GetComponentsInChildren<AvatarPickerPanelView>(true))
                    {
                        var data = new SerializedObject(view);
                        var button = data.FindProperty("_optionTemplate")?.objectReferenceValue as Button;
                        if (button != null && button.targetGraphic != null) dynamicColors.Add(button.targetGraphic);
                        var icon = button != null ? button.transform.Find("Icon")?.GetComponent<Graphic>() : null;
                        if (icon != null) { dynamicSprites.Add(icon); dynamicColors.Add(icon); }
                    }
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        var binding = transform.GetComponent<DOTORIONThemeBinding>();
                        var old = binding != null ? binding.Entries : new List<DOTORIONThemeBinding.Entry>();
                        var entries = new List<DOTORIONThemeBinding.Entry>();
                        var label = Path.GetFileNameWithoutExtension(path) + "/" + AnimationUtility.CalculateTransformPath(transform, root.transform);
                        Action<Component, string, ThemeTarget, Color, Sprite> add = (target, member, kind, color, sprite) =>
                        {
                            var previous = old.Find(e => e.Target == target && e.Member == member && e.Slot.Target == kind);
                            var slot = previous != null ? previous.Slot : new ThemeSlot { Id = Guid.NewGuid().ToString("N"), Color = color, Sprite = sprite, Target = kind, Mode = sprite == null ? ThemeMode.Tint : ThemeMode.Original };
                            if (!usedIds.Add(slot.Id)) { slot.Id = Guid.NewGuid().ToString("N"); usedIds.Add(slot.Id); }
                            slot.Label = label + " · " + target.GetType().Name + "/" + member;
                            slot.RuntimeColor = target is Graphic g && dynamicColors.Contains(g);
                            slot.RuntimeSprite = target is Graphic s && dynamicSprites.Contains(s);
                            slot.AllowsSprite = target is Image || kind == ThemeTarget.SpriteField || kind == ThemeTarget.ButtonSprite;
                            entries.Add(new DOTORIONThemeBinding.Entry { Slot = slot, Target = target, Member = member, OriginalTransition = target is Selectable selectable ? selectable.transition : Selectable.Transition.None });
                            slots.Add(slot);
                        };
                        foreach (var graphic in transform.GetComponents<Graphic>()) add(graphic, "Appearance", ThemeTarget.Graphic, graphic.color, (graphic as Image)?.sprite);
                        foreach (var button in transform.GetComponents<Selectable>())
                        {
                            foreach (var p in typeof(ColorBlock).GetProperties().Where(p => p.PropertyType == typeof(Color) && p.CanWrite)) add(button, p.Name, ThemeTarget.ButtonColor, (Color)p.GetValue(button.colors), null);
                            foreach (var p in typeof(SpriteState).GetProperties().Where(p => p.PropertyType == typeof(Sprite) && p.CanWrite)) add(button, p.Name, ThemeTarget.ButtonSprite, Color.white, (Sprite)p.GetValue(button.spriteState));
                        }
                        foreach (var view in transform.GetComponents<MonoBehaviour>())
                        {
                            if (view == null || view is DOTORIONThemeBinding || view.GetType().Namespace != "DOTORION.UI") continue;
                            foreach (var field in view.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                            {
                                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;
                                if (field.FieldType == typeof(Color)) add(view, field.Name, ThemeTarget.ColorField, (Color)field.GetValue(view), null);
                                if (field.FieldType == typeof(Sprite)) add(view, field.Name, ThemeTarget.SpriteField, Color.white, (Sprite)field.GetValue(view));
                            }
                        }
                        if (entries.Count == 0) continue;
                        if (binding == null) binding = transform.gameObject.AddComponent<DOTORIONThemeBinding>();
                        binding.Entries = entries;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            catalog.Slots = slots;
            EditorUtility.SetDirty(catalog);
            foreach (var themeGuid in AssetDatabase.FindAssets("t:DOTORIONTheme"))
            {
                var theme = AssetDatabase.LoadAssetAtPath<DOTORIONTheme>(AssetDatabase.GUIDToAssetPath(themeGuid));
                if (theme.Catalog == null) { theme.Catalog = catalog; EditorUtility.SetDirty(theme); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Theme catalog collected: " + slots.Count + " slots. Existing baseline values and IDs preserved.");
        }
    }

    [CustomEditor(typeof(DOTORIONTheme))]
    public sealed class DOTORIONThemeInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var theme = (DOTORIONTheme)target;
            if (GUILayout.Button("Theme Editor 열기 · 이미지 교체 / 개별 예외")) DOTORIONThemeEditor.Open(theme);
            DOTORIONThemeEditor.DrawPalette(theme);
        }
    }
}
