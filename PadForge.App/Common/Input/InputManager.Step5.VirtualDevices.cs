using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using PadForge.Engine;

namespace PadForge.Common.Input
{
    public partial class InputManager
    {
        // ─────────────────────────────────────────────
        //  Step 5: UpdateVirtualDevices
        //  Feeds combined Gamepad states to ViGEmBus virtual Xbox 360
        //  controllers via the Nefarius.ViGEm.Client NuGet package.
        // ─────────────────────────────────────────────

        /// <summary>Shared ViGEmClient instance (one per process).</summary>
        private static ViGEmClient _vigemClient;
        private static readonly object _vigemClientLock = new object();
        private static bool _vigemClientFailed;

        /// <summary>Virtual controller targets (one per slot).</summary>
        private IXbox360Controller[] _virtualControllers = new IXbox360Controller[MaxPads];

        /// <summary>
        /// Count of currently active ViGEm virtual controllers.
        /// Used by IsViGEmVirtualDevice() in Step 1 for zero-VID/PID heuristic.
        /// </summary>
        private int _activeVigemCount;

        /// <summary>
        /// Tracks how many consecutive polling cycles each slot has been inactive.
        /// Virtual controllers are only destroyed after a sustained inactivity period
        /// to prevent transient <see cref="IsSlotActive"/> false returns from
        /// destroying/recreating controllers (which kills vibration feedback).
        /// </summary>
        private readonly int[] _slotInactiveCounter = new int[MaxPads];

        /// <summary>
        /// Number of consecutive inactive cycles before a virtual controller is destroyed.
        /// At ~1000Hz polling, 10000 cycles ≈ 10 seconds of sustained inactivity.
        /// </summary>
        private const int SlotDestroyGraceCycles = 10000;

        /// <summary>Whether virtual controller output is enabled.</summary>
        public bool VirtualControllersEnabled { get; set; } = true;

        /// <summary>Whether ViGEmBus driver is reachable.</summary>
        public bool IsViGEmAvailable => _vigemClient != null;

        /// <summary>
        /// Step 5: Feed each slot's combined gamepad state to ViGEmBus.
        /// Receives vibration feedback from games via the virtual controller.
        ///
        /// Uses a grace period before destroying inactive virtual controllers to
        /// prevent transient IsSlotActive(false) from killing vibration feedback.
        /// Destroying a virtual controller severs the game's vibration connection
        /// (FeedbackReceived stops firing), and recreating it requires the game to
        /// rediscover the controller and re-send XInputSetState — causing a gap.
        /// </summary>
        private void UpdateVirtualDevices()
        {
            if (!VirtualControllersEnabled || _vigemClientFailed)
                return;

            EnsureViGEmClient();
            if (_vigemClient == null)
                return;

            for (int padIndex = 0; padIndex < MaxPads; padIndex++)
            {
                try
                {
                    var gp = CombinedXiStates[padIndex];
                    var vc = _virtualControllers[padIndex];
                    bool slotActive = IsSlotActive(padIndex);

                    if (slotActive)
                    {
                        if (_slotInactiveCounter[padIndex] > 0)
                            RumbleLogger.Log($"[Step5] Pad{padIndex} active again after {_slotInactiveCounter[padIndex]} inactive cycles");

                        _slotInactiveCounter[padIndex] = 0;

                        if (vc == null)
                        {
                            RumbleLogger.Log($"[Step5] Pad{padIndex} creating virtual controller");
                            vc = CreateVirtualController(padIndex);
                            _virtualControllers[padIndex] = vc;
                        }

                        if (vc != null)
                        {
                            SubmitGamepadToVirtual(vc, gp);
                        }
                    }
                    else
                    {
                        // Don't destroy immediately — wait for sustained inactivity.
                        // Transient IsSlotActive=false (e.g., during device enumeration
                        // or brief lock contention) must not kill vibration feedback.
                        _slotInactiveCounter[padIndex]++;

                        if (_slotInactiveCounter[padIndex] == 1)
                            RumbleLogger.Log($"[Step5] Pad{padIndex} !slotActive (vc={vc != null}) VibL={VibrationStates[padIndex].LeftMotorSpeed} VibR={VibrationStates[padIndex].RightMotorSpeed}");

                        if (vc != null && _slotInactiveCounter[padIndex] >= SlotDestroyGraceCycles)
                        {
                            RumbleLogger.Log($"[Step5] Pad{padIndex} destroying virtual controller after {SlotDestroyGraceCycles} inactive cycles");
                            DestroyVirtualController(padIndex);
                            _virtualControllers[padIndex] = null;
                            VibrationStates[padIndex].LeftMotorSpeed = 0;
                            VibrationStates[padIndex].RightMotorSpeed = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    RaiseError($"Error updating virtual controller for pad {padIndex}", ex);
                }
            }
        }

        // ─────────────────────────────────────────────
        //  ViGEm client lifecycle
        // ─────────────────────────────────────────────

        private void EnsureViGEmClient()
        {
            if (_vigemClient != null || _vigemClientFailed)
                return;

            lock (_vigemClientLock)
            {
                if (_vigemClient != null || _vigemClientFailed)
                    return;

                try
                {
                    _vigemClient = new ViGEmClient();
                }
                catch (Nefarius.ViGEm.Client.Exceptions.VigemBusNotFoundException)
                {
                    _vigemClientFailed = true;
                    RaiseError("ViGEmBus driver is not installed.", null);
                }
                catch (Exception ex)
                {
                    _vigemClientFailed = true;
                    RaiseError("Failed to initialize ViGEmClient.", ex);
                }
            }
        }

        /// <summary>
        /// Static check: is ViGEmBus driver installed?
        /// Called by the UI on startup to populate SettingsViewModel.
        /// </summary>
        public static bool CheckViGEmInstalled()
        {
            try
            {
                using var client = new ViGEmClient();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────────────────────────
        //  Slot activity check
        // ─────────────────────────────────────────────

        private bool IsSlotActive(int padIndex)
        {
            var settings = SettingsManager.UserSettings;
            if (settings == null) return false;

            // Use non-allocating overload with pre-allocated buffer.
            int slotCount = settings.FindByPadIndex(padIndex, _padIndexBuffer);
            if (slotCount == 0)
                return false;

            for (int i = 0; i < slotCount; i++)
            {
                var us = _padIndexBuffer[i];
                if (us == null) continue;
                var ud = FindOnlineDeviceByInstanceGuid(us.InstanceGuid);
                if (ud != null && ud.IsOnline)
                    return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────
        //  Virtual controller management
        //  Uses XInput slot mask delta to detect which
        //  slot the new virtual controller occupies.
        // ─────────────────────────────────────────────

        private IXbox360Controller CreateVirtualController(int padIndex)
        {
            if (_vigemClient == null)
                return null;

            try
            {
                // ── Snapshot XInput slot mask BEFORE connecting ──
                uint maskBefore = GetXInputConnectedSlotMask();

                var controller = _vigemClient.CreateXbox360Controller();
                controller.Connect();

                // ── Wait for the new XInput slot to appear ──
                // After Connect(), the ViGEm kernel driver needs a few ms to
                // register the new device with the XInput stack. Spin-wait for
                // up to 50ms for the slot mask to change. This is a one-time
                // cost per controller creation (rare event), not per cycle.
                var waitSw = Stopwatch.StartNew();
                while (waitSw.ElapsedMilliseconds < 50)
                {
                    uint maskAfter = GetXInputConnectedSlotMask();
                    if (maskAfter != maskBefore)
                        break;
                    Thread.SpinWait(100);
                }

                _activeVigemCount++;

                int capturedIndex = padIndex;
                controller.FeedbackReceived += (sender, args) =>
                {
                    if (capturedIndex >= 0 && capturedIndex < MaxPads)
                    {
                        ushort newL = (ushort)(args.LargeMotor * 257);
                        ushort newR = (ushort)(args.SmallMotor * 257);
                        ushort oldL = VibrationStates[capturedIndex].LeftMotorSpeed;
                        ushort oldR = VibrationStates[capturedIndex].RightMotorSpeed;

                        VibrationStates[capturedIndex].LeftMotorSpeed = newL;
                        VibrationStates[capturedIndex].RightMotorSpeed = newR;

                        if (newL != oldL || newR != oldR)
                            RumbleLogger.Log($"[ViGEm] Pad{capturedIndex} feedback L:{oldL}->{newL} R:{oldR}->{newR}");
                    }
                };

                return controller;
            }
            catch (Exception ex)
            {
                RaiseError($"Failed to create virtual controller for pad {padIndex}", ex);
                return null;
            }
        }

        private void DestroyVirtualController(int padIndex)
        {
            var vc = _virtualControllers[padIndex];
            if (vc == null) return;

            try
            {
                vc.Disconnect();

                // Brief wait for the slot to disappear from the XInput stack.
                var waitSw = Stopwatch.StartNew();
                uint maskBefore = GetXInputConnectedSlotMask();
                while (waitSw.ElapsedMilliseconds < 50)
                {
                    uint maskAfter = GetXInputConnectedSlotMask();
                    if (maskAfter != maskBefore)
                        break;
                    Thread.SpinWait(100);
                }

                _activeVigemCount = Math.Max(0, _activeVigemCount - 1);
            }
            catch { /* best effort */ }
        }

        private void DestroyAllVirtualControllers()
        {
            for (int i = 0; i < MaxPads; i++)
            {
                DestroyVirtualController(i);
                _virtualControllers[i] = null;
            }

            _activeVigemCount = 0;
        }

        // ─────────────────────────────────────────────
        //  XInput slot mask — direct P/Invoke to xinput1_4.dll
        //
        //  Used for ViGEm virtual controller management only
        //  (detecting when a newly created virtual controller
        //  appears in the XInput stack).
        // ─────────────────────────────────────────────

        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        private static extern uint XInputGetStateEx(
            uint dwUserIndex, ref XInputStateInternal pState);

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputGamepadInternal
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputStateInternal
        {
            public uint dwPacketNumber;
            public XInputGamepadInternal Gamepad;
        }

        private const uint XINPUT_ERROR_DEVICE_NOT_CONNECTED = 0x048F;

        /// <summary>
        /// Returns a bitmask of connected XInput slots (bit 0 = slot 0, etc.).
        /// Probes slots 0–3 directly via xinput1_4.dll.
        /// </summary>
        private static uint GetXInputConnectedSlotMask()
        {
            uint mask = 0;
            for (uint i = 0; i < 4; i++)
            {
                var state = new XInputStateInternal();
                if (XInputGetStateEx(i, ref state) != XINPUT_ERROR_DEVICE_NOT_CONNECTED)
                    mask |= (1u << (int)i);
            }
            return mask;
        }

        // ─────────────────────────────────────────────
        //  Report submission
        // ─────────────────────────────────────────────

        private static void SubmitGamepadToVirtual(IXbox360Controller vc, Gamepad gp)
        {
            vc.SetButtonState(Xbox360Button.A, (gp.Buttons & Gamepad.A) != 0);
            vc.SetButtonState(Xbox360Button.B, (gp.Buttons & Gamepad.B) != 0);
            vc.SetButtonState(Xbox360Button.X, (gp.Buttons & Gamepad.X) != 0);
            vc.SetButtonState(Xbox360Button.Y, (gp.Buttons & Gamepad.Y) != 0);
            vc.SetButtonState(Xbox360Button.LeftShoulder, (gp.Buttons & Gamepad.LEFT_SHOULDER) != 0);
            vc.SetButtonState(Xbox360Button.RightShoulder, (gp.Buttons & Gamepad.RIGHT_SHOULDER) != 0);
            vc.SetButtonState(Xbox360Button.Back, (gp.Buttons & Gamepad.BACK) != 0);
            vc.SetButtonState(Xbox360Button.Start, (gp.Buttons & Gamepad.START) != 0);
            vc.SetButtonState(Xbox360Button.LeftThumb, (gp.Buttons & Gamepad.LEFT_THUMB) != 0);
            vc.SetButtonState(Xbox360Button.RightThumb, (gp.Buttons & Gamepad.RIGHT_THUMB) != 0);
            vc.SetButtonState(Xbox360Button.Guide, (gp.Buttons & Gamepad.GUIDE) != 0);
            vc.SetButtonState(Xbox360Button.Up, (gp.Buttons & Gamepad.DPAD_UP) != 0);
            vc.SetButtonState(Xbox360Button.Down, (gp.Buttons & Gamepad.DPAD_DOWN) != 0);
            vc.SetButtonState(Xbox360Button.Left, (gp.Buttons & Gamepad.DPAD_LEFT) != 0);
            vc.SetButtonState(Xbox360Button.Right, (gp.Buttons & Gamepad.DPAD_RIGHT) != 0);

            vc.SetAxisValue(Xbox360Axis.LeftThumbX, gp.ThumbLX);
            vc.SetAxisValue(Xbox360Axis.LeftThumbY, gp.ThumbLY);
            vc.SetAxisValue(Xbox360Axis.RightThumbX, gp.ThumbRX);
            vc.SetAxisValue(Xbox360Axis.RightThumbY, gp.ThumbRY);

            vc.SetSliderValue(Xbox360Slider.LeftTrigger, gp.LeftTrigger);
            vc.SetSliderValue(Xbox360Slider.RightTrigger, gp.RightTrigger);

            vc.SubmitReport();
        }
    }
}
