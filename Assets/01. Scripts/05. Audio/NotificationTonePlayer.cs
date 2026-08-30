using UnityEngine;

namespace DOTORION.Audio
{
    /// <summary>
    /// Plays the clip a <see cref="DOTORIONSounds"/> asset assigns to an event,
    /// and falls back to a chime generated at runtime when no asset or no clip is
    /// set. The fallback is what keeps the app audible on a fresh clone with no
    /// binary audio committed. One event results in one PlayOneShot call.
    /// </summary>
    public sealed class NotificationTonePlayer : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private AudioClip _notificationClip;
        private AudioSource _audioSource;
        private DOTORIONSounds _sounds;
        private bool _muted;

        public bool IsMuted => _muted;

        /// <summary>
        /// Silences every notification. It is a mute rather than a volume of
        /// zero so the sounds asset's own volume survives being turned back on.
        /// </summary>
        public void SetMuted(bool muted)
        {
            _muted = muted;
        }

        /// <summary>Assigning null keeps the generated chime for every event.</summary>
        public void UseSounds(DOTORIONSounds sounds)
        {
            _sounds = sounds;
            if (_audioSource != null && sounds != null)
            {
                _audioSource.volume = sounds.Volume;
            }
        }

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = 0.35f;
            _notificationClip = CreateNotificationClip();
        }

        public void Play(TeamSound sound)
        {
            if (_audioSource == null || _muted)
            {
                return;
            }

            var clip = _sounds != null ? _sounds.Clip(sound) : null;
            if (clip == null)
            {
                if (!DOTORIONSounds.FallsBackToChime(sound))
                {
                    return;
                }

                clip = _notificationClip;
            }

            if (clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
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
