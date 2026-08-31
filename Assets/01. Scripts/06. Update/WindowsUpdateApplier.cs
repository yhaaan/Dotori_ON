using System;
using System.IO;
using DOTORION.Platform.Windows;
using UnityEngine;

namespace DOTORION.Update
{
    /// <summary>
    /// Hands the downloaded zip to a helper process and gets out of its way.
    ///
    /// A running executable cannot overwrite itself, so the swap has to outlive
    /// the app: this writes the shipped PowerShell script out to the temp folder,
    /// starts it with this process's own id, and returns. The caller then quits
    /// through the ordinary exit path, and the script - which has been waiting
    /// for that id to disappear - unpacks the release and starts it again.
    /// </summary>
    public static class WindowsUpdateApplier
    {
        /// <summary>
        /// The folder the release zip carries at its top level. The script copies
        /// out of it, so it has to match what <c>Tools/release.ps1</c> packs.
        /// </summary>
        public const string ArchiveRootName = "DOTORI ON";

        /// <summary>Where the script and the download live while an update runs.</summary>
        public static string WorkFolder =>
            Path.Combine(Path.GetTempPath(), "DOTORI ON", "update");

        public static bool IsSupported
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            get { return true; }
#else
            get { return false; }
#endif
        }

        /// <summary>
        /// Starts the helper. Returns whether it is running - the caller must not
        /// quit on a false, or the person is left with a closed app and no update.
        /// </summary>
        public static bool Launch(string zipPath, string scriptText)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
                {
                    Debug.LogWarning("Update was asked to apply a zip that is not there: " + zipPath);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(scriptText))
                {
                    Debug.LogWarning("The updater script resource is missing from this build.");
                    return false;
                }

                var executable = WindowsExecutablePath.Current;
                var installFolder = WindowsExecutablePath.InstallFolder;
                if (string.IsNullOrEmpty(executable) || string.IsNullOrEmpty(installFolder))
                {
                    Debug.LogWarning("Update could not work out where this build is installed.");
                    return false;
                }

                Directory.CreateDirectory(WorkFolder);
                var scriptPath = Path.Combine(WorkFolder, "apply-update.ps1");
                // A BOM: Windows PowerShell 5.1 reads a file without one as the
                // system codepage, which mangles any non-ASCII path it is handed.
                File.WriteAllText(scriptPath, scriptText, new System.Text.UTF8Encoding(true));

                var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                var arguments =
                    "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " + Quote(scriptPath) +
                    " -ProcessId " + processId +
                    " -ZipPath " + Quote(zipPath) +
                    " -InstallDir " + Quote(installFolder) +
                    " -ExePath " + Quote(executable) +
                    " -RootName " + Quote(ArchiveRootName);

                var start = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = arguments,
                    // Not shell execute, so the helper does not die with the
                    // window this process is about to close.
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = WorkFolder
                };

                var helper = System.Diagnostics.Process.Start(start);
                return helper != null;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Could not start the updater: " + error.Message);
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// PowerShell splits an unquoted argument at spaces, and both the install
        /// folder and the archive root have one in them.
        /// </summary>
        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
