using System.Threading;
using System.Threading.Tasks;

namespace DOTORION.Backend.Mock
{
    /// <summary>
    /// Development-only controls kept out of ITeamBackend so production backends
    /// never need to expose fake mutations.
    /// </summary>
    public interface IMockTeamBackendControls
    {
        Task TriggerFakeTeammateCheckInAsync(CancellationToken cancellationToken);

        Task TriggerFakeTeammateCheckInAsync(string memberId, CancellationToken cancellationToken);
    }
}
