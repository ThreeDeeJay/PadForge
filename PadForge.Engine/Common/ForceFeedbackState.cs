using System;
using PadForge.Engine.Data;

namespace PadForge.Engine
{
    /// <summary>
    /// Manages force feedback (rumble) state for a single device.
    /// Tracks cached settings values for change detection and converts
    /// XInput vibration motor speeds to SDL rumble calls.
    /// 
    /// Replaces the former DirectInput Effect-based ForceFeedbackState.
    /// SDL rumble is duration-based (100ms), so the update loop must call
    /// <see cref="SetDeviceForces"/> frequently (at least every ~80ms) to
    /// maintain continuous vibration.
    /// </summary>
    public class ForceFeedbackState
    {
        // ─────────────────────────────────────────────
        //  Cached settings for change detection
        // ─────────────────────────────────────────────

        private int _cachedForceType;
        private bool _cachedForceSwapMotor;
        private int _cachedLeftStrength = -1;
        private int _cachedRightStrength = -1;
        private int _cachedOverallStrength = -1;
        private ushort _cachedLeftMotorSpeed;
        private ushort _cachedRightMotorSpeed;

        // ─────────────────────────────────────────────
        //  Public state
        // ─────────────────────────────────────────────

        /// <summary>
        /// The most recent left (low-frequency) motor speed sent to the device (0–65535).
        /// </summary>
        public ushort LeftMotorSpeed { get; private set; }

        /// <summary>
        /// The most recent right (high-frequency) motor speed sent to the device (0–65535).
        /// </summary>
        public ushort RightMotorSpeed { get; private set; }

        /// <summary>
        /// Whether force feedback is currently active on the device.
        /// </summary>
        public bool IsActive { get; private set; }

        // ─────────────────────────────────────────────
        //  Stop
        // ─────────────────────────────────────────────

        /// <summary>
        /// Stops all rumble on the device.
        /// </summary>
        /// <param name="device">The SDL device wrapper to stop.</param>
        public void StopDeviceForces(SdlDeviceWrapper device)
        {
            if (device == null || !device.HasRumble)
                return;

            device.StopRumble();
            LeftMotorSpeed = 0;
            RightMotorSpeed = 0;
            IsActive = false;
        }

        // ─────────────────────────────────────────────
        //  Set
        // ─────────────────────────────────────────────

        /// <summary>
        /// Calculates and applies rumble forces to the device based on PadSetting
        /// configuration and incoming XInput vibration values.
        /// 
        /// The method:
        /// 1. Reads gain (overall strength) and per-motor strength from PadSetting.
        /// 2. Applies gain scaling to the raw XInput motor speeds.
        /// 3. Swaps motors if configured.
        /// 4. Sends the result via <see cref="SdlDeviceWrapper.SetRumble"/> with 100ms duration.
        /// </summary>
        /// <param name="ud">The user device data model (for device reference).</param>
        /// <param name="device">The SDL device wrapper to rumble.</param>
        /// <param name="ps">PadSetting containing force feedback configuration.</param>
        /// <param name="v">Vibration values from the XInput state (LeftMotorSpeed, RightMotorSpeed).</param>
        public void SetDeviceForces(UserDevice ud, SdlDeviceWrapper device, PadSetting ps, Vibration v)
        {
            if (device == null || !device.HasRumble)
                return;

            if (ps == null || v == null)
            {
                StopDeviceForces(device);
                return;
            }

            // Parse gain settings from PadSetting.
            // ForceOverall: overall gain percentage (0–100, default 100).
            // LeftMotorStrength: left motor percentage (0–100, default 100).
            // RightMotorStrength: right motor percentage (0–100, default 100).
            int overallGain = TryParseInt(ps.ForceOverall, 100);
            int leftGain = TryParseInt(ps.LeftMotorStrength, 100);
            int rightGain = TryParseInt(ps.RightMotorStrength, 100);
            bool swapMotors = TryParseBool(ps.ForceSwapMotor);
            int forceType = TryParseInt(ps.ForceType, 0);

            // Clamp gains to 0–100.
            overallGain = Math.Clamp(overallGain, 0, 100);
            leftGain = Math.Clamp(leftGain, 0, 100);
            rightGain = Math.Clamp(rightGain, 0, 100);

            // Raw XInput motor speeds (0–65535).
            ushort rawLeft = v.LeftMotorSpeed;
            ushort rawRight = v.RightMotorSpeed;

            // Apply per-motor and overall gain.
            double left = rawLeft * (leftGain / 100.0) * (overallGain / 100.0);
            double right = rawRight * (rightGain / 100.0) * (overallGain / 100.0);

            // Clamp to ushort range.
            ushort finalLeft = (ushort)Math.Clamp(left, 0, 65535);
            ushort finalRight = (ushort)Math.Clamp(right, 0, 65535);

            // Swap motors if configured.
            if (swapMotors)
            {
                (finalLeft, finalRight) = (finalRight, finalLeft);
            }

            // Apply rumble.
            // SDL rumble duration is 100ms; the ~1000Hz update loop refreshes before expiry.
            bool success = device.SetRumble(finalLeft, finalRight, 100);

            if (success)
            {
                LeftMotorSpeed = finalLeft;
                RightMotorSpeed = finalRight;
                IsActive = finalLeft > 0 || finalRight > 0;
            }
        }

        // ─────────────────────────────────────────────
        //  Change detection
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns true if any force feedback setting in the <see cref="PadSetting"/>
        /// has changed since the last call to <see cref="SetDeviceForces"/>. This is used
        /// to avoid redundant rumble updates when settings haven't changed.
        /// </summary>
        /// <param name="ps">The current PadSetting to compare against cached values.</param>
        /// <returns>True if any setting differs from the cached value.</returns>
        public bool Changed(PadSetting ps)
        {
            if (ps == null)
                return false;

            int forceType = TryParseInt(ps.ForceType, 0);
            bool swapMotor = TryParseBool(ps.ForceSwapMotor);
            int leftStrength = TryParseInt(ps.LeftMotorStrength, 100);
            int rightStrength = TryParseInt(ps.RightMotorStrength, 100);
            int overallStrength = TryParseInt(ps.ForceOverall, 100);

            bool changed =
                _cachedForceType != forceType ||
                _cachedForceSwapMotor != swapMotor ||
                _cachedLeftStrength != leftStrength ||
                _cachedRightStrength != rightStrength ||
                _cachedOverallStrength != overallStrength;

            if (changed)
            {
                _cachedForceType = forceType;
                _cachedForceSwapMotor = swapMotor;
                _cachedLeftStrength = leftStrength;
                _cachedRightStrength = rightStrength;
                _cachedOverallStrength = overallStrength;
            }

            return changed;
        }

        // ─────────────────────────────────────────────
        //  Parse helpers
        // ─────────────────────────────────────────────

        private static int TryParseInt(string value, int defaultValue)
        {
            if (string.IsNullOrEmpty(value))
                return defaultValue;
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        private static bool TryParseBool(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Vibration — lightweight struct matching XInput XINPUT_VIBRATION
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents XInput vibration motor speeds. Matches the layout of
    /// XINPUT_VIBRATION so it can be populated directly from XInput state.
    /// </summary>
    public class Vibration
    {
        /// <summary>Left motor (low-frequency, heavy rumble) speed. Range: 0–65535.</summary>
        public ushort LeftMotorSpeed { get; set; }

        /// <summary>Right motor (high-frequency, light buzz) speed. Range: 0–65535.</summary>
        public ushort RightMotorSpeed { get; set; }

        /// <summary>
        /// Creates a zeroed vibration (no rumble).
        /// </summary>
        public Vibration() { }

        /// <summary>
        /// Creates a vibration with the specified motor speeds.
        /// </summary>
        public Vibration(ushort leftMotor, ushort rightMotor)
        {
            LeftMotorSpeed = leftMotor;
            RightMotorSpeed = rightMotor;
        }
    }
}
