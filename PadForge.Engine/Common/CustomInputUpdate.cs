namespace PadForge.Engine
{
    /// <summary>
    /// Represents a single buffered input change between two <see cref="CustomInputState"/> snapshots.
    /// Replaces the former CustomDiUpdate class.
    /// 
    /// Used by the input recorder to detect which button was pressed or which axis moved,
    /// and by the update pipeline to generate change notifications.
    /// </summary>
    public struct CustomInputUpdate
    {
        /// <summary>
        /// The type of input source that changed (Axis, Button, Slider, or POV).
        /// </summary>
        public MapType Type;

        /// <summary>
        /// Zero-based index within the type's array. For example, index 0 for Type=Axis
        /// corresponds to <see cref="CustomInputState.Axis"/>[0].
        /// </summary>
        public int Index;

        /// <summary>
        /// The new value after the change.
        /// For axes/sliders: unsigned 0–65535.
        /// For POVs: centidegrees 0–35900 or -1 (centered).
        /// For buttons: 1 = pressed, 0 = released.
        /// </summary>
        public int Value;

        /// <summary>
        /// Returns a human-readable description of this update.
        /// </summary>
        public override string ToString()
        {
            return $"{Type} {Index} = {Value}";
        }
    }
}
