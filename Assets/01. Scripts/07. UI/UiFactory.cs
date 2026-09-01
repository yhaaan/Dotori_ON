using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DOTORION.UI
{
    /// <summary>Editor-prefab construction helpers and small runtime UI services.</summary>
    public static class UiFactory
    {
        public static GameObject CreateRect(string name, Transform parent)
        {
            var instance = new GameObject(name, typeof(RectTransform));
            instance.transform.SetParent(parent, false);
            return instance;
        }

        public static Image CreateImage(string name, Transform parent)
        {
            var instance = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            instance.transform.SetParent(parent, false);
            return instance.GetComponent<Image>();
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var image = CreateImage(name, parent);
            image.color = color;
            return image;
        }

        public static Text CreateText(string name, Transform parent, Font font, int fontSize,
            TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            var instance = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            instance.transform.SetParent(parent, false);
            var text = instance.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, Font font, string label,
            Action onClick = null, Color? background = null)
        {
            var image = CreateImage(name, parent, background ?? DOTORIONPalette.Button);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = DOTORIONPalette.ButtonHover;
            colors.pressedColor = new Color(0.72f, 0.78f, 0.88f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.48f, 0.52f, 0.58f, 0.55f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var text = CreateText("Label", button.transform, font, 12, TextAnchor.MiddleCenter,
                DOTORIONPalette.TextPrimary, FontStyle.Bold);
            text.text = label;
            Stretch(text.rectTransform);
            return button;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            var inputSystemType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem", false);
            if (inputSystemType != null) eventSystem.AddComponent(inputSystemType);
            else eventSystem.AddComponent<StandaloneInputModule>();
        }

        public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f,
            float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void AnchorTop(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void AnchorRight(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
