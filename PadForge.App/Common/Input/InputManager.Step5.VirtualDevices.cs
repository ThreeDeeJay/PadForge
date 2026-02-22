using System;
using System.Collections.Generic;
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
        /// XInput user indices currently occupied by our ViGEm virtual controllers.
        /// Used by Step 1 to avoid reading back our own virtual controllers as input
        /// devices (loopback prevention).
        /// </summary>
        private readonly HashSet<int> _vigemOccupiedXInputSlots = new HashSet<int>();

        /// <summary>Whether virtual controller output is enabled.</summary>
        public bool VirtualControllersEnabled { get; set; } = true;

        /// <summary>Whether ViGEmBus driver is reachable.</summary>
        public bool IsViGEmAvailable => _vigemClient != null;

        /// <summary>
        /// Step 5: Feed each slot's combined gamepad state to ViGEmBus.
        /// Receives vibration feedback from games via the virtual controller.
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
                        if (vc == null)
                        {
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
                        if (vc != null)
                        {
                            DestroyVirtualController(padIndex);
                            _virtualControllers[padIndex] = null;
                        }

                        VibrationStates[padIndex].LeftMotorSpeed = 0;
                        VibrationStates[padIndex].RightMotorSpeed = 0;
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

            var slotSettings = settings.FindByPadIndex(padIndex);
            if (slotSettings == null || slotSettings.Count == 0)
                return false;

            foreach (var us in slotSettings)
            {
                var ud = FindOnlineDeviceByInstanceGuid(us.InstanceGuid);
                if (ud != null && ud.IsOnline)
                    return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────
        //  Virtual controller management
        // ─────────────────────────────────────────────

        private IXbox360Controller CreateVirtualController(int padIndex)
        {
            if (_vigemClient == null)
                return null;

            try
            {
                var controller = _vigemClient.CreateXbox360Controller();
                controller.Connect();

                // Record which XInput user index this virtual controller landed on
                // so that Step 1 can skip it during XInput enumeration (loopback prevention).
                try
                {
                    int userIndex = (int)controller.UserIndex;
                    if (userIndex >= 0 && userIndex < MaxPads)
                    {
                        lock (_vigemOccupiedXInputSlots)
                        {
                            _vigemOccupiedXInputSlots.Add(userIndex);
                        }
                    }
                }
                catch
                {
                    // UserIndex may not be available on all ViGEm versions; ignore.
                }

                int capturedIndex = padIndex;
                controller.FeedbackReceived += (sender, args) =>
                {
                    if (capturedIndex >= 0 && capturedIndex < MaxPads)
                    {
                        VibrationStates[capturedIndex].LeftMotorSpeed =
                            (ushort)(args.LargeMotor * 257);
                        VibrationStates[capturedIndex].RightMotorSpeed =
                            (ushort)(args.SmallMotor * 257);
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
                // Remove from loopback tracking before disconnecting.
                try
                {
                    int userIndex = (int)vc.UserIndex;
                    lock (_vigemOccupiedXInputSlots)
                    {
                        _vigemOccupiedXInputSlots.Remove(userIndex);
                    }
                }
                catch { /* UserIndex may not be available */ }

                vc.Disconnect();
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
