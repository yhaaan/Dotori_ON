using System.Threading;
using System.Threading.Tasks;

namespace TeamOverlay.Core
{
    /// <summary>
    /// Where the daily check-in stands for the local member. A claim and a plain
    /// read return the same shape, because the caller wants the same three
    /// answers either way; only <see cref="AwardedPoints"/> tells them apart.
    /// </summary>
    public sealed class DailyCheckInState
    {
        public DailyCheckInState(
            bool claimedToday,
            int totalPoints,
            int awardedPoints,
            int streakDays)
        {
            ClaimedToday = claimedToday;
            TotalPoints = totalPoints < 0 ? 0 : totalPoints;
            AwardedPoints = awardedPoints < 0 ? 0 : awardedPoints;
            StreakDays = streakDays < 0 ? 0 : streakDays;
        }

        /// <summary>True once today has been claimed, however it was found out.</summary>
        public bool ClaimedToday { get; }

        public int TotalPoints { get; }

        /// <summary>
        /// Days in a row, ending today or yesterday. Yesterday counts so the
        /// number does not read as broken every morning before the button is
        /// pressed; a run that ended earlier than that is over and reads zero.
        /// </summary>
        public int StreakDays { get; }

        /// <summary>
        /// What this particular call earned. Zero when reading the status, and
        /// zero when the claim arrived after today was already taken.
        /// </summary>
        public int AwardedPoints { get; }
    }

    /// <summary>
    /// Showing up, in the "I am here today" sense, which is not the same thing as
    /// an attendance session. A backend with no history cannot fake it, so like
    /// statistics it is advertised rather than assumed.
    /// </summary>
    public interface ITeamCheckIn
    {
        Task<DailyCheckInState> GetDailyCheckInStateAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Claims today. Pressing twice is an ordinary thing to do, so a second
        /// call is not an error: it comes back with nothing awarded.
        /// </summary>
        Task<DailyCheckInState> ClaimDailyCheckInAsync(CancellationToken cancellationToken);
    }
}
