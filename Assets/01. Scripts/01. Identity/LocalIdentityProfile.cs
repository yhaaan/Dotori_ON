using System;

namespace TeamOverlay.Identity
{
    /// <summary>
    /// Immutable local identity. ClientInstanceId is the stable device/install
    /// identity; UniqueNameKey is the canonical key used to claim a human-facing
    /// name in the backend.
    /// </summary>
    public sealed class LocalIdentityProfile
    {
        public const int CurrentSchemaVersion = 1;

        public LocalIdentityProfile(
            Guid clientInstanceId,
            string displayName,
            DateTimeOffset createdAtUtc)
        {
            if (clientInstanceId == Guid.Empty)
            {
                throw new ArgumentException("A non-empty client instance id is required.", nameof(clientInstanceId));
            }

            var validation = DisplayNamePolicy.Validate(displayName);
            if (!validation.IsValid)
            {
                throw new ArgumentException(
                    $"The display name is invalid: {validation.Error}.",
                    nameof(displayName));
            }

            SchemaVersion = CurrentSchemaVersion;
            ClientInstanceId = clientInstanceId;
            DisplayName = validation.DisplayName;
            UniqueNameKey = validation.UniqueNameKey;
            CreatedAtUtc = createdAtUtc.ToUniversalTime();
        }

        public int SchemaVersion { get; }

        public Guid ClientInstanceId { get; }

        public string DisplayName { get; }

        public string UniqueNameKey { get; }

        public DateTimeOffset CreatedAtUtc { get; }
    }
}
