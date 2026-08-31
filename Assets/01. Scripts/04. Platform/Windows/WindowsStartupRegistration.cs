using System;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using Microsoft.Win32;
#endif

namespace DOTORION.Platform.Windows
{
    /// <summary>
    /// Reads and writes this install's "start with Windows" entry.
    ///
    /// Everything here does nothing outside a Windows player. In the Editor the
    /// running executable is Unity itself, and registering that would put the
    /// Editor into the person's startup list - so play mode reports the switch
    /// as off and refuses to turn it on rather than pretending it worked.
    /// </summary>
    public static class WindowsStartupRegistration
    {
        /// <summary>
        /// Whether this build can register itself at all. False in the Editor
        /// and on every platform that is not a Windows player.
        /// </summary>
        public static bool IsSupported
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            get { return true; }
#else
            get { return false; }
#endif
        }

        /// <summary>
        /// Whether Windows currently starts this install at login.
        ///
        /// The registry is the setting, not a copy of it: nothing in PlayerPrefs
        /// shadows this, because the person can also remove the entry from Task
        /// Manager's Startup tab and the panel has to agree with what they did.
        /// </summary>
        public static bool IsEnabled()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(WindowsStartupPolicy.RunKeyPath, false))
                {
                    var stored = key?.GetValue(WindowsStartupPolicy.ValueName) as string;
                    return WindowsStartupPolicy.Matches(stored, GetExecutablePath());
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("Could not read the Windows startup entry: " + error.Message);
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// Adds or removes the entry. Returns whether the registry actually took
        /// the change, so the caller can put the switch back and say so rather
        /// than leaving it showing a state Windows does not agree with.
        /// </summary>
        public static bool SetEnabled(bool enabled)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                // CreateSubKey rather than OpenSubKey for the write: the Run key
                // is there on any normal profile, but it is not guaranteed, and
                // creating it is both harmless and what every installer does.
                using (var key = Registry.CurrentUser.CreateSubKey(WindowsStartupPolicy.RunKeyPath))
                {
                    if (key == null)
                    {
                        return false;
                    }

                    if (enabled)
                    {
                        key.SetValue(
                            WindowsStartupPolicy.ValueName,
                            WindowsStartupPolicy.BuildCommand(GetExecutablePath()),
                            RegistryValueKind.String);
                    }
                    else
                    {
                        // throwOnMissingValue: false - switching off something
                        // that was already removed elsewhere is not a failure.
                        key.DeleteValue(WindowsStartupPolicy.ValueName, false);
                    }
                }

                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Could not write the Windows startup entry: " + error.Message);
                return false;
            }
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        /// <summary>
        /// The running executable's own path, taken from the process rather than
        /// rebuilt from Application.dataPath and the product name. A build that
        /// was renamed, or a second copy run from another folder, then registers
        /// the file that is actually running instead of one that may not exist.
        /// </summary>
        private static string GetExecutablePath()
        {
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                return process.MainModule.FileName;
            }
        }
#endif
    }
}
