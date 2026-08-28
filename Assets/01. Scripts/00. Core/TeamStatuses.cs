namespace DOTORION.Core
{
    /// <summary>
    /// Whether a member currently has an open attendance session.
    /// </summary>
    public enum AttendanceStatus
    {
        ClockedOut = 0,
        ClockedIn = 1
    }

    /// <summary>
    /// Activity within an open attendance session. Clocked-out members have no
    /// current activity rather than using an artificial "offline" activity.
    /// </summary>
    public enum ActivityStatus
    {
        Working = 0,
        Break = 1,
        Meal = 2
    }

    /// <summary>
    /// Transport health is intentionally independent from attendance.
    /// </summary>
    public enum ConnectionStatus
    {
        Disconnected = 0,
        Degraded = 1,
        Connected = 2
    }

    public enum CheckoutReason
    {
        Manual = 0,
        AppExit = 1,
        OsShutdown = 2,
        AutoTimeout = 3,
        Admin = 4
    }

    public enum TeamEventType
    {
        MemberCheckedIn = 0,
        MemberActivityChanged = 1,
        MemberCheckedOut = 2,

        /// <summary>Someone poked a teammate, or the whole team.</summary>
        MemberNudged = 3
    }
}
