using UnityEngine;
using UnityEngine.EventSystems;

namespace DOTORION.UI
{
    /// <summary>
    /// The arch that rises from the bottom edge carrying the status buttons,
    /// with clock-out in its hollow. Everything on it is placed by hand in the
    /// prefab; this only slides that piece between where the prefab left it
    /// and a fixed depth below, and decides when.
    ///
    /// Folded, the tray sits under the RectMask2D on this object so that only
    /// the handle on top of the arch pokes out of the bottom edge. The mask
    /// also keeps the hidden arch from drawing over, or catching clicks from,
    /// the panels that open below the compact window.
    /// </summary>
    public sealed class ActivityDockView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Tooltip("올라오고 내려가는 묶음입니다. 프리팹에 놓인 위치가 펼쳐진 모습입니다.")]
        [SerializeField] private RectTransform _tray;

        [Tooltip("접혔을 때 펼친 위치에서 아래로 내려가는 거리(px). 손잡이만 창 아래 끝에 걸리도록 맞춥니다.")]
        [SerializeField] private float _hiddenDepth = 59f;

        [Tooltip("끝까지 올라오거나 내려가는 데 걸리는 시간(초).")]
        [SerializeField] private float _slideSeconds = 0.18f;

        [Tooltip("마우스가 떠난 뒤 접히기까지 기다리는 시간(초). 가장자리를 스치기만 해도 닫히지 않게 합니다.")]
        [SerializeField] private float _closeDelaySeconds = 0.35f;

        private Vector2 _openPosition;
        private bool _captured;
        private bool _open;
        private float _progress;
        private float _closeAt = -1f;

        public bool IsOpen => _open;

        private void Awake()
        {
            // The prefab is saved open so the arch can be arranged where it is
            // seen; that position is the open one, whatever it was moved to.
            if (_tray == null) return;
            _openPosition = _tray.anchoredPosition;
            _captured = true;
        }

        private void OnEnable()
        {
            // Hidden means clocked out or mini mode, and neither should come
            // back to an arch that was left open or halfway up.
            _open = false;
            _closeAt = -1f;
            _progress = 0f;
            ApplyPosition();
        }

        private void Update()
        {
            if (_closeAt >= 0f && Time.unscaledTime >= _closeAt)
            {
                _closeAt = -1f;
                _open = false;
            }

            var target = _open ? 1f : 0f;
            if (Mathf.Approximately(_progress, target)) return;

            _progress = _slideSeconds > 0f
                ? Mathf.MoveTowards(_progress, target, Time.unscaledDeltaTime / _slideSeconds)
                : target;
            ApplyPosition();
        }

        // The EventSystem keeps a parent entered while the pointer moves between
        // its children, so this only fires on leaving the handle, the arch and
        // its buttons altogether.
        public void OnPointerEnter(PointerEventData eventData) => Open();

        public void OnPointerExit(PointerEventData eventData)
        {
            _closeAt = Time.unscaledTime + _closeDelaySeconds;
        }

        // Hover needs the window to be receiving mouse input, which an overlay
        // behind another app may not; a click on the handle always opens it.
        public void OnPointerClick(PointerEventData eventData) => Open();

        private void Open()
        {
            _closeAt = -1f;
            _open = true;
        }

        private void ApplyPosition()
        {
            if (!_captured) return;
            var eased = 1f - Mathf.Pow(1f - _progress, 3f);
            // Whole pixels only, or the pixel art smears on every frame of the slide.
            var offset = Mathf.Round(Mathf.Lerp(-_hiddenDepth, 0f, eased));
            _tray.anchoredPosition = _openPosition + new Vector2(0f, offset);
        }
    }
}
