using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeamOverlay.Supabase
{
    public sealed class SupabaseAuthSession
    {
        public SupabaseAuthSession(
            Guid userId,
            string accessToken,
            string refreshToken,
            DateTimeOffset expiresAtUtc)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("A Supabase user id is required.", nameof(userId));
            }

            UserId = userId;
            AccessToken = !string.IsNullOrWhiteSpace(accessToken)
                ? accessToken
                : throw new ArgumentException("An access token is required.", nameof(accessToken));
            RefreshToken = !string.IsNullOrWhiteSpace(refreshToken)
                ? refreshToken
                : throw new ArgumentException("A refresh token is required.", nameof(refreshToken));
            ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
        }

        public Guid UserId { get; }

        public string AccessToken { get; }

        public string RefreshToken { get; }

        public DateTimeOffset ExpiresAtUtc { get; }
    }

    /// <summary>
    /// Hands out an access token that is still valid, refreshing it first when it
    /// is about to expire. One implementation owns the rotating refresh token so
    /// two callers can never race each other into an invalidated session.
    /// </summary>
    public interface ISupabaseSessionProvider
    {
        Task<SupabaseAuthSession> GetValidSessionAsync(CancellationToken cancellationToken);
    }

    public sealed class SupabaseMemberRecord
    {
        public SupabaseMemberRecord(
            Guid id,
            Guid teamId,
            string displayName,
            int sortOrder)
        {
            Id = id;
            TeamId = teamId;
            DisplayName = displayName;
            SortOrder = sortOrder;
        }

        public Guid Id { get; }

        public Guid TeamId { get; }

        public string DisplayName { get; }

        public int SortOrder { get; }
    }

    /// <summary>
    /// Slot usage for the team, readable before joining. A client that knows the
    /// team is full can refuse to sign up rather than creating an anonymous Auth
    /// user that <c>claim_member_name</c> is guaranteed to reject.
    /// </summary>
    public sealed class SupabaseTeamCapacity
    {
        public SupabaseTeamCapacity(int occupied, int capacity)
        {
            Occupied = occupied;
            Capacity = capacity;
        }

        public int Occupied { get; }

        public int Capacity { get; }

        public bool HasRoom => Occupied < Capacity;
    }

    public sealed class SupabaseIdentityBootstrap
    {
        public SupabaseIdentityBootstrap(
            SupabaseMemberRecord member,
            bool createdAnonymousUser)
        {
            Member = member;
            CreatedAnonymousUser = createdAnonymousUser;
        }

        public SupabaseMemberRecord Member { get; }

        public bool CreatedAnonymousUser { get; }
    }

    public sealed class SupabaseApiException : Exception
    {
        public SupabaseApiException(int statusCode, string errorCode, string serverMessage)
            : base(BuildMessage(statusCode, errorCode, serverMessage))
        {
            StatusCode = statusCode;
            ErrorCode = errorCode ?? string.Empty;
            ServerMessage = serverMessage ?? string.Empty;
        }

        public int StatusCode { get; }

        public string ErrorCode { get; }

        public string ServerMessage { get; }

        private static string BuildMessage(int statusCode, string errorCode, string serverMessage)
        {
            var detail = !string.IsNullOrWhiteSpace(serverMessage)
                ? serverMessage
                : errorCode;
            return string.IsNullOrWhiteSpace(detail)
                ? "Supabase request failed with HTTP " + statusCode + "."
                : "Supabase request failed: " + detail + " (HTTP " + statusCode + ").";
        }
    }

    /// <summary>
    /// Refresh-token failure is deliberately distinct: silently creating a new
    /// anonymous user would orphan the name owned by the previous Auth UUID.
    /// </summary>
    public sealed class SupabaseIdentityRecoveryException : Exception
    {
        public SupabaseIdentityRecoveryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public interface ISupabaseClock
    {
        DateTimeOffset UtcNow { get; }
    }

    internal sealed class SystemSupabaseClock : ISupabaseClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
