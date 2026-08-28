using UnityEngine;

namespace DOTORION.Audio
{
    /// <summary>Every sound the overlay can play, in one place.</summary>
    public enum TeamSound
    {
        TeammateCheckedIn = 0,
        TeammateCheckedOut = 1,
        NudgeReceived = 2
    }

    /// <summary>
    /// The one asset that decides what the overlay sounds like. Drop an
    /// <see cref="AudioClip"/> into a slot and that event uses it; leave a slot
    /// empty and the event falls back to the generated chime, or stays silent
    /// where there is nothing to fall back to.
    ///
    /// Sounds live in an asset rather than in code so changing them never needs a
    /// rebuild of anything but the player.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DOTORIONSounds",
        menuName = "DOTORI ON/Sounds",
        order = 0)]
    public sealed class DOTORIONSounds : ScriptableObject
    {
        [Header("효과음 (비우면 기본 알림음)")]
        [Tooltip("팀원이 출근했을 때. 비우면 코드로 만든 기본 알림음이 납니다.")]
        [SerializeField] private AudioClip _teammateCheckedIn;

        [Tooltip("팀원이 퇴근했을 때. 비우면 소리가 나지 않습니다.")]
        [SerializeField] private AudioClip _teammateCheckedOut;

        [Tooltip("누가 나를 호출했을 때. 비우면 코드로 만든 기본 알림음이 납니다.")]
        [SerializeField] private AudioClip _nudgeReceived;

        [Header("공통")]
        [Range(0f, 1f)]
        [Tooltip("모든 효과음에 함께 적용되는 음량입니다.")]
        [SerializeField] private float _volume = 0.35f;

        public float Volume => _volume;

        public AudioClip Clip(TeamSound sound)
        {
            switch (sound)
            {
                case TeamSound.TeammateCheckedOut: return _teammateCheckedOut;
                case TeamSound.NudgeReceived: return _nudgeReceived;
                default: return _teammateCheckedIn;
            }
        }

        /// <summary>
        /// Whether an unassigned slot should fall back to the generated chime. A
        /// check-out has never made a sound, so silence there is the intended
        /// default rather than a missing file.
        /// </summary>
        public static bool FallsBackToChime(TeamSound sound)
        {
            return sound != TeamSound.TeammateCheckedOut;
        }
    }
}
