using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PadForge.Common.Input;

namespace PadForge.Services
{
    /// <summary>
    /// Monitors the foreground window and fires an event when the foreground
    /// process matches a profile's executable list. Called at 30Hz from the
    /// UI timer in <see cref="InputService"/>.
    /// </summary>
    public class ForegroundMonitorService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private string _lastExeName;
        private string _lastMatchedProfileId;

        /// <summary>
        /// Raised when the foreground process matches a different profile than the
        /// currently active one. The string argument is the profile ID to switch to,
        /// or null to revert to the default profile.
        /// </summary>
        public event Action<string> ProfileSwitchRequired;

        /// <summary>
        /// Checks the foreground window process against all profile executable lists.
        /// Only fires <see cref="ProfileSwitchRequired"/> when the matched profile changes.
        /// </summary>
        public void CheckForegroundWindow()
        {
            if (!SettingsManager.EnableAutoProfileSwitching)
                return;

            var profiles = SettingsManager.Profiles;
            if (profiles == null || profiles.Count == 0)
                return;

            string exeName = GetForegroundExeName();
            if (exeName == _lastExeName)
                return; // Same process — skip redundant lookups.

            _lastExeName = exeName;

            // Find matching profile.
            string matchedId = null;
            if (!string.IsNullOrEmpty(exeName))
            {
                foreach (var profile in profiles)
                {
                    if (MatchesExecutables(exeName, profile.ExecutableNames))
                    {
                        matchedId = profile.Id;
                        break;
                    }
                }
            }

            // Only fire if the matched profile changed.
            if (matchedId != _lastMatchedProfileId)
            {
                _lastMatchedProfileId = matchedId;
                ProfileSwitchRequired?.Invoke(matchedId);
            }
        }

        private static string GetForegroundExeName()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return null;

                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0)
                    return null;

                using var proc = Process.GetProcessById((int)pid);
                return proc.ProcessName;
            }
            catch
            {
                return null;
            }
        }

        private static bool MatchesExecutables(string processName, string executables)
        {
            if (string.IsNullOrEmpty(executables))
                return false;

            var parts = executables.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var exe in parts)
            {
                // Strip .exe extension for comparison if present.
                string name = exe;
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - 4);

                if (string.Equals(name, processName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
