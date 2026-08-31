using System;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
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
                return WindowsStartupPolicy.Matches(ReadRunValue(), GetExecutablePath());
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
                return enabled
                    ? WriteRunValue(WindowsStartupPolicy.BuildCommand(GetExecutablePath()))
                    : DeleteRunValue();
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
        // advapi32 is called directly rather than through Microsoft.Win32.Registry.
        // The managed API is not in the .NET Standard 2.1 surface this project
        // builds against - it ships only as a Mono facade - so it compiles in the
        // Editor and then fails the player build with "Registry does not exist".
        // The credential store next door reaches CredWrite the same way.

        private static readonly IntPtr HkeyCurrentUser = new IntPtr(unchecked((int)0x80000001));

        private const int ErrorSuccess = 0;
        private const int ErrorFileNotFound = 2;

        private const int KeyRead = 0x20019;
        private const int KeyWrite = 0x20006;

        private const int RegSz = 1;

        /// <summary>
        /// Only ever read, never written. A command somebody wrote by hand may
        /// carry environment variables, and it still names this install.
        /// </summary>
        private const int RegExpandSz = 2;

        private static string ReadRunValue()
        {
            IntPtr key;
            if (RegOpenKeyEx(HkeyCurrentUser, WindowsStartupPolicy.RunKeyPath, 0, KeyRead, out key)
                != ErrorSuccess)
            {
                return null;
            }

            try
            {
                var type = 0;
                var size = 0;
                // A null buffer asks only how many bytes the value needs.
                if (RegQueryValueEx(key, WindowsStartupPolicy.ValueName, IntPtr.Zero, ref type, null, ref size)
                        != ErrorSuccess ||
                    size <= 0 ||
                    (type != RegSz && type != RegExpandSz))
                {
                    return null;
                }

                var buffer = new byte[size];
                if (RegQueryValueEx(key, WindowsStartupPolicy.ValueName, IntPtr.Zero, ref type, buffer, ref size)
                    != ErrorSuccess)
                {
                    return null;
                }

                // The byte count includes the string's terminating null.
                return Encoding.Unicode.GetString(buffer, 0, size).TrimEnd('\0');
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        private static bool WriteRunValue(string command)
        {
            IntPtr key;
            // Created rather than merely opened: the Run key is there on any
            // normal profile, but it is not guaranteed, and making it is both
            // harmless and what every installer does.
            if (RegCreateKeyEx(HkeyCurrentUser, WindowsStartupPolicy.RunKeyPath, 0, null, 0,
                    KeyWrite, IntPtr.Zero, out key, IntPtr.Zero) != ErrorSuccess)
            {
                return false;
            }

            try
            {
                var bytes = Encoding.Unicode.GetBytes(command + "\0");
                return RegSetValueEx(key, WindowsStartupPolicy.ValueName, 0, RegSz, bytes, bytes.Length)
                    == ErrorSuccess;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        private static bool DeleteRunValue()
        {
            IntPtr key;
            var opened = RegOpenKeyEx(HkeyCurrentUser, WindowsStartupPolicy.RunKeyPath, 0, KeyWrite, out key);
            if (opened == ErrorFileNotFound)
            {
                // No Run key at all, so there is nothing registered to remove.
                return true;
            }

            if (opened != ErrorSuccess)
            {
                return false;
            }

            try
            {
                var status = RegDeleteValue(key, WindowsStartupPolicy.ValueName);
                // Removing what something else already removed is not a failure.
                return status == ErrorSuccess || status == ErrorFileNotFound;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        /// <summary>
        /// The running executable's own path, asked of Windows rather than
        /// rebuilt from Application.dataPath and the product name. A build that
        /// was renamed, or a second copy run from another folder, then registers
        /// the file that is actually running instead of one that may not exist.
        /// </summary>
        private static string GetExecutablePath()
        {
            var buffer = new StringBuilder(1024);
            var length = GetModuleFileName(IntPtr.Zero, buffer, buffer.Capacity);
            return length > 0 ? buffer.ToString(0, length) : null;
        }

        [DllImport("advapi32.dll", EntryPoint = "RegOpenKeyExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegOpenKeyEx(
            IntPtr key, string subKey, int options, int desiredAccess, out IntPtr result);

        [DllImport("advapi32.dll", EntryPoint = "RegCreateKeyExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegCreateKeyEx(
            IntPtr key,
            string subKey,
            int reserved,
            string keyClass,
            int options,
            int desiredAccess,
            IntPtr securityAttributes,
            out IntPtr result,
            IntPtr disposition);

        [DllImport("advapi32.dll", EntryPoint = "RegQueryValueExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegQueryValueEx(
            IntPtr key, string valueName, IntPtr reserved, ref int type, byte[] data, ref int dataSize);

        [DllImport("advapi32.dll", EntryPoint = "RegSetValueExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegSetValueEx(
            IntPtr key, string valueName, int reserved, int type, byte[] data, int dataSize);

        [DllImport("advapi32.dll", EntryPoint = "RegDeleteValueW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegDeleteValue(IntPtr key, string valueName);

        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr key);

        [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetModuleFileName(IntPtr module, StringBuilder fileName, int size);
#endif
    }
}
