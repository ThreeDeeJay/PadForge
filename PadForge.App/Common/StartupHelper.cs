using System.Diagnostics;
using Microsoft.Win32;

namespace PadForge.Common
{
    /// <summary>
    /// Manages the Windows startup registry entry for PadForge.
    /// Uses HKCU\Software\Microsoft\Windows\CurrentVersion\Run,
    /// which does not require elevation.
    /// </summary>
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "PadForge";

        /// <summary>
        /// Returns true if the PadForge startup entry exists in the registry.
        /// </summary>
        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates or removes the PadForge startup registry entry.
        /// </summary>
        public static void SetStartupEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null) return;

                if (enabled)
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                        key.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch
            {
                // HKCU\Run is almost always writable, but fail silently
                // to prevent unexpected crashes.
            }
        }
    }
}
