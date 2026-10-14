using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DOTORION.UI
{
    /// <summary>
    /// The grid that opens above the overlay when someone clicks their own
    /// profile tile. Picking applies immediately - there is nothing to undo about
    /// a picture, and a preview that needs confirming would make the common case
    /// two clicks - so 확인 only closes the panel.
    ///
    /// Every cell is a slot the person can make their own. The catalog icons
    /// come first, then a row of empty "+" cells; clicking "+" registers a photo
    /// there. 편집 turns on hover buttons on every cell: 교체 puts a photo into
    /// it (over the icon, if it had one) and 기본 takes it back to the icon or
    /// the "+". Which photos are in which slot is <see cref="AvatarSlotStore"/>'s
    /// business; this view only draws what it is told and reports clicks.
    ///
    /// The cells are cloned from a template at runtime instead of being fixed
    /// slots in the prefab, because the catalog is an asset the team adds to: a
    /// prefab with a hand-placed grid would cap how many icons can ever exist.
    /// </summary>
    public sealed class AvatarPickerPanelView : MonoBehaviour
    {
        [SerializeField] private RectTransform _grid;
        [SerializeField] private Button _optionTemplate;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Text _feedbackText;

        [Header("Selection tint")]
        [Tooltip("고른 아바타 칸에 곱해지는 색. 아트가 선택 상태를 직접 표현하면 흰색으로 두세요.")]
        [SerializeField] private Color _selectedTint = new Color(0.431f, 0.659f, 0.996f, 1f);

        [Tooltip("고르지 않은 칸에 곱해지는 색. 흰색이면 스프라이트가 그려진 그대로 나옵니다.")]
        [SerializeField] private Color _unselectedTint = Color.white;

        private sealed class Cell
        {
            public string SlotId;
            public Sprite DefaultSprite;
            public Button Button;
            public Image Icon;
            public Text Plus;
            public RectTransform Overlay;
            public Button ResetButton;
        }

        private readonly List<Cell> _cells = new List<Cell>();
        private TeamAvatarCatalog _catalog;
        private Func<string, Sprite> _slotPhoto = _ => null;
        private Func<string, string> _slotForAvatarKey = key => AvatarSlotStore.IconSlot(key);
        private string _selectedSlot;
        private bool _initialized;
        private bool _editing;
        private bool _busy;
        private Cell _hovered;
        private Button _editButton;
        private Text _editButtonLabel;
        private Text _confirmLabel;
        private Text _heading;
        private string _headingText;
        private AvatarCropEditor _cropEditor;
        private string _cropSlot;
        private bool _wearCroppedPhoto;

        /// <summary>Raised when a cell is clicked to wear what it shows.</summary>
        public event Action<string> SlotPicked;

        /// <summary>Raised by "+" or 교체: the app opens a file and hands it back to <see cref="BeginPhotoCrop"/>.</summary>
        public event Action<string> SlotPhotoRequested;

        /// <summary>Raised by 기본: the slot goes back to its icon or to "+".</summary>
        public event Action<string> SlotResetRequested;

        /// <summary>
        /// Raised by 적용 with the slot, the cropped PNG, and whether the person
        /// meant to wear it now ("+" outside 편집, or replacing the worn cell).
        /// </summary>
        public event Action<string, byte[], bool> SlotPhotoCropped;

        /// <summary>Raised by 확인; the panel itself never decides to close.</summary>
        public event Action CloseRequested;

        public bool IsCropping => _cropEditor != null && _cropEditor.gameObject.activeSelf;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (_optionTemplate != null)
            {
                _optionTemplate.gameObject.SetActive(false);
            }

            if (_confirmButton != null)
            {
                _confirmButton.onClick.AddListener(Confirm);
                _confirmLabel = _confirmButton.GetComponentInChildren<Text>(true);
                BuildHeaderControls();
            }
        }

        /// <summary>
        /// Fills the grid from the catalog. Re-running it with the same catalog
        /// costs nothing, so the app may call it on every open.
        /// </summary>
        public void SetCatalog(TeamAvatarCatalog catalog)
        {
            Initialize();
            if (_catalog == catalog && _cells.Count > 0)
            {
                return;
            }

            _catalog = catalog;
            BuildCells();
        }

        /// <summary>
        /// Where cells read their photos from, and how a worn avatar key maps back
        /// to the cell that holds it.
        /// </summary>
        public void SetSlotSource(Func<string, Sprite> slotPhoto, Func<string, string> slotForAvatarKey)
        {
            _slotPhoto = slotPhoto ?? (_ => null);
            _slotForAvatarKey = slotForAvatarKey ?? (key => AvatarSlotStore.IconSlot(key));
            RefreshCells();
        }

        /// <summary>Redraws every cell after a slot's photo changed.</summary>
        public void RefreshCells()
        {
            foreach (var cell in _cells)
            {
                var photo = _slotPhoto(cell.SlotId);
                var sprite = photo != null ? photo : cell.DefaultSprite;
                if (cell.Icon != null)
                {
                    cell.Icon.sprite = sprite;
                    cell.Icon.enabled = sprite != null;
                }

                if (cell.Plus != null)
                {
                    cell.Plus.gameObject.SetActive(sprite == null);
                }
            }

            UpdateOverlay();
        }

        /// <summary>Marks the cell holding the avatar the member is wearing right now.</summary>
        public void SetSelected(string avatarKey)
        {
            var key = string.IsNullOrWhiteSpace(avatarKey) ? TeamAvatarCatalog.DefaultKey : avatarKey.Trim();
            var slot = string.Equals(key, TeamAvatarCatalog.DefaultKey, StringComparison.Ordinal)
                ? null
                : _slotForAvatarKey(key);
            if (string.Equals(slot, _selectedSlot, StringComparison.Ordinal))
            {
                return;
            }

            _selectedSlot = slot;
            ApplySelection();
        }

        /// <summary>The cell currently marked as worn, or null.</summary>
        public string SelectedSlot => _selectedSlot;

        public void SetBusy(bool busy)
        {
            _busy = busy;
            foreach (var cell in _cells)
            {
                cell.Button.interactable = !busy;
            }

            if (_editButton != null)
            {
                _editButton.interactable = !busy;
            }

            UpdateOverlay();
        }

        /// <summary>
        /// Opens the crop editor on a picture the app loaded for
        /// <paramref name="slotId"/>. The header buttons turn into 취소 / 적용 for
        /// as long as it is open.
        /// </summary>
        public void BeginPhotoCrop(Texture2D picture, string slotId)
        {
            Initialize();
            if (_cropEditor == null || picture == null)
            {
                return;
            }

            _cropSlot = slotId;
            // Outside 편집 the only way here is "+", which means "use this". In
            // 편집 a replaced photo is worn only if its cell already was.
            _wearCroppedPhoto = !_editing || string.Equals(slotId, _selectedSlot, StringComparison.Ordinal);
            SetGridVisible(false);
            _cropEditor.Begin(picture);
            ApplyHeader();
        }

        /// <summary>Leaves the crop editor without saving anything.</summary>
        public void EndPhotoCrop()
        {
            if (!IsCropping)
            {
                return;
            }

            _cropEditor.End();
            _cropSlot = null;
            SetGridVisible(true);
            ApplyHeader();
        }

        private void OnDisable()
        {
            // Closing the picker mid-crop drops the picture, and it reopens out
            // of 편집 so a stray click cannot replace anything.
            EndPhotoCrop();
            _editing = false;
            ApplyHeader();
            UpdateOverlay();
        }

        /// <summary>
        /// Hover is polled rather than taken from enter/exit events: the 교체 and
        /// 기본 buttons are children of the cell, and moving onto them would
        /// otherwise count as leaving it and hide them under the pointer.
        /// </summary>
        private void Update()
        {
            if (!_editing || IsCropping)
            {
                return;
            }

            var mouse = Mouse.current;
            Cell hovered = null;
            if (mouse != null)
            {
                var position = mouse.position.ReadValue();
                var canvas = GetComponentInParent<Canvas>();
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.rootCanvas.worldCamera
                    : null;
                foreach (var cell in _cells)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(
                            (RectTransform)cell.Button.transform, position, camera))
                    {
                        hovered = cell;
                        break;
                    }
                }
            }

            if (hovered != _hovered)
            {
                _hovered = hovered;
                UpdateOverlay();
            }
        }

        private void BuildCells()
        {
            foreach (var cell in _cells)
            {
                if (cell.Button != null)
                {
                    Destroy(cell.Button.gameObject);
                }
            }

            _cells.Clear();
            _hovered = null;
            if (_grid == null || _optionTemplate == null)
            {
                return;
            }

            var entries = _catalog != null ? _catalog.Options : Array.Empty<AvatarOption>();
            foreach (var entry in entries)
            {
                AddCell(AvatarSlotStore.IconSlot(entry.Key), entry.Sprite);
            }

            for (var index = 0; index < AvatarSlotStore.CustomSlotCount; index++)
            {
                AddCell(AvatarSlotStore.CustomSlot(index), null);
            }

            ShowEmptyHint(entries.Count == 0);
            RefreshCells();
            ApplySelection();
        }

        private void AddCell(string slotId, Sprite defaultSprite)
        {
            var button = Instantiate(_optionTemplate, _grid);
            button.name = "Avatar_" + slotId;
            button.gameObject.SetActive(true);
            var cell = new Cell
            {
                SlotId = slotId,
                DefaultSprite = defaultSprite,
                Button = button,
                Icon = button.transform.Find("Icon")?.GetComponent<Image>()
            };

            if (_heading != null)
            {
                cell.Plus = Instantiate(_heading, button.transform);
                cell.Plus.name = "Plus";
                cell.Plus.text = "+";
                cell.Plus.fontSize = 28;
                cell.Plus.alignment = TextAnchor.MiddleCenter;
                cell.Plus.raycastTarget = false;
                UiFactory.Stretch(cell.Plus.rectTransform);
            }

            BuildOverlay(cell);
            button.onClick.AddListener(() => CellClicked(cell));
            _cells.Add(cell);
        }

        /// <summary>교체 over 기본, each the cell's width, shown on the hovered cell in 편집.</summary>
        private void BuildOverlay(Cell cell)
        {
            cell.Overlay = UiFactory.CreateRect("EditButtons", cell.Button.transform).GetComponent<RectTransform>();
            UiFactory.Stretch(cell.Overlay);
            var replace = CloneHeaderButton(cell.Overlay, "Replace", "교체", () => RequestPhoto(cell));
            Place((RectTransform)replace.transform, 2f);
            cell.ResetButton = CloneHeaderButton(cell.Overlay, "Reset", "기본", () => SlotResetRequested?.Invoke(cell.SlotId));
            Place((RectTransform)cell.ResetButton.transform, 26f);
            cell.Overlay.gameObject.SetActive(false);

            void Place(RectTransform rect, float top)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -top);
                rect.sizeDelta = new Vector2(-4f, 22f);
            }
        }

        private void CellClicked(Cell cell)
        {
            if (_editing || _busy)
            {
                return;
            }

            if (_slotPhoto(cell.SlotId) == null && cell.DefaultSprite == null)
            {
                RequestPhoto(cell);
                return;
            }

            // Repainting before the request lands keeps the click feeling instant;
            // a failed save is corrected by the next roster poll.
            _selectedSlot = cell.SlotId;
            ApplySelection();
            SlotPicked?.Invoke(cell.SlotId);
        }

        private void RequestPhoto(Cell cell)
        {
            if (!_busy)
            {
                SlotPhotoRequested?.Invoke(cell.SlotId);
            }
        }

        private void Confirm()
        {
            if (!IsCropping)
            {
                CloseRequested?.Invoke();
                return;
            }

            var slot = _cropSlot;
            var wear = _wearCroppedPhoto;
            var png = _cropEditor.Export();
            EndPhotoCrop();
            if (png != null && slot != null)
            {
                SlotPhotoCropped?.Invoke(slot, png, wear);
            }
        }

        private void EditButtonClicked()
        {
            if (IsCropping)
            {
                EndPhotoCrop();
                return;
            }

            _editing = !_editing;
            _hovered = null;
            ApplyHeader();
            UpdateOverlay();
        }

        /// <summary>
        /// 편집 is a clone of 확인 placed to its left, and the crop editor takes
        /// the grid's rect. Both borrow the prefab's own art, so every skin draws
        /// them without new theme slots.
        /// </summary>
        private void BuildHeaderControls()
        {
            _heading = transform.Find("Heading")?.GetComponent<Text>();
            _headingText = _heading != null ? _heading.text : string.Empty;

            _editButton = Instantiate(_confirmButton, _confirmButton.transform.parent);
            _editButton.name = "Edit";
            _editButton.onClick = new Button.ButtonClickedEvent();
            _editButton.onClick.AddListener(EditButtonClicked);
            var confirmRect = (RectTransform)_confirmButton.transform;
            var editRect = (RectTransform)_editButton.transform;
            editRect.anchoredPosition = confirmRect.anchoredPosition - new Vector2(confirmRect.sizeDelta.x + 6f, 0f);
            _editButtonLabel = _editButton.GetComponentInChildren<Text>(true);

            var area = _grid != null ? _grid.parent as RectTransform : null;
            if (area != null && _heading != null)
            {
                _cropEditor = AvatarCropEditor.Create((RectTransform)transform, area, _heading, _confirmButton);
            }

            ApplyHeader();
        }

        private Button CloneHeaderButton(Transform parent, string name, string label, Action onClick)
        {
            var button = Instantiate(_confirmButton, parent);
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

        private void UpdateOverlay()
        {
            foreach (var cell in _cells)
            {
                var show = _editing && !_busy && cell == _hovered;
                if (cell.Overlay != null && cell.Overlay.gameObject.activeSelf != show)
                {
                    cell.Overlay.gameObject.SetActive(show);
                }

                if (show && cell.ResetButton != null)
                {
                    // Nothing to put back on a cell that is already showing its default.
                    cell.ResetButton.gameObject.SetActive(_slotPhoto(cell.SlotId) != null);
                }
            }
        }

        private void ApplyHeader()
        {
            if (IsCropping)
            {
                SetLabels("사진 자르기", "취소", "적용");
            }
            else if (_editing)
            {
                SetLabels("편집: 칸에 마우스를 올리세요", "완료", "확인");
            }
            else
            {
                SetLabels(_headingText, "편집", "확인");
            }
        }

        private void ApplySelection()
        {
            foreach (var cell in _cells)
            {
                var isSelected = string.Equals(cell.SlotId, _selectedSlot, StringComparison.Ordinal);
                var background = cell.Button.GetComponent<Image>();
                if (background != null)
                {
                    // Both come from the Inspector rather than the palette: the
                    // tile is artwork now, and a colour written here multiplies
                    // over it on every repaint. White leaves the sprite alone.
                    background.color = isSelected ? _selectedTint : _unselectedTint;
                }
            }
        }

        private void SetGridVisible(bool visible)
        {
            var area = _grid != null ? _grid.parent : null;
            if (area != null)
            {
                area.gameObject.SetActive(visible);
            }

            if (_feedbackText != null && !visible)
            {
                _feedbackText.gameObject.SetActive(false);
            }
            else if (visible)
            {
                ShowEmptyHint(_catalog == null || _catalog.Count == 0);
            }
        }

        private void SetLabels(string heading, string edit, string confirm)
        {
            if (_heading != null)
            {
                _heading.text = heading;
                // The 편집 hint is longer than the box the title was laid out in.
                _heading.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            if (_editButtonLabel != null) _editButtonLabel.text = edit;
            if (_confirmLabel != null) _confirmLabel.text = confirm;
        }

        private void ShowEmptyHint(bool empty)
        {
            if (_feedbackText == null)
            {
                return;
            }

            _feedbackText.gameObject.SetActive(empty);
            _feedbackText.text = "고를 수 있는 아이콘이 없습니다. " +
                                 "Resources/DOTORION/TeamAvatarCatalog에 이미지를 넣어 주세요.";
        }
    }
}
