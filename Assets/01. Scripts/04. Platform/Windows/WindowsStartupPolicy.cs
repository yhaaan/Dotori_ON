using System;

namespace DOTORION.Platform.Windows
{
    /// <summary>
    /// What the Run registry entry has to say for this install to start with
    /// Windows, and whether the entry already there says it.
    ///
    /// Kept apart from the registry itself so the two awkward parts can be
    /// pinned by tests: the quoting a path with a space in it needs, and what to
    /// make of an entry left behind by a copy of the app that has since moved.
    /// </summary>
    public static class WindowsStartupPolicy
    {
        /// <summary>
        /// Per-user, which is the point: writing here never asks for
        /// administrator rights. The machine-wide equivalent under HKLM would,
        /// and there is nothing about one person's overlay that needs it.
        /// </summary>
        public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// The value name under the Run key. It is also the name Task Manager's
        /// Startup tab shows the entry as, so it reads as the product rather
        /// than as an identifier.
        /// </summary>
        public const string ValueName = "DOTORI ON";

        /// <summary>
        /// The command Windows runs at login. The path is always quoted, and
        /// that is not cosmetic here: the executable ships as "DOTORI ON.exe",
        /// and Windows reads an unquoted command up to the first space, which
        /// would leave it looking for a C:\...\DOTORI that does not exist.
        /// </summary>
        public static string BuildCommand(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException(
                    "An executable path is required to build a startup command.",
                    nameof(executablePath));
            }

            return "\"" + Normalize(executablePath) + "\"";
        }

        /// <summary>
        /// Whether an entry already under the Run key starts this same install.
        ///
        /// An entry left by a copy that has since been moved - unzipped
        /// somewhere else, or replaced by an update that landed in another
        /// folder - counts as not registered. The switch then reads off, and
        /// switching it on rewrites the entry to point at wherever the app is
        /// actually running from, which is the repair anyone would want.
        /// </summary>
        public static bool Matches(string storedCommand, string executablePath)
        {
            if (string.IsNullOrWhiteSpace(storedCommand) || string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            // Windows paths are case-insensitive, so a drive letter or a folder
            // that comes back in a different case is still the same install.
            return string.Equals(
                Normalize(storedCommand),
                Normalize(executablePath),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Strips the quotes and the surrounding whitespace a command may or may
        /// not carry, so a value written by hand compares equal to one written here.
        /// </summary>
        private static string Normalize(string command)
        {
            return command.Trim().Trim('"').Trim();
        }
    }
}
