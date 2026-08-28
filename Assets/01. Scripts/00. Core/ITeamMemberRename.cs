using System.Threading;
using System.Threading.Tasks;

namespace TeamOverlay.Core
{
    /// <summary>
    /// Changing your own name without losing what is recorded under it. Only a
    /// backend that owns the identity can do this - the name is what the account
    /// is keyed by, so the rename has to move the account too - which is why it
    /// is advertised rather than assumed.
    /// </summary>
    public interface ITeamMemberRename
    {
        /// <summary>
        /// Renames the local member. Sessions, intervals and check-ins all stay
        /// with them. Throws if the name is taken or malformed; nothing is left
        /// half changed either way.
        /// </summary>
        Task RenameAsync(string newDisplayName, CancellationToken cancellationToken);
    }
}
