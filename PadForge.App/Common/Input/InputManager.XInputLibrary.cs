using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PadForge.Common.Input
{
    public partial class InputManager
    {
        // ─────────────────────────────────────────────
        //  XInput Library Management
        //  Detects and loads the appropriate XInput DLL for the system.
        //  Windows ships several XInput versions:
        //    xinput1_4.dll  — Windows 8+, supports GetStateEx
        //    xinput1_3.dll  — Windows Vista/7
        //    xinput9_1_0.dll — Minimal subset
        //  PadForge targets xinput1_4.dll as the primary DLL.
        // ─────────────────────────────────────────────

        /// <summary>
        /// Name of the currently loaded XInput DLL.
        /// </summary>
        public static string LoadedXInputLibrary { get; private set; } = string.Empty;

        /// <summary>
        /// Whether an XInput DLL has been successfully loaded.
        /// </summary>
        public static bool IsXInputLibraryLoaded { get; private set; }

        /// <summary>
        /// Preferred XInput DLL names in order of preference.
        /// </summary>
        private static readonly string[] XInputDllNames = new[]
        {
            "xinput1_4.dll",
            "xinput1_3.dll",
            "xinput9_1_0.dll"
        };

        /// <summary>
        /// Attempts to load the XInput DLL. Called automatically when
        /// <see cref="XInputInterop"/> methods are first used.
        /// </summary>
        public static bool LoadXInputLibrary()
        {
            if (IsXInputLibraryLoaded)
                return true;

            string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);

            foreach (string dllName in XInputDllNames)
            {
                string fullPath = Path.Combine(systemDir, dllName);
                if (File.Exists(fullPath))
                {
                    // Verify we can load it.
                    IntPtr handle = NativeLibrary.Load(fullPath);
                    if (handle != IntPtr.Zero)
                    {
                        LoadedXInputLibrary = dllName;
                        IsXInputLibraryLoaded = true;
                        // Don't free — keep loaded for P/Invoke.
                        return true;
                    }
                }
            }

            // Try loading by name only (let the OS resolve).
            foreach (string dllName in XInputDllNames)
            {
                try
                {
                    if (NativeLibrary.TryLoad(dllName, out IntPtr handle) && handle != IntPtr.Zero)
                    {
                        LoadedXInputLibrary = dllName;
                        IsXInputLibraryLoaded = true;
                        return true;
                    }
                }
                catch { /* try next */ }
            }

            return false;
        }

        /// <summary>
        /// Returns the path to the currently loaded XInput DLL, or empty string.
        /// </summary>
        public static string GetXInputLibraryPath()
        {
            if (!IsXInputLibraryLoaded)
                return string.Empty;

            string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string fullPath = Path.Combine(systemDir, LoadedXInputLibrary);
            return File.Exists(fullPath) ? fullPath : LoadedXInputLibrary;
        }

        /// <summary>
        /// Returns information about the loaded XInput library for diagnostic display.
        /// </summary>
        public static string GetXInputLibraryInfo()
        {
            if (!IsXInputLibraryLoaded)
                return "No XInput library loaded.";

            string path = GetXInputLibraryPath();
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                return $"{LoadedXInputLibrary} ({fi.Length / 1024}KB)";
            }

            return LoadedXInputLibrary;
        }
    }
}
