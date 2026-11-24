namespace DOTORION.Core
{
    /// <summary>
    /// Keeps invalidations until a read actually starts. A slow read or mutation
    /// must not consume a signal merely because the app tried to refresh.
    /// </summary>
    public sealed class TeamChangeRefreshScheduler
    {
        public const float BatchSeconds = 0.3f;
        private int _joinGeneration;
        private bool _pending;
        private float _dueAt;

        public bool Observe(ITeamChangeSignals signals, float now)
        {
            if (signals == null)
            {
                return false;
            }

            var generation = signals.JoinGeneration;
            var joined = generation != _joinGeneration;
            _joinGeneration = generation;
            if (joined)
            {
                RequestRetry(now);
            }

            while (signals.TryDequeue(out _))
            {
                if (!_pending)
                {
                    _pending = true;
                    // A fixed window cannot be postponed forever by a busy team.
                    _dueAt = now + BatchSeconds;
                }
            }

            return joined;
        }

        public bool IsDue(float now) => _pending && now >= _dueAt;

        public void MarkRefreshStarted()
        {
            _pending = false;
        }

        public void RequestRetry(float now)
        {
            _pending = true;
            _dueAt = now;
        }

        public void Reset()
        {
            _joinGeneration = 0;
            _pending = false;
            _dueAt = 0f;
        }
    }
}
