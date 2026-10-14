using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DOTORION.UI
{
    /// <summary>
    /// Lets the person choose which square of their picture becomes the profile
    /// photo. Two views share one state: on the left the whole picture with a
    /// frame over the part that is kept, on the right that part at the size it
    /// is cut at. Dragging either one moves the frame, the wheel or the − / +
    /// buttons zoom, and 회전 turns a sideways phone photo upright.
    ///
    /// It is built in code inside the avatar picker rather than placed in the
    /// prefab: it only ever borrows the picker's area while a picture is open,
    /// and cloning the picker's own text and button keeps every skin's art and
    /// colours without a second set of theme bindings to maintain.
    /// </summary>
    public sealed class AvatarCropEditor : MonoBehaviour
    {
        private const float BoxSize = 110f;
        private const float MaximumZoom = 8f;
        private const float WheelStep = 1.15f;
        private const float ButtonStep = 1.25f;

        private RawImage _whole;
        private RectTransform _frame;
        private RawImage _crop;
        private Texture2D _texture;
        private float _zoom = 1f;
        private Vector2 _center;

        /// <summary>
        /// Creates the editor over <paramref name="area"/>'s rect, hidden.
        /// <paramref name="textTemplate"/> and <paramref name="buttonTemplate"/>
        /// are cloned for the hint and the zoom/rotate buttons.
        /// </summary>
        public static AvatarCropEditor Create(RectTransform panel, RectTransform area, Text textTemplate, Button buttonTemplate)
        {
            var root = UiFactory.CreateRect("PhotoCrop", panel).GetComponent<RectTransform>();
            root.anchorMin = area.anchorMin;
            root.anchorMax = area.anchorMax;
            root.pivot = area.pivot;
            root.anchoredPosition = area.anchoredPosition;
            root.sizeDelta = area.sizeDelta;
            var editor = root.gameObject.AddComponent<AvatarCropEditor>();
            editor.Build(root, textTemplate, buttonTemplate);
            root.gameObject.SetActive(false);
            return editor;
        }

        /// <summary>Opens <paramref name="texture"/>, which the editor now owns and destroys.</summary>
        public void Begin(Texture2D texture)
        {
            ReplaceTexture(texture);
            gameObject.SetActive(true);
        }

        /// <summary>The chosen square as the PNG the server stores.</summary>
        public byte[] Export()
        {
            return _texture == null ? null : AvatarPhotoImage.CropToPng(_texture, CropUv());
        }

        /// <summary>Closes the editor and lets go of the picture.</summary>
        public void End()
        {
            gameObject.SetActive(false);
            ReplaceTexture(null);
        }

        private void OnDestroy()
        {
            ReplaceTexture(null);
        }

        private void Build(RectTransform root, Text textTemplate, Button buttonTemplate)
        {
            // Left: the whole picture, fitted, with the frame drawn over it.
            var wholeBox = UiFactory.CreateImage("WholeBox", root, new Color(0f, 0f, 0f, 0.25f));
            UiFactory.AnchorTop(wholeBox.rectTransform, 4f, 0f, BoxSize, BoxSize);
            _whole = CreateRawImage("Whole", wholeBox.transform);
            _whole.rectTransform.anchorMin = _whole.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _whole.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            AddDrag(wholeBox.gameObject, delta => MoveBy(delta / WholeScale()));

            _frame = UiFactory.CreateRect("Frame", _whole.transform).GetComponent<RectTransform>();
            _frame.anchorMin = _frame.anchorMax = Vector2.zero;
            _frame.pivot = Vector2.zero;
            var frameColor = DOTORIONPalette.Accent;
            CreateEdge(_frame, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), frameColor);
            CreateEdge(_frame, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), frameColor);
            CreateEdge(_frame, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), frameColor);
            CreateEdge(_frame, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), frameColor);

            // Middle: exactly what will be kept, at the box's size.
            _crop = CreateRawImage("Crop", root);
            UiFactory.AnchorTop(_crop.rectTransform, 4f + BoxSize + 12f, 0f, BoxSize, BoxSize);
            // Dragging the cut moves the picture under it, so the frame goes the other way.
            AddDrag(_crop.gameObject, delta => MoveBy(-delta * SideTexels() / BoxSize));

            // Right: how to use it, and the buttons the mouse wheel stands in for.
            var left = 4f + (BoxSize + 12f) * 2f;
            var hint = Instantiate(textTemplate, root);
            hint.name = "Hint";
            hint.text = "드래그로 위치를 옮기고\n휠이나 − / + 로 크기를 맞추세요.";
            hint.alignment = TextAnchor.UpperLeft;
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.AnchorTop(hint.rectTransform, left, 2f, 200f, 40f);

            var zoomOut = CloneButton(buttonTemplate, root, "ZoomOut", "−", () => ZoomBy(1f / ButtonStep));
            UiFactory.AnchorTop((RectTransform)zoomOut.transform, left, 52f, 34f, 22f);
            var zoomIn = CloneButton(buttonTemplate, root, "ZoomIn", "+", () => ZoomBy(ButtonStep));
            UiFactory.AnchorTop((RectTransform)zoomIn.transform, left + 40f, 52f, 34f, 22f);
            var rotate = CloneButton(buttonTemplate, root, "Rotate", "회전", Rotate);
            UiFactory.AnchorTop((RectTransform)rotate.transform, left + 80f, 52f, 63f, 22f);

            var wheel = root.gameObject.AddComponent<CropSurface>();
            wheel.Scrolled = amount => ZoomBy(Mathf.Pow(WheelStep, amount));
        }

        private void ReplaceTexture(Texture2D texture)
        {
            if (_texture != null && _texture != texture)
            {
                Destroy(_texture);
            }

            _texture = texture;
            _whole.texture = texture;
            _crop.texture = texture;
            _zoom = 1f;
            if (texture == null)
            {
                return;
            }

            _center = new Vector2(texture.width, texture.height) * 0.5f;
            var scale = WholeScale();
            _whole.rectTransform.sizeDelta = new Vector2(texture.width, texture.height) * scale;
            Refresh();
        }

        private void Rotate()
        {
            if (_texture != null)
            {
                ReplaceTexture(AvatarPhotoImage.RotateClockwise(_texture));
            }
        }

        private void ZoomBy(float factor)
        {
            if (_texture == null)
            {
                return;
            }

            _zoom = Mathf.Clamp(_zoom * factor, 1f, MaximumZoom);
            Refresh();
        }

        private void MoveBy(Vector2 texels)
        {
            if (_texture == null)
            {
                return;
            }

            _center += texels;
            Refresh();
        }

        /// <summary>Keeps the square inside the picture, then repaints both views.</summary>
        private void Refresh()
        {
            var half = SideTexels() * 0.5f;
            _center.x = Mathf.Clamp(_center.x, half, _texture.width - half);
            _center.y = Mathf.Clamp(_center.y, half, _texture.height - half);
            _crop.uvRect = CropUv();
            var scale = WholeScale();
            _frame.anchoredPosition = (_center - new Vector2(half, half)) * scale;
            _frame.sizeDelta = new Vector2(half, half) * 2f * scale;
        }

        /// <summary>The kept square's side in texels: the short side at zoom 1.</summary>
        private float SideTexels() => Mathf.Min(_texture.width, _texture.height) / _zoom;

        private float WholeScale() => BoxSize / Mathf.Max(_texture.width, _texture.height);

        private Rect CropUv()
        {
            var side = SideTexels();
            return new Rect(
                (_center.x - side * 0.5f) / _texture.width,
                (_center.y - side * 0.5f) / _texture.height,
                side / _texture.width,
                side / _texture.height);
        }

        private void AddDrag(GameObject target, Action<Vector2> moved)
        {
            var surface = target.AddComponent<CropSurface>();
            var canvas = GetComponentInParent<Canvas>();
            // Pointer deltas are screen pixels; the views are laid out in canvas
            // units, which differ whenever the UI scale setting is not 100%.
            surface.Dragged = delta => moved(delta / (canvas != null ? canvas.rootCanvas.scaleFactor : 1f));
        }

        private static RawImage CreateRawImage(string name, Transform parent)
        {
            var instance = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            instance.transform.SetParent(parent, false);
            return instance.GetComponent<RawImage>();
        }

        private static void CreateEdge(RectTransform frame, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var edge = UiFactory.CreateImage(name, frame, color);
            edge.raycastTarget = false;
            var rect = edge.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            // Two units across the edge, the full length along it.
            rect.sizeDelta = anchorMin.x == anchorMax.x ? new Vector2(2f, 0f) : new Vector2(0f, 2f);
        }

        private static Button CloneButton(Button template, Transform parent, string name, string label, Action onClick)
        {
            var button = Instantiate(template, parent);
            button.name = name;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => onClick());
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }

            return button;
        }

        /// <summary>Forwards pointer drags and wheel turns to the editor.</summary>
        private sealed class CropSurface : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
        {
            public Action<Vector2> Dragged;
            public Action<float> Scrolled;

            public void OnBeginDrag(PointerEventData eventData)
            {
            }

            public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData.delta);

            public void OnScroll(PointerEventData eventData)
            {
                if (Scrolled != null)
                {
                    Scrolled(eventData.scrollDelta.y > 0f ? 1f : eventData.scrollDelta.y < 0f ? -1f : 0f);
                }
                else
                {
                    // Let a wheel over the left or middle view reach the root's handler.
                    ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.scrollHandler);
                }
            }
        }
    }
}
