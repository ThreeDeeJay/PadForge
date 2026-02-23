using System;
using System.Diagnostics;
using System.IO;

namespace PadForge.Engine
{
    /// <summary>
    /// Lightweight diagnostic logger for force feedback debugging.
    /// Only logs when values change or SDL calls are made — not every cycle.
    /// Output goes to "rumble_log.txt" next to the executable.
    ///
    /// Enable by setting <see cref="Enabled"/> = true (off by default).
    /// Delete this file once the issue is diagnosed.
    /// </summary>
    public static class RumbleLogger
    {
        /// <summary>Set to true to enable rumble logging.</summary>
        public static bool Enabled { get; set; }

        private static StreamWriter _writer;
        private static readonly object _lock = new object();
        private static readonly Stopwatch _sw = Stopwatch.StartNew();

        /// <summary>
        /// Logs a timestamped message. Thread-safe.
        /// Only writes if <see cref="Enabled"/> is true.
        /// </summary>
        public static void Log(string message)
        {
            if (!Enabled)
                return;

            lock (_lock)
            {
                try
                {
                    if (_writer == null)
                    {
                        string dir = AppDomain.CurrentDomain.BaseDirectory;
                        string path = Path.Combine(dir, "rumble_log.txt");
                        _writer = new StreamWriter(path, append: false) { AutoFlush = true };
                        _writer.WriteLine($"--- Rumble log started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
                    }

                    _writer.WriteLine($"[{_sw.Elapsed.TotalSeconds:F4}] {message}");
                }
                catch
                {
                    // Best effort — don't crash the app for diagnostics.
                }
            }
        }

        /// <summary>
        /// Closes the log file. Call on app shutdown.
        /// </summary>
        public static void Close()
        {
            lock (_lock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }
    }
}
