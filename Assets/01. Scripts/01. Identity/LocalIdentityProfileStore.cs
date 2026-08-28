using System;
using System.IO;

namespace DOTORION.Identity
{
    public enum IdentityProfileLoadStatus
    {
        Missing = 0,
        Loaded = 1,
        RecoveredFromBackup = 2,
        RecoveredFromTemporaryFile = 3,
        Corrupt = 4,
        StorageUnavailable = 5
    }

    /// <summary>
    /// Outcome of a profile load. A corrupt or unavailable store never silently
    /// becomes a new identity; the caller decides what to do about it.
    /// </summary>
    public sealed class IdentityProfileLoadResult
    {
        internal IdentityProfileLoadResult(
            IdentityProfileLoadStatus status,
            LocalIdentityProfile profile,
            bool storageRepairSucceeded)
        {
            Status = status;
            Profile = profile;
            StorageRepairSucceeded = storageRepairSucceeded;
        }

        public IdentityProfileLoadStatus Status { get; }

        public LocalIdentityProfile Profile { get; }

        public bool HasProfile => Profile != null;

        public bool StorageRepairSucceeded { get; }
    }

    /// <summary>
    /// Crash-safe local profile storage. Writes go to a temporary file that is
    /// committed with a rename, and the previous contents are kept as a backup so
    /// a torn write can still be recovered on the next launch.
    /// </summary>
    public sealed class LocalIdentityProfileStore
    {
        private readonly object _gate = new object();
        private readonly IIdentityStoragePathProvider _pathProvider;
        private readonly IIdentityFileSystem _fileSystem;
        private readonly IIdentityProfileSerializer _serializer;
        private readonly IIdentityValueSource _valueSource;

        public LocalIdentityProfileStore()
            : this(
                new UnityPersistentIdentityStoragePathProvider(),
                new SystemIdentityFileSystem(),
                new JsonIdentityProfileSerializer(),
                new SystemIdentityValueSource())
        {
        }

        public LocalIdentityProfileStore(
            IIdentityStoragePathProvider pathProvider,
            IIdentityFileSystem fileSystem,
            IIdentityProfileSerializer serializer,
            IIdentityValueSource valueSource)
        {
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _valueSource = valueSource ?? throw new ArgumentNullException(nameof(valueSource));
        }

        public IdentityProfileLoadResult Load()
        {
            lock (_gate)
            {
                return LoadLocked(repairRecoveredProfile: true);
            }
        }

        public LocalIdentityProfile Create(string rawDisplayName)
        {
            var validation = DisplayNamePolicy.Validate(rawDisplayName);
            if (!validation.IsValid)
            {
                throw new ArgumentException(
                    $"The display name is invalid: {validation.Error}.",
                    nameof(rawDisplayName));
            }

            lock (_gate)
            {
                switch (LoadLocked(repairRecoveredProfile: false).Status)
                {
                    case IdentityProfileLoadStatus.Loaded:
                    case IdentityProfileLoadStatus.RecoveredFromBackup:
                    case IdentityProfileLoadStatus.RecoveredFromTemporaryFile:
                        throw new InvalidOperationException("A local identity profile already exists.");
                    case IdentityProfileLoadStatus.Corrupt:
                        throw new InvalidDataException(
                            "The local identity profile is corrupt and must be explicitly recovered or reset.");
                    case IdentityProfileLoadStatus.StorageUnavailable:
                        throw new IOException("The local identity storage is unavailable.");
                    default:
                        var clientInstanceId = _valueSource.NewClientInstanceId();
                        if (clientInstanceId == Guid.Empty)
                        {
                            throw new InvalidOperationException("The identity value source returned an empty UUID.");
                        }

                        var profile = new LocalIdentityProfile(
                            clientInstanceId,
                            validation.DisplayName,
                            _valueSource.UtcNow());
                        PersistLocked(profile);
                        return profile;
                }
            }
        }

        /// <summary>
        /// Points this PC at the same identity under a new name. The client
        /// instance id and creation time are kept deliberately: a rename is not
        /// this PC becoming a new install, and the backend tells devices apart by
        /// that id.
        /// </summary>
        public LocalIdentityProfile Rename(string rawDisplayName)
        {
            var validation = DisplayNamePolicy.Validate(rawDisplayName);
            if (!validation.IsValid)
            {
                throw new ArgumentException(
                    $"The display name is invalid: {validation.Error}.",
                    nameof(rawDisplayName));
            }

            lock (_gate)
            {
                var current = LoadLocked(repairRecoveredProfile: false);
                if (!current.HasProfile)
                {
                    throw new InvalidOperationException(
                        "There is no local identity profile to rename.");
                }

                var renamed = new LocalIdentityProfile(
                    current.Profile.ClientInstanceId,
                    validation.DisplayName,
                    current.Profile.CreatedAtUtc);
                PersistLocked(renamed);
                return renamed;
            }
        }

        /// <summary>
        /// Forgets which profile this PC is signed in as. Only the local pointer is
        /// dropped: the backend member row and its stored Auth session are left
        /// alone, so signing back in with the same name resumes that member.
        /// </summary>
        public void Clear()
        {
            lock (_gate)
            {
                var profilePath = GetProfilePath();
                var backupPath = GetBackupPath(profilePath);
                _fileSystem.DeleteFile(GetTemporaryPath(profilePath));
                _fileSystem.DeleteFile(GetTemporaryPath(backupPath));
                _fileSystem.DeleteFile(GetBackupPath(backupPath));
                _fileSystem.DeleteFile(backupPath + ".previous");
                _fileSystem.DeleteFile(backupPath);
                _fileSystem.DeleteFile(profilePath);
            }
        }

        private IdentityProfileLoadResult LoadLocked(bool repairRecoveredProfile)
        {
            var profilePath = GetProfilePath();
            var backupPath = GetBackupPath(profilePath);
            var temporaryPath = GetTemporaryPath(profilePath);
            var sawInvalid = false;
            var sawUnavailable = false;

            var primaryStatus = TryReadCandidate(profilePath, out var profile);
            if (primaryStatus == CandidateReadStatus.Valid)
            {
                return new IdentityProfileLoadResult(
                    IdentityProfileLoadStatus.Loaded,
                    profile,
                    storageRepairSucceeded: true);
            }

            sawInvalid |= primaryStatus == CandidateReadStatus.Invalid;
            sawUnavailable |= primaryStatus == CandidateReadStatus.Unavailable;

            var backupStatus = TryReadCandidate(backupPath, out profile);
            if (backupStatus == CandidateReadStatus.Valid)
            {
                var repaired = !repairRecoveredProfile || TryRepair(profile);
                return new IdentityProfileLoadResult(
                    IdentityProfileLoadStatus.RecoveredFromBackup,
                    profile,
                    repaired);
            }

            sawInvalid |= backupStatus == CandidateReadStatus.Invalid;
            sawUnavailable |= backupStatus == CandidateReadStatus.Unavailable;

            var temporaryStatus = TryReadCandidate(temporaryPath, out profile);
            if (temporaryStatus == CandidateReadStatus.Valid)
            {
                var repaired = !repairRecoveredProfile || TryRepair(profile);
                return new IdentityProfileLoadResult(
                    IdentityProfileLoadStatus.RecoveredFromTemporaryFile,
                    profile,
                    repaired);
            }

            sawInvalid |= temporaryStatus == CandidateReadStatus.Invalid;
            if (sawUnavailable || temporaryStatus == CandidateReadStatus.Unavailable)
            {
                return new IdentityProfileLoadResult(
                    IdentityProfileLoadStatus.StorageUnavailable,
                    null,
                    storageRepairSucceeded: false);
            }

            if (sawInvalid)
            {
                return new IdentityProfileLoadResult(
                    IdentityProfileLoadStatus.Corrupt,
                    null,
                    storageRepairSucceeded: false);
            }

            return new IdentityProfileLoadResult(
                IdentityProfileLoadStatus.Missing,
                null,
                storageRepairSucceeded: false);
        }

        private CandidateReadStatus TryReadCandidate(string path, out LocalIdentityProfile profile)
        {
            profile = null;
            try
            {
                if (!_fileSystem.FileExists(path))
                {
                    return CandidateReadStatus.Missing;
                }

                var serializedProfile = _fileSystem.ReadAllText(path);
                return _serializer.TryDeserialize(serializedProfile, out profile)
                    ? CandidateReadStatus.Valid
                    : CandidateReadStatus.Invalid;
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return CandidateReadStatus.Unavailable;
            }
        }

        private bool TryRepair(LocalIdentityProfile profile)
        {
            try
            {
                PersistLocked(profile);
                return true;
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return false;
            }
        }

        private void PersistLocked(LocalIdentityProfile profile)
        {
            var profilePath = GetProfilePath();
            var parentDirectory = Path.GetDirectoryName(profilePath);
            if (string.IsNullOrEmpty(parentDirectory))
            {
                throw new InvalidOperationException("The identity profile path must include a directory.");
            }

            _fileSystem.CreateDirectory(parentDirectory);
            var serializedProfile = _serializer.Serialize(profile);
            if (!_serializer.TryDeserialize(serializedProfile, out var verifiedProfile)
                || verifiedProfile.ClientInstanceId != profile.ClientInstanceId
                || !string.Equals(verifiedProfile.UniqueNameKey, profile.UniqueNameKey, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The serialized identity profile failed validation.");
            }

            var backupPath = GetBackupPath(profilePath);
            WriteAndCommit(serializedProfile, profilePath, backupPath);
            WriteAndCommit(serializedProfile, backupPath, backupPath + ".previous");
        }

        private void WriteAndCommit(string contents, string destinationPath, string backupPath)
        {
            var temporaryPath = GetTemporaryPath(destinationPath);
            _fileSystem.DeleteFile(temporaryPath);
            _fileSystem.WriteAllTextAndFlush(temporaryPath, contents);
            _fileSystem.CommitTempFile(temporaryPath, destinationPath, backupPath);
        }

        private string GetProfilePath()
        {
            var profilePath = _pathProvider.ProfilePath;
            if (string.IsNullOrWhiteSpace(profilePath))
            {
                throw new InvalidOperationException("The identity storage path provider returned an empty path.");
            }

            return profilePath;
        }

        private static string GetBackupPath(string profilePath)
        {
            return profilePath + ".backup";
        }

        private static string GetTemporaryPath(string destinationPath)
        {
            return destinationPath + ".tmp";
        }

        private static bool IsStorageException(Exception exception)
        {
            return exception is IOException
                   || exception is UnauthorizedAccessException
                   || exception is NotSupportedException;
        }

        private enum CandidateReadStatus
        {
            Missing = 0,
            Valid = 1,
            Invalid = 2,
            Unavailable = 3
        }
    }
}
