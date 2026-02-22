using System;

namespace PadForge.Engine
{
    /// <summary>
    /// Helper class providing constants and utility methods for working with
    /// <see cref="CustomInputState"/> values. Replaces the former CustomDiHelper class.
    /// </summary>
    public static class CustomInputHelper
    {
        // ─────────────────────────────────────────────
        //  Array size constants (mirrored from CustomInputState for convenience)
        // ─────────────────────────────────────────────

        /// <summary>Maximum number of axes in a <see cref="CustomInputState"/>.</summary>
        public const int MaxAxis = CustomInputState.MaxAxis;

        /// <summary>Maximum number of sliders in a <see cref="CustomInputState"/>.</summary>
        public const int MaxSliders = CustomInputState.MaxSliders;

        /// <summary>Maximum number of POV hat switches in a <see cref="CustomInputState"/>.</summary>
        public const int MaxPovs = CustomInputState.MaxPovs;

        /// <summary>Maximum number of buttons in a <see cref="CustomInputState"/>.</summary>
        public const int MaxButtons = CustomInputState.MaxButtons;

        /// <summary>Unsigned axis center value (32767).</summary>
        public const int AxisCenter = 32767;

        /// <summary>Unsigned axis minimum value (0).</summary>
        public const int AxisMin = 0;

        /// <summary>Unsigned axis maximum value (65535).</summary>
        public const int AxisMax = 65535;

        /// <summary>POV centered value (-1 means no direction pressed).</summary>
        public const int PovCentered = -1;

        // ─────────────────────────────────────────────
        //  State comparison
        // ─────────────────────────────────────────────

        /// <summary>
        /// Compares two input states and returns true if any value differs.
        /// Used for change detection in the recording and update pipeline.
        /// </summary>
        /// <param name="a">First state (may be null).</param>
        /// <param name="b">Second state (may be null).</param>
        /// <returns>True if the states differ in any axis, slider, POV, or button value.</returns>
        public static bool HasChanged(CustomInputState a, CustomInputState b)
        {
            if (a == null && b == null) return false;
            if (a == null || b == null) return true;

            for (int i = 0; i < MaxAxis; i++)
            {
                if (a.Axis[i] != b.Axis[i])
                    return true;
            }

            for (int i = 0; i < MaxSliders; i++)
            {
                if (a.Sliders[i] != b.Sliders[i])
                    return true;
            }

            for (int i = 0; i < MaxPovs; i++)
            {
                if (a.Povs[i] != b.Povs[i])
                    return true;
            }

            for (int i = 0; i < MaxButtons; i++)
            {
                if (a.Buttons[i] != b.Buttons[i])
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Generates a list of <see cref="CustomInputUpdate"/> items describing every
        /// difference between two input states. Used for buffered update notifications
        /// and the input recorder.
        /// </summary>
        /// <param name="oldState">Previous state snapshot (may be null, treated as all-zero).</param>
        /// <param name="newState">Current state snapshot (may be null, treated as all-zero).</param>
        /// <returns>Array of update items. Empty array if no changes.</returns>
        public static CustomInputUpdate[] GetUpdates(CustomInputState oldState, CustomInputState newState)
        {
            if (oldState == null)
                oldState = new CustomInputState();
            if (newState == null)
                newState = new CustomInputState();

            var updates = new System.Collections.Generic.List<CustomInputUpdate>();

            // Axes
            for (int i = 0; i < MaxAxis; i++)
            {
                if (oldState.Axis[i] != newState.Axis[i])
                {
                    updates.Add(new CustomInputUpdate
                    {
                        Type = MapType.Axis,
                        Index = i,
                        Value = newState.Axis[i]
                    });
                }
            }

            // Sliders
            for (int i = 0; i < MaxSliders; i++)
            {
                if (oldState.Sliders[i] != newState.Sliders[i])
                {
                    updates.Add(new CustomInputUpdate
                    {
                        Type = MapType.Slider,
                        Index = i,
                        Value = newState.Sliders[i]
                    });
                }
            }

            // POVs
            for (int i = 0; i < MaxPovs; i++)
            {
                if (oldState.Povs[i] != newState.Povs[i])
                {
                    updates.Add(new CustomInputUpdate
                    {
                        Type = MapType.POV,
                        Index = i,
                        Value = newState.Povs[i]
                    });
                }
            }

            // Buttons
            for (int i = 0; i < MaxButtons; i++)
            {
                if (oldState.Buttons[i] != newState.Buttons[i])
                {
                    updates.Add(new CustomInputUpdate
                    {
                        Type = MapType.Button,
                        Index = i,
                        Value = newState.Buttons[i] ? 1 : 0
                    });
                }
            }

            return updates.ToArray();
        }

        // ─────────────────────────────────────────────
        //  Value extraction helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Reads a value from a <see cref="CustomInputState"/> by type and index.
        /// </summary>
        /// <param name="state">The input state to read from.</param>
        /// <param name="type">The type of input (Axis, Button, Slider, POV).</param>
        /// <param name="index">Zero-based index within the type's array.</param>
        /// <returns>The integer value, or 0 if the index is out of range.</returns>
        public static int GetValue(CustomInputState state, MapType type, int index)
        {
            if (state == null)
                return 0;

            switch (type)
            {
                case MapType.Axis:
                    return (index >= 0 && index < MaxAxis) ? state.Axis[index] : 0;

                case MapType.Slider:
                    return (index >= 0 && index < MaxSliders) ? state.Sliders[index] : 0;

                case MapType.POV:
                    return (index >= 0 && index < MaxPovs) ? state.Povs[index] : PovCentered;

                case MapType.Button:
                    return (index >= 0 && index < MaxButtons && state.Buttons[index]) ? 1 : 0;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// Returns a human-readable label for an input source, e.g., "Axis 0", "Button 3", "POV 0".
        /// </summary>
        public static string GetInputLabel(MapType type, int index)
        {
            return type switch
            {
                MapType.Axis => $"Axis {index}",
                MapType.Slider => $"Slider {index}",
                MapType.POV => $"POV {index}",
                MapType.Button => $"Button {index}",
                _ => $"Unknown {index}"
            };
        }
    }
}
