using System.Threading;
using System.Threading.Tasks;

namespace DOTORION.Core
{
    /// <summary>
    /// Nudging is a separate capability from attendance: it carries no state, so
    /// a backend that only reports a roster cannot fake it the way it can fake a
    /// check-in. Backends that support it advertise it by implementing this.
    /// </summary>
    public interface ITeamNudges
    {
        /// <summary>
        /// Pokes one teammate, or the whole team when <paramref name="targetMemberId"/>
        /// is null.
        /// </summary>
        Task SendNudgeAsync(string targetMemberId, CancellationToken cancellationToken);
    }
}
