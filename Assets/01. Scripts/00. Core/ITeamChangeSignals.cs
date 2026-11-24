namespace DOTORION.Core
{
    public enum TeamChangeKind
    {
        State,
        Member,
        Nudge
    }

    /// <summary>
    /// Invalidates snapshots without exposing the transport or trusting row data
    /// received outside the backend's authorized read path.
    /// </summary>
    public interface ITeamChangeSignals
    {
        /// <summary>True only after the server has accepted the subscription.</summary>
        bool IsLive { get; }

        /// <summary>Drained by the main thread before deciding whether to refresh.</summary>
        bool TryDequeue(out TeamChangeKind kind);

        /// <summary>Each successful join requires a read to cover missed changes.</summary>
        int JoinGeneration { get; }
    }
}
