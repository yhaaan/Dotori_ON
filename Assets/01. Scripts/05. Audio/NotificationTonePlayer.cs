using UnityEngine;

namespace TeamOverlay.Audio
{
    /// <summary>
    /// Creates a tiny notification chime at runtime so the mock vertical slice has
    /// no binary audio-asset dependency. One event results in one PlayOneShot call.
    /// </summary>
    public sealed class NotificationTonePlayer : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private AudioClip _notificationClip;
        private AudioSource _audioSource;

        public int PlayCount { get; private set; }

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = 0.35f;
            _notificationClip = CreateNotificationClip();
        }

        public void Play()
        {
            if (_notificationClip == null || _audioSource == null)
            {
                return;
            }

            PlayCount++;
            _audioSource.PlayOneShot(_notificationClip);
        }

        private void OnDestroy()
        {
            if (_notificationClip != null)
            {
                Destroy(_notificationClip);
            }
        }

        private static AudioClip CreateNotificationClip()
        {
            const float durationSeconds = 0.32f;
            var sampleCount = Mathf.CeilToInt(SampleRate * durationSeconds);
            var samples = new float[sampleCount];

            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (float)SampleRate;
                var progress = time / durationSeconds;
                var attack = Mathf.Clamp01(time / 0.025f);
                var release = Mathf.Pow(1f - progress, 1.7f);
                var envelope = attack * release;
                var first = Mathf.Sin(2f * Mathf.PI * 659.25f * time);
                var second = Mathf.Sin(2f * Mathf.PI * 987.77f * time);
                samples[index] = (first * 0.7f + second * 0.3f) * envelope * 0.35f;
            }

            var clip = AudioClip.Create("Mock teammate check-in", sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
