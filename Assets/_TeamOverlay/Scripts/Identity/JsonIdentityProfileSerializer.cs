using System;
using System.Globalization;
using UnityEngine;

namespace TeamOverlay.Identity
{
    public interface IIdentityProfileSerializer
    {
        string Serialize(LocalIdentityProfile profile);

        bool TryDeserialize(string serializedProfile, out LocalIdentityProfile profile);
    }

    public sealed class JsonIdentityProfileSerializer : IIdentityProfileSerializer
    {
        [Serializable]
        private sealed class IdentityProfileDocument
        {
            public int schemaVersion;
            public string clientInstanceId;
            public string displayName;
            public string uniqueNameKey;
            public string createdAtUtc;
        }

        public string Serialize(LocalIdentityProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var document = new IdentityProfileDocument
            {
                schemaVersion = profile.SchemaVersion,
                clientInstanceId = profile.ClientInstanceId.ToString("D"),
                displayName = profile.DisplayName,
                uniqueNameKey = profile.UniqueNameKey,
                createdAtUtc = profile.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)
            };
            return JsonUtility.ToJson(document, true);
        }

        public bool TryDeserialize(string serializedProfile, out LocalIdentityProfile profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(serializedProfile))
            {
                return false;
            }

            IdentityProfileDocument document;
            try
            {
                document = JsonUtility.FromJson<IdentityProfileDocument>(serializedProfile);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (document == null
                || document.schemaVersion != LocalIdentityProfile.CurrentSchemaVersion
                || !Guid.TryParse(document.clientInstanceId, out var clientInstanceId)
                || clientInstanceId == Guid.Empty
                || !DateTimeOffset.TryParseExact(
                    document.createdAtUtc,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var createdAtUtc))
            {
                return false;
            }

            LocalIdentityProfile candidate;
            try
            {
                candidate = new LocalIdentityProfile(
                    clientInstanceId,
                    document.displayName,
                    createdAtUtc);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (!string.Equals(
                candidate.UniqueNameKey,
                document.uniqueNameKey,
                StringComparison.Ordinal))
            {
                return false;
            }

            profile = candidate;
            return true;
        }
    }
}
