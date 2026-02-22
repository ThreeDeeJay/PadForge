using System;
using PadForge.Engine;
using PadForge.Engine.Data;

namespace PadForge.Common.Input
{
    /// <summary>
    /// Event arguments for input device events (device connected, disconnected,
    /// state changed, etc.). Carries the relevant device and state information.
    /// </summary>
    public class InputEventArgs : EventArgs
    {
        /// <summary>
        /// The device that triggered the event.
        /// </summary>
        public UserDevice Device { get; }

        /// <summary>
        /// The current input state of the device (may be null for disconnect events).
        /// </summary>
        public CustomInputState State { get; }

        /// <summary>
        /// Buffered updates describing what changed since the last poll cycle.
        /// Empty for connect/disconnect events.
        /// </summary>
        public CustomInputUpdate[] Updates { get; }

        /// <summary>
        /// The type of input event.
        /// </summary>
        public InputEventType EventType { get; }

        /// <summary>
        /// Timestamp when the event was generated.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Creates a new InputEventArgs.
        /// </summary>
        /// <param name="device">The device that triggered the event.</param>
        /// <param name="eventType">The type of event.</param>
        /// <param name="state">Current input state (optional).</param>
        /// <param name="updates">Buffered updates (optional).</param>
        public InputEventArgs(UserDevice device, InputEventType eventType,
            CustomInputState state = null, CustomInputUpdate[] updates = null)
        {
            Device = device;
            EventType = eventType;
            State = state;
            Updates = updates ?? Array.Empty<CustomInputUpdate>();
            Timestamp = DateTime.UtcNow;
        }

        public override string ToString()
        {
            string deviceName = Device?.ResolvedName ?? "(unknown)";
            return $"[{EventType}] {deviceName} at {Timestamp:HH:mm:ss.fff}";
        }
    }

    /// <summary>
    /// Types of input events raised by the input pipeline.
    /// </summary>
    public enum InputEventType
    {
        /// <summary>A new device was detected and opened.</summary>
        DeviceConnected,

        /// <summary>A device was disconnected or lost.</summary>
        DeviceDisconnected,

        /// <summary>A device's input state has changed (button pressed, axis moved, etc.).</summary>
        StateChanged,

        /// <summary>Force feedback was applied to a device.</summary>
        ForceFeedbackApplied,

        /// <summary>Force feedback was stopped on a device.</summary>
        ForceFeedbackStopped
    }
}
