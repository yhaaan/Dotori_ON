using System;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
#endif

namespace DOTORION.Platform.Windows
{
    /// <summary>
    /// Where this build is running from.
    ///
    /// Asked of Windows rather than rebuilt out of Application.dataPath and the
    /// product name, so a copy that was renamed or unzipped somewhere unusual
    /// still reports the file that is actually running. Both the startup entry
    /// and the updater need to name that file, and they must agree.
    /// </summary>
    public static class WindowsExecutablePath
    {
        /// <summary>
        /// The full path of the running executable, or null outside a Windows
        /// player - in the Editor the running executable is Unity itself, which
        /// is never the answer any caller here wants.
        /// </summary>
        public static string Current
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            get
            {
                var buffer = new StringBuilder(1024);
                var length = GetModuleFileName(IntPtr.Zero, buffer, buffer.Capacity);
                return length > 0 ? buffer.ToString(0, length) : null;
            }
#else
            get { return null; }
#endif
        }

        /// <summary>The folder the executable sits in, or null when it is unknown.</summary>
        public static string InstallFolder
        {
            get
            {
                var executable = Current;
                return string.IsNullOrEmpty(executable)
                    ? null
                    : System.IO.Path.GetDirectoryName(executable);
            }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetModuleFileName(IntPtr module, StringBuilder fileName, int size);
#endif
    }
}
