using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.UI
{
    /// <summary>
    /// The icon grid that opens above the overlay when someone clicks their own
    /// profile tile. Picking applies immediately - there is nothing to undo about
    /// a picture, and a preview that needs confirming would make the common case
    /// two clicks - so 확인 only closes the panel.
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

        private readonly List<Button> _options = new List<Button>();
        private readonly List<string> _optionKeys = new List<string>();
        private TeamAvatarCatalog _catalog;
        private string _selectedKey;
        private bool _initialized;
        private Button _photoButton;
        private Text _photoButtonLabel;
        private Text _confirmLabel;
        private Text _heading;
        private string _headingText;
        private AvatarCropEditor _cropEditor;

        /// <summary>Raised with the catalog key the person clicked.</summary>
        public event Action<string> AvatarPicked;

        /// <summary>Raised by 확인; the panel itself never decides to close.</summary>
        public event Action CloseRequested;

        /// <summary>Raised by 내 사진. The app owns the file dialog and hands the picture back.</summary>
        public event Action PhotoRequested;

        /// <summary>Raised by 적용 with the cropped square, already encoded as the stored PNG.</summary>
        public event Action<byte[]> PhotoCropped;

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
                BuildPhotoControls();
            }
        }

        /// <summary>
        /// Opens the crop editor on a picture the app loaded. The panel's own
        /// buttons turn into 취소 / 적용 for as long as it is open.
        /// </summary>
        public void BeginPhotoCrop(Texture2D picture)
        {
            Initialize();
            if (_cropEditor == null || picture == null)
            {
                return;
            }

            SetGridVisible(false);
            _cropEditor.Begin(picture);
            SetLabels("사진 자르기", "취소", "적용");
        }

        /// <summary>Leaves the crop editor without saving anything.</summary>
        public void EndPhotoCrop()
        {
            if (!IsCropping)
            {
                return;
            }

            _cropEditor.End();
            SetGridVisible(true);
            SetLabels(_headingText, "내 사진", "확인");
        }

        /// <summary>
        /// Fills the grid from the catalog. Re-running it with the same catalog
        /// costs nothing, so the app may call it on every open.
        /// </summary>
        public void SetCatalog(TeamAvatarCatalog catalog)
        {
            Initialize();
            if (_catalog == catalog && _options.Count > 0)
            {
                return;
            }

            _catalog = catalog;
            BuildOptions();
        }

        /// <summary>Marks which icon the member is wearing right now.</summary>
        public void SetSelected(string avatarKey)
        {
            _selectedKey = string.IsNullOrWhiteSpace(avatarKey)
                ? TeamAvatarCatalog.DefaultKey
                : avatarKey.Trim();
            ApplySelection();
        }

        public void SetBusy(bool busy)
        {
            foreach (var option in _options)
            {
                option.interactable = !busy;
            }

            if (_photoButton != null)
            {
                _photoButton.interactable = !busy;
            }
        }

        private void OnDisable()
        {
            // Closing the picker mid-crop drops the picture; reopening starts from the grid.
            EndPhotoCrop();
        }

        private void Confirm()
        {
            if (!IsCropping)
            {
                CloseRequested?.Invoke();
                return;
            }

            var png = _cropEditor.Export();
            EndPhotoCrop();
            if (png != null)
            {
                PhotoCropped?.Invoke(png);
            }
        }

        private void PhotoButtonClicked()
        {
            if (IsCropping)
            {
                EndPhotoCrop();
            }
            else
            {
                PhotoRequested?.Invoke();
            }
        }

        /// <summary>
        /// 내 사진 is a clone of 확인 placed to its left, and the crop editor takes
        /// the grid's rect. Both borrow the prefab's own art, so every skin draws
        /// them without new theme slots.
        /// </summary>
        private void BuildPhotoControls()
        {
            _heading = transform.Find("Heading")?.GetComponent<Text>();
            _headingText = _heading != null ? _heading.text : string.Empty;

            _photoButton = Instantiate(_confirmButton, _confirmButton.transform.parent);
            _photoButton.name = "Photo";
            _photoButton.onClick = new Button.ButtonClickedEvent();
            _photoButton.onClick.AddListener(PhotoButtonClicked);
            var confirmRect = (RectTransform)_confirmButton.transform;
            var photoRect = (RectTransform)_photoButton.transform;
            photoRect.anchoredPosition = confirmRect.anchoredPosition - new Vector2(confirmRect.sizeDelta.x + 6f, 0f);
            _photoButtonLabel = _photoButton.GetComponentInChildren<Text>(true);
            if (_photoButtonLabel != null)
            {
                _photoButtonLabel.text = "내 사진";
            }

            var area = _grid != null ? _grid.parent as RectTransform : null;
            if (area != null && _heading != null)
            {
                _cropEditor = AvatarCropEditor.Create((RectTransform)transform, area, _heading, _confirmButton);
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
                ShowEmptyHint(_options.Count == 0);
            }
        }

        private void SetLabels(string heading, string photo, string confirm)
        {
            if (_heading != null) _heading.text = heading;
            if (_photoButtonLabel != null) _photoButtonLabel.text = photo;
            if (_confirmLabel != null) _confirmLabel.text = confirm;
        }

        private void BuildOptions()
        {
            foreach (var option in _options)
            {
                if (option != null)
                {
                    Destroy(option.gameObject);
                }
            }

            _options.Clear();
            _optionKeys.Clear();

            if (_grid == null || _optionTemplate == null)
            {
                return;
            }

            var entries = _catalog != null ? _catalog.Options : Array.Empty<AvatarOption>();
            foreach (var entry in entries)
            {
                var instance = Instantiate(_optionTemplate, _grid);
                instance.name = "Avatar_" + entry.Key;
                instance.gameObject.SetActive(true);
                var icon = instance.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null)
                {
                    icon.sprite = entry.Sprite;
                    icon.enabled = true;
                }

                var key = entry.Key;
                instance.onClick.AddListener(() => Pick(key));
                _options.Add(instance);
                _optionKeys.Add(key);
            }

            ShowEmptyHint(entries.Count == 0);
            ApplySelection();
        }

        private void Pick(string key)
        {
            // Repainting before the request lands keeps the click feeling instant;
            // a failed save is corrected by the next roster poll.
            SetSelected(key);
            AvatarPicked?.Invoke(key);
        }

        private void ApplySelection()
        {
            for (var index = 0; index < _options.Count; index++)
            {
                var isSelected = string.Equals(_optionKeys[index], _selectedKey, StringComparison.Ordinal);
                var background = _options[index].GetComponent<Image>();
                if (background != null)
                {
                    // Both come from the Inspector rather than the palette: the
                    // tile is artwork now, and a colour written here multiplies
                    // over it on every repaint. White leaves the sprite alone.
                    background.color = isSelected ? _selectedTint : _unselectedTint;
                }
            }
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
