using System;
using DOTORION.Identity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DOTORION.UI
{
    /// <summary>The editable first-launch name modal prefab.</summary>
    public sealed class FirstRunNameView : MonoBehaviour
    {
        private const string DefaultMessage = "팀에서 사용할 이름을 알려주세요.";

        // The first-run warning stopped being true once renaming kept the
        // record. Saying it during a rename would talk someone out of a
        // thing that now costs nothing.
        private const string RenameMessage = "기록은 그대로 따라옵니다.";

        private const string BusyMessage = "이름을 저장하고 있어요…";

        [Header("Prefab references")]
        [SerializeField] private InputField _nameInput;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        /// <summary>
        /// The one line the modal speaks with. It carries the invitation, then
        /// whatever went wrong with what was typed - there is no second line
        /// underneath any more, so a hint and an error cannot contradict.
        /// </summary>
        [FormerlySerializedAs("_feedbackText")]
        [SerializeField] private Text _messageText;

        private bool _isBusy;
        private bool _isRename;
        private bool _messageShowsBusy;
        private bool _initialized;
        private EventTrigger _submitTrigger;
        private EventTrigger.Entry _submitEntry;

        public event Action<string> Submitted;

        /// <summary>Only ever raised for a rename; the first run has no way out.</summary>
        public event Action Cancelled;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            UiFactory.EnsureEventSystem();
            _confirmButton.onClick.AddListener(TrySubmit);
            if (_cancelButton != null)
            {
                _cancelButton.onClick.AddListener(() => Cancelled?.Invoke());
            }
            _submitTrigger = _nameInput.GetComponent<EventTrigger>();
            if (_submitTrigger == null) _submitTrigger = _nameInput.gameObject.AddComponent<EventTrigger>();
            _submitEntry = new EventTrigger.Entry { eventID = EventTriggerType.Submit };
            _submitEntry.callback.AddListener(_ => TrySubmit());
            _submitTrigger.triggers.Add(_submitEntry);
            ShowDefaultMessage();
            gameObject.SetActive(false);
        }

        public void Show(string initialName = null)
        {
            _isRename = false;
            ShowInternal(initialName);
        }

        /// <summary>
        /// The same modal, but backed out of. The first run cannot be cancelled -
        /// there is no overlay behind it to return to - while a rename can, and a
        /// rename that trapped the person until they typed something would be a
        /// worse thing than the button that opened it.
        /// </summary>
        public void ShowForRename(string currentName)
        {
            _isRename = true;
            ShowInternal(currentName);
        }

        private void ShowInternal(string initialName)
        {
            gameObject.SetActive(true);
            SetBusy(false);
            _nameInput.text = initialName ?? string.Empty;
            if (_cancelButton != null)
            {
                _cancelButton.gameObject.SetActive(_isRename);
            }

            ShowDefaultMessage();
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
            if (busy)
            {
                _messageShowsBusy = true;
                SetMessage(BusyMessage);
            }
            else if (_messageShowsBusy) ShowDefaultMessage();
        }

        public void ShowError(string message)
        {
            _messageShowsBusy = false;
            SetMessage(string.IsNullOrWhiteSpace(message)
                ? "이름을 저장하지 못했습니다. 다시 시도해주세요." : message);
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
                case DisplayNameValidationError.Required: return "팀에서 사용할 이름을 알려주세요.";
                case DisplayNameValidationError.TooLong: return "이름은 16자 이하로 입력해주세요.";
                case DisplayNameValidationError.UnsupportedCharacter: return "이름에는 한글, 영문, 숫자, 공백, _, -만 사용할 수 있어요.";
                case DisplayNameValidationError.LetterOrNumberRequired: return "이름에는 한글, 영문 또는 숫자가 하나 이상 필요해요.";
                default: return "사용할 수 없는 이름입니다.";
            }
        }

        private void ShowDefaultMessage()
        {
            _messageShowsBusy = false;
            SetMessage(_isRename ? RenameMessage : DefaultMessage);
        }

        private void SetMessage(string message)
        {
            if (_messageText != null) _messageText.text = message;
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
