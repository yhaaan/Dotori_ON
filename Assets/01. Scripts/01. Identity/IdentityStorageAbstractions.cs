using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DOTORION.Identity
{
    public interface IIdentityStoragePathProvider
    {
        string ProfilePath { get; }
    }

    public sealed class UnityPersistentIdentityStoragePathProvider : IIdentityStoragePathProvider
    {
        public string ProfilePath => Path.Combine(
            Application.persistentDataPath,
            "identity",
            "local-profile.json");
    }

    /// <summary>
    /// Useful for edit-mode tests and command-line tools that must not touch the
    /// real Unity persistent-data directory.
    /// </summary>
    public sealed class FixedIdentityStoragePathProvider : IIdentityStoragePathProvider
    {
        public FixedIdentityStoragePathProvider(string profilePath)
        {
            if (string.IsNullOrWhiteSpace(profilePath))
            {
                throw new ArgumentException("A profile path is required.", nameof(profilePath));
            }

            ProfilePath = profilePath;
        }

        public string ProfilePath { get; }
    }

    public interface IIdentityFileSystem
    {
        bool FileExists(string path);

        void CreateDirectory(string path);

        string ReadAllText(string path);

        void WriteAllTextAndFlush(string path, string contents);

        void DeleteFile(string path);

        void CommitTempFile(string temporaryPath, string destinationPath, string backupPath);
    }

    /// <summary>
    /// Real filesystem implementation. Commits use same-volume rename/replace and
    /// retain the previous destination as a backup. The fallback restores the old
    /// destination if a platform does not implement File.Replace.
    /// </summary>
    public sealed class SystemIdentityFileSystem : IIdentityFileSystem
    {
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, true);

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path, Utf8WithoutBom);
        }

        public void WriteAllTextAndFlush(string path, string contents)
        {
            using (var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, Utf8WithoutBom))
            {
                writer.Write(contents);
                writer.Flush();
                stream.Flush(true);
            }
        }

        public void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public void CommitTempFile(string temporaryPath, string destinationPath, string backupPath)
        {
            if (!File.Exists(temporaryPath))
            {
                throw new FileNotFoundException("The temporary identity file does not exist.", temporaryPath);
            }

            if (!File.Exists(destinationPath))
            {
                File.Move(temporaryPath, destinationPath);
                return;
            }

            DeleteFile(backupPath);
            try
            {
                File.Replace(temporaryPath, destinationPath, backupPath, true);
                return;
            }
            catch (PlatformNotSupportedException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (IOException)
            {
                // Some filesystems do not expose replace even when rename works.
                // The recoverable fallback below keeps the old file as backup.
            }

            File.Move(destinationPath, backupPath);
            try
            {
                File.Move(temporaryPath, destinationPath);
            }
            catch
            {
                if (!File.Exists(destinationPath) && File.Exists(backupPath))
                {
                    File.Move(backupPath, destinationPath);
                }

                throw;
            }
        }
    }

    public interface IIdentityValueSource
    {
        Guid NewClientInstanceId();

        DateTimeOffset UtcNow();
    }

    public sealed class SystemIdentityValueSource : IIdentityValueSource
    {
        public Guid NewClientInstanceId()
        {
            return Guid.NewGuid();
        }

        public DateTimeOffset UtcNow()
        {
            return DateTimeOffset.UtcNow;
        }
    }
}
