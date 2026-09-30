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

        [Tooltip("끝까지 올라오는 데 걸리는 시간(초).")]
        [SerializeField] private float _openSeconds = 0.14f;

        [Tooltip("끝까지 내려가는 데 걸리는 시간(초).")]
        [SerializeField] private float _closeSeconds = 0.15f;

        [Tooltip("올라올 때의 움직임. 가로는 시간(0~1), 세로는 진행도(0~1)입니다.")]
        [SerializeField] private AnimationCurve _openCurve =
            new AnimationCurve(new Keyframe(0f, 0f, 0f, 2.2f), new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("내려갈 때의 움직임. 가로는 시간(0~1), 세로는 진행도(0~1)입니다.")]
        [SerializeField] private AnimationCurve _closeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("마우스가 떠난 뒤 접히기까지 기다리는 시간(초). 가장자리를 스치기만 해도 닫히지 않게 합니다.")]
        [SerializeField] private float _closeDelaySeconds = 0.35f;

        private Vector2 _openPosition;
        private bool _captured;
        private bool _open;
        private float _closeAt = -1f;

        // Offsets below the open position, unrounded. Each slide runs from
        // wherever the tray is, so reversing halfway never jumps.
        private float _offset;
        private float _from;
        private float _to;
        private float _elapsed;
        private float _duration;
        private AnimationCurve _curve;

        // The app idles at 30 fps, which draws a 0.15 s slide in four frames.
        // The cap is lifted only while the tray is moving.
        private const int MinimumSlideFrameRate = 60;
        private int _idleFrameRate;
        private bool _frameRateRaised;

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
            _offset = _from = _to = -_hiddenDepth;
            _elapsed = _duration = 0f;
            ApplyPosition();
        }

        private void OnDisable()
        {
            RestoreFrameRate();
        }

        private void Update()
        {
            if (_closeAt >= 0f && Time.unscaledTime >= _closeAt)
            {
                _closeAt = -1f;
                SetOpen(false);
            }

            if (_offset == _to) return;

            // The first frame of a slide still carries the idle 30 fps gap, which
            // would spend a quarter of the slide in one jump.
            _elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / MinimumSlideFrameRate);
            if (_elapsed >= _duration)
            {
                _offset = _to;
                RestoreFrameRate();
            }
            else
            {
                var eased = (_curve ?? _closeCurve).Evaluate(_elapsed / _duration);
                _offset = Mathf.LerpUnclamped(_from, _to, eased);
            }

            ApplyPosition();
        }

        private void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;

            _from = _offset;
            _to = open ? 0f : -_hiddenDepth;
            _elapsed = 0f;
            _curve = open ? _openCurve : _closeCurve;
            // A slide cut short and reversed covers less ground, so it gets
            // proportionally less time instead of crawling back.
            var fullSeconds = open ? _openSeconds : _closeSeconds;
            _duration = fullSeconds * Mathf.Abs(_to - _from) / Mathf.Max(_hiddenDepth, 1f);
            RaiseFrameRate();
        }

        private void RaiseFrameRate()
        {
            if (_frameRateRaised) return;
            _frameRateRaised = true;
            _idleFrameRate = Application.targetFrameRate;
            var refreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = Mathf.Max(refreshRate, MinimumSlideFrameRate);
        }

        private void RestoreFrameRate()
        {
            if (!_frameRateRaised) return;
            _frameRateRaised = false;
            Application.targetFrameRate = _idleFrameRate;
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
            SetOpen(true);
        }

        private void ApplyPosition()
        {
            if (!_captured) return;
            // Whole pixels only, or the pixel art smears on every frame of the slide.
            _tray.anchoredPosition = _openPosition + new Vector2(0f, Mathf.Round(_offset));
        }
    }
}
