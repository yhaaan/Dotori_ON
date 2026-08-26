using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TeamOverlay.Identity;

namespace TeamOverlay.Tests.EditMode
{
    public sealed class LocalIdentityProfileStoreTests
    {
        private const string ProfilePath = "C:/virtual/team-overlay/identity/local-profile.json";
        private static readonly Guid FixedClientId = new Guid("6c3177b6-31cf-4d8c-a8b9-8f38714a1780");
        private static readonly DateTimeOffset FixedNow =
            new DateTimeOffset(2026, 8, 26, 1, 2, 3, TimeSpan.Zero);

        private MemoryIdentityFileSystem _fileSystem;
        private LocalIdentityProfileStore _store;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new MemoryIdentityFileSystem();
            _store = CreateStore(_fileSystem);
        }

        [Test]
        public void DisplayNamePolicy_CanonicalizesUnicodeAndWhitespace()
        {
            var result = DisplayNamePolicy.Validate("  KIM   하늘  ");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.DisplayName, Is.EqualTo("KIM 하늘"));
            Assert.That(result.UniqueNameKey, Is.EqualTo("kim 하늘"));
        }

        [Test]
        public void DisplayNamePolicy_RejectsUnsupportedAndOverlongNames()
        {
            Assert.That(
                DisplayNamePolicy.Validate(new string('가', 17)).Error,
                Is.EqualTo(DisplayNameValidationError.TooLong));
            Assert.That(
                DisplayNamePolicy.Validate("---").Error,
                Is.EqualTo(DisplayNameValidationError.LetterOrNumberRequired));
            Assert.That(
                DisplayNamePolicy.Validate("하늘🙂").Error,
                Is.EqualTo(DisplayNameValidationError.UnsupportedCharacter));
        }

        [Test]
        public void CreateThenLoad_PreservesStableIdentityAndCanonicalName()
        {
            var created = _store.Create("  김   하늘  ");
            var reloaded = CreateStore(_fileSystem).Load();

            Assert.That(created.ClientInstanceId, Is.EqualTo(FixedClientId));
            Assert.That(created.DisplayName, Is.EqualTo("김 하늘"));
            Assert.That(created.UniqueNameKey, Is.EqualTo("김 하늘"));
            Assert.That(created.CreatedAtUtc, Is.EqualTo(FixedNow));
            Assert.That(reloaded.Status, Is.EqualTo(IdentityProfileLoadStatus.Loaded));
            Assert.That(reloaded.HasProfile, Is.True);
            Assert.That(reloaded.Profile.ClientInstanceId, Is.EqualTo(FixedClientId));
            Assert.That(reloaded.Profile.DisplayName, Is.EqualTo("김 하늘"));
        }

        [Test]
        public void CreateTwice_RefusesToReplaceTheOriginalPerson()
        {
            var created = _store.Create("첫이름");
            var primaryBefore = _fileSystem.GetFile(ProfilePath);

            Assert.Throws<InvalidOperationException>(() => _store.Create("다른이름"));

            var reloaded = CreateStore(_fileSystem).Load();
            Assert.That(reloaded.Profile.ClientInstanceId, Is.EqualTo(created.ClientInstanceId));
            Assert.That(reloaded.Profile.DisplayName, Is.EqualTo("첫이름"));
            Assert.That(_fileSystem.GetFile(ProfilePath), Is.EqualTo(primaryBefore));
        }

        [Test]
        public void Load_RecoversCorruptPrimaryFromBackupAndRepairsIt()
        {
            var created = _store.Create("복구대상");
            _fileSystem.SetFile(ProfilePath, "{ corrupt json");

            var result = CreateStore(_fileSystem).Load();

            Assert.That(result.Status, Is.EqualTo(IdentityProfileLoadStatus.RecoveredFromBackup));
            Assert.That(result.StorageRepairSucceeded, Is.True);
            Assert.That(result.Profile.ClientInstanceId, Is.EqualTo(created.ClientInstanceId));
            Assert.That(CreateStore(_fileSystem).Load().Status, Is.EqualTo(IdentityProfileLoadStatus.Loaded));
        }

        [Test]
        public void CorruptIdentity_IsNeverSilentlyOverwritten()
        {
            _fileSystem.SetFile(ProfilePath, "invalid");

            var result = _store.Load();

            Assert.That(result.Status, Is.EqualTo(IdentityProfileLoadStatus.Corrupt));
            Assert.That(result.HasProfile, Is.False);
            Assert.Throws<InvalidDataException>(() => _store.Create("새사람"));
            Assert.That(_fileSystem.GetFile(ProfilePath), Is.EqualTo("invalid"));
        }

        [Test]
        public void Serializer_RejectsTamperedUniqueNameKey()
        {
            var serializer = new JsonIdentityProfileSerializer();
            var profile = new LocalIdentityProfile(FixedClientId, "Alice", FixedNow);
            var serialized = serializer.Serialize(profile)
                .Replace("\"uniqueNameKey\": \"alice\"", "\"uniqueNameKey\": \"mallory\"");

            Assert.That(serializer.TryDeserialize(serialized, out _), Is.False);
        }

        private static LocalIdentityProfileStore CreateStore(MemoryIdentityFileSystem fileSystem)
        {
            return new LocalIdentityProfileStore(
                new FixedIdentityStoragePathProvider(ProfilePath),
                fileSystem,
                new JsonIdentityProfileSerializer(),
                new FixedIdentityValueSource());
        }

        private sealed class FixedIdentityValueSource : IIdentityValueSource
        {
            public Guid NewClientInstanceId()
            {
                return FixedClientId;
            }

            public DateTimeOffset UtcNow()
            {
                return FixedNow;
            }
        }

        private sealed class MemoryIdentityFileSystem : IIdentityFileSystem
        {
            private readonly Dictionary<string, string> _files =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public bool FileExists(string path)
            {
                return _files.ContainsKey(path);
            }

            public void CreateDirectory(string path)
            {
            }

            public string ReadAllText(string path)
            {
                if (!_files.TryGetValue(path, out var contents))
                {
                    throw new FileNotFoundException("Missing in-memory file.", path);
                }

                return contents;
            }

            public void WriteAllTextAndFlush(string path, string contents)
            {
                _files[path] = contents;
            }

            public void DeleteFile(string path)
            {
                _files.Remove(path);
            }

            public void CommitTempFile(
                string temporaryPath,
                string destinationPath,
                string backupPath)
            {
                if (!_files.TryGetValue(temporaryPath, out var contents))
                {
                    throw new FileNotFoundException("Missing in-memory temporary file.", temporaryPath);
                }

                if (_files.TryGetValue(destinationPath, out var previous))
                {
                    _files[backupPath] = previous;
                }

                _files[destinationPath] = contents;
                _files.Remove(temporaryPath);
            }

            public void SetFile(string path, string contents)
            {
                _files[path] = contents;
            }

            public string GetFile(string path)
            {
                return _files[path];
            }
        }
    }
}
