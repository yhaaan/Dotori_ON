namespace DOTORION.Core
{
    /// <summary>What the desk going quiet, or waking up, should do to the status.</summary>
    public enum IdleActivityAction
    {
        None = 0,
        StartBreak = 1,
        ResumeWork = 2
    }

    /// <summary>
    /// Decides when a quiet desk should say so. Kept apart from the app so the
    /// rules can be read and tested without a backend, a window or a clock.
    ///
    /// The one rule worth stating out loud: only a break this policy started is
    /// ever taken back. 식사중, or a break someone chose for themselves, is a
    /// statement about where they are, and a keyboard press is not a reason to
    /// overrule it.
    /// </summary>
    public sealed class IdleActivityPolicy
    {
        private readonly double _breakAfterSeconds;
        private bool _engaged;

        public IdleActivityPolicy(double breakAfterSeconds)
        {
            _breakAfterSeconds = breakAfterSeconds;
        }

        /// <summary>True while the current break is one this policy started.</summary>
        public bool IsEngaged => _engaged;

        /// <summary>
        /// Forgets any break in progress. Called when the session ends, so the
        /// next one cannot be resumed out of a break it never entered.
        /// </summary>
        public void Reset()
        {
            _engaged = false;
        }

        public IdleActivityAction Evaluate(double idleSeconds, ActivityStatus activity)
        {
            if (idleSeconds >= _breakAfterSeconds)
            {
                if (_engaged || activity != ActivityStatus.Working)
                {
                    return IdleActivityAction.None;
                }

                // Engaged even if the change fails to reach the server. A retry
                // every few seconds would spend the whole absence reporting the
                // same error, and staying 작업중 while away is only what the
                // overlay did before this existed. The next absence tries again.
                _engaged = true;
                return IdleActivityAction.StartBreak;
            }

            if (!_engaged)
            {
                return IdleActivityAction.None;
            }

            _engaged = false;
            return activity == ActivityStatus.Break
                ? IdleActivityAction.ResumeWork
                : IdleActivityAction.None;
        }
    }
}
