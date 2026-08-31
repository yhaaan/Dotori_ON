using System;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.UI
{
    /// <summary>
    /// The "there is a newer version" modal prefab.
    ///
    /// It is the only thing in the app that asks a question the person cannot
    /// undo - saying yes closes the app - so the two answers are kept plainly
    /// apart, and once the download starts both buttons go away rather than
    /// leaving a cancel that would strand a half-downloaded file.
    /// </summary>
    public sealed class UpdatePromptView : MonoBehaviour
    {
        [Header("Prefab references")]
        [SerializeField] private Text _messageText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _laterButton;

        [Tooltip("오류일 때만 쓰는 글자색. 나머지 상태는 프리팹에 칠해진 색을 그대로 씁니다.")]
        [SerializeField] private Color _errorColor = new Color(0.914f, 0.443f, 0.443f, 1f);

        private bool _initialized;

        /// <summary>
        /// Whatever colour the status line was painted in the prefab. Read once
        /// and put back afterwards, so the artwork stays the source of truth and
        /// only the error state is allowed to depart from it.
        /// </summary>
        private Color _statusColor = Color.white;

        /// <summary>The person said yes. The app downloads and then quits.</summary>
        public event Action Confirmed;

        /// <summary>Not now. The check runs again the next time the app starts.</summary>
        public event Action Dismissed;

        public bool IsVisible => gameObject.activeSelf;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (_statusText != null)
            {
                _statusColor = _statusText.color;
            }

            UiFactory.EnsureEventSystem();
            _confirmButton?.onClick.AddListener(() => Confirmed?.Invoke());
            _laterButton?.onClick.AddListener(() => Dismissed?.Invoke());
            gameObject.SetActive(false);
        }

        public void Show(string version)
        {
            if (_messageText != null)
            {
                _messageText.text = "새로운 버전 " + version + "이 나왔습니다.\n업데이트 할까요?";
            }

            SetStatus(string.Empty);
            SetButtonsVisible(true);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Replaces the buttons with a running count. The app is about to close
        /// itself, so the one thing the person needs is to see that something is
        /// happening and roughly how long it has left.
        /// </summary>
        public void ShowProgress(float fraction)
        {
            SetButtonsVisible(false);
            var percent = Mathf.Clamp01(fraction) * 100f;
            SetStatus("내려받는 중… " + Mathf.FloorToInt(percent) + "%");
        }

        /// <summary>Shown just before the app closes, so the wait is not silent.</summary>
        public void ShowApplying()
        {
            SetButtonsVisible(false);
            SetStatus("설치하고 다시 시작합니다…");
        }

        /// <summary>
        /// Something went wrong and nothing was changed. The buttons come back so
        /// the person can try again or carry on working.
        /// </summary>
        public void ShowError(string message)
        {
            SetButtonsVisible(true);
            SetStatus(message, _errorColor);
        }

        private void SetButtonsVisible(bool visible)
        {
            if (_confirmButton != null) _confirmButton.gameObject.SetActive(visible);
            if (_laterButton != null) _laterButton.gameObject.SetActive(visible);
        }

        /// <summary>The ordinary states leave the prefab's colour alone.</summary>
        private void SetStatus(string message)
        {
            SetStatus(message, _statusColor);
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusText == null)
            {
                return;
            }

            _statusText.text = message ?? string.Empty;
            _statusText.color = color;
        }
    }
}
