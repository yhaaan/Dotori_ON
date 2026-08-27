using System;
using TeamOverlay.Identity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamOverlay.UI
{
    /// <summary>The editable first-launch name modal prefab.</summary>
    public sealed class FirstRunNameView : MonoBehaviour
    {
        private const string DefaultHint = "이름은 바꾸기 어려우니 신중하게 적어주세요.";

        [Header("Optional typography override")]
        [SerializeField] private Font _fontOverride;
        [Header("Prefab references")]
        [SerializeField] private InputField _nameInput;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Text _confirmLabel;
        [SerializeField] private Text _feedbackText;

        private bool _isBusy;
        private bool _feedbackShowsBusy;
        private bool _initialized;
        private EventTrigger _submitTrigger;
        private EventTrigger.Entry _submitEntry;

        public event Action<string> Submitted;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            UiFactory.EnsureEventSystem();
            UiFactory.ApplyApplicationFont(transform, _fontOverride);
            _confirmButton.onClick.AddListener(TrySubmit);
            _submitTrigger = _nameInput.GetComponent<EventTrigger>();
            if (_submitTrigger == null) _submitTrigger = _nameInput.gameObject.AddComponent<EventTrigger>();
            _submitEntry = new EventTrigger.Entry { eventID = EventTriggerType.Submit };
            _submitEntry.callback.AddListener(_ => TrySubmit());
            _submitTrigger.triggers.Add(_submitEntry);
            ShowHint();
            gameObject.SetActive(false);
        }

        public void Show(string initialName = null)
        {
            gameObject.SetActive(true);
            SetBusy(false);
            _nameInput.text = initialName ?? string.Empty;
            ShowHint();
            FocusNameInput();
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            _nameInput.DeactivateInputField();
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _nameInput.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            gameObject.SetActive(false);
        }

        public void SetBusy(bool busy)
        {
            _isBusy = busy;
            _nameInput.interactable = !busy;
            _confirmButton.interactable = !busy;
            _confirmLabel.text = busy ? "저장 중…" : "확인";
            if (busy)
            {
                _feedbackShowsBusy = true;
                _feedbackText.text = "이름을 저장하고 있어요…";
                _feedbackText.color = TeamOverlayPalette.TextSecondary;
            }
            else if (_feedbackShowsBusy) ShowHint();
        }

        public void ShowError(string message)
        {
            _feedbackShowsBusy = false;
            _feedbackText.text = string.IsNullOrWhiteSpace(message)
                ? "이름을 저장하지 못했습니다. 다시 시도해주세요." : message;
            _feedbackText.color = TeamOverlayPalette.Danger;
            if (!_isBusy) FocusNameInput();
        }

        public void TrySubmit()
        {
            if (_isBusy || !gameObject.activeSelf) return;
            var validation = DisplayNamePolicy.Validate(_nameInput.text);
            if (!validation.IsValid)
            {
                ShowError(ValidationMessage(validation.Error));
                return;
            }
            _nameInput.text = validation.DisplayName;
            Submitted?.Invoke(validation.DisplayName);
        }

        private void OnDestroy()
        {
            if (!_initialized) return;
            _confirmButton.onClick.RemoveListener(TrySubmit);
            if (_submitTrigger != null && _submitEntry != null) _submitTrigger.triggers.Remove(_submitEntry);
        }

        private static string ValidationMessage(DisplayNameValidationError error)
        {
            switch (error)
            {
                case DisplayNameValidationError.Required: return "표시할 이름을 입력해주세요.";
                case DisplayNameValidationError.TooLong: return "이름은 16자 이하로 입력해주세요.";
                case DisplayNameValidationError.UnsupportedCharacter: return "이름에는 한글, 영문, 숫자, 공백, _, -만 사용할 수 있어요.";
                case DisplayNameValidationError.LetterOrNumberRequired: return "이름에는 한글, 영문 또는 숫자가 하나 이상 필요해요.";
                default: return "사용할 수 없는 이름입니다.";
            }
        }

        private void ShowHint()
        {
            _feedbackShowsBusy = false;
            _feedbackText.text = DefaultHint;
            _feedbackText.color = TeamOverlayPalette.TextSecondary;
        }

        private void FocusNameInput()
        {
            if (!gameObject.activeInHierarchy || _isBusy) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_nameInput.gameObject);
            _nameInput.Select();
            _nameInput.ActivateInputField();
            _nameInput.MoveTextEnd(false);
        }
    }
}
