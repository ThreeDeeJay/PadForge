using System;
using System.Collections.Generic;
using System.Linq;
using PadForge.Engine;
using PadForge.Engine.Data;
using SDL2;
using static SDL2.SDL;

namespace PadForge.Common.Input
{
    public partial class InputManager
    {
        // ─────────────────────────────────────────────
        //  Step 1: UpdateDevices
        //  Enumerates SDL joystick devices, opens newly connected devices,
        //  marks disconnected devices as offline, and tracks ViGEm slot assignments.
        // ─────────────────────────────────────────────

        /// <summary>
        /// Set of SDL instance IDs that we have already opened.
        /// Used to detect new vs. already-known devices.
        /// </summary>
        private readonly HashSet<int> _openedSdlInstanceIds = new HashSet<int>();

        /// <summary>
        /// Maps SDL instance ID → device index at time of opening.
        /// Used for device tracking during disconnection.
        /// </summary>
        private readonly Dictionary<int, int> _sdlInstanceToDeviceIndex = new Dictionary<int, int>();

        /// <summary>
        /// Step 1: Enumerate all connected SDL joystick devices.
        /// 
        /// For each device found by SDL:
        ///   - If not yet opened: open it, create/update a UserDevice record, mark online
        ///   - If already opened: verify it's still attached
        /// 
        /// For each previously opened device not found in current enumeration:
        ///   - Mark offline, close SDL handle
        /// 
        /// Fires <see cref="DevicesUpdated"/> if the device list changed.
        /// </summary>
        private void UpdateDevices()
        {
            if (!_sdlInitialized)
                return;

            bool changed = false;
            int numJoysticks = SDL_NumJoysticks();

            // Build a set of device indices currently visible to SDL.
            var currentDeviceIndices = new HashSet<int>();
            for (int i = 0; i < numJoysticks; i++)
            {
                currentDeviceIndices.Add(i);
            }

            // --- Phase 1: Open newly connected devices ---
            for (int deviceIndex = 0; deviceIndex < numJoysticks; deviceIndex++)
            {
                try
                {
                    // Get pre-open identification to check if already tracked.
                    ushort vid = SDL_JoystickGetDeviceVendor(deviceIndex);
                    ushort pid = SDL_JoystickGetDeviceProduct(deviceIndex);
                    string path = SDL_JoystickPathForIndex(deviceIndex);
                    Guid instanceGuid = SdlDeviceWrapper.BuildInstanceGuid(path, vid, pid, deviceIndex);

                    // Check if we already have this device online.
                    UserDevice existingUd = FindOnlineDeviceByInstanceGuid(instanceGuid);
                    if (existingUd != null && existingUd.IsOnline && existingUd.Device != null)
                    {
                        // Already open — verify still attached.
                        if (!existingUd.Device.IsAttached)
                        {
                            MarkDeviceOffline(existingUd);
                            changed = true;
                        }
                        continue;
                    }

                    // Open the device.
                    var wrapper = new SdlDeviceWrapper();
                    if (!wrapper.Open(deviceIndex))
                    {
                        wrapper.Dispose();
                        continue;
                    }

                    // Skip devices that are native XInput controllers —
                    // those are handled via XInputInterop in Step 2.
                    if (IsNativeXInputDevice(wrapper))
                    {
                        wrapper.Dispose();
                        continue;
                    }

                    // Find or create the UserDevice record.
                    UserDevice ud = FindOrCreateUserDevice(wrapper.InstanceGuid);

                    // Populate from the SDL device.
                    ud.LoadFromSdlDevice(wrapper);
                    ud.IsOnline = true;

                    // Track the SDL instance ID.
                    _openedSdlInstanceIds.Add(wrapper.SdlInstanceId);
                    _sdlInstanceToDeviceIndex[wrapper.SdlInstanceId] = deviceIndex;

                    changed = true;
                }
                catch (Exception ex)
                {
                    RaiseError($"Error opening device at index {deviceIndex}", ex);
                }
            }

            // --- Phase 2: Detect disconnected devices ---
            var disconnectedIds = new List<int>();

            foreach (int sdlId in _openedSdlInstanceIds)
            {
                // Find the UserDevice with this SDL instance ID.
                UserDevice ud = FindOnlineDeviceBySdlInstanceId(sdlId);
                if (ud == null)
                {
                    disconnectedIds.Add(sdlId);
                    continue;
                }

                // Check if the device is still attached.
                if (ud.Device == null || !ud.Device.IsAttached)
                {
                    MarkDeviceOffline(ud);
                    disconnectedIds.Add(sdlId);
                    changed = true;
                }
            }

            // Clean up tracking for disconnected devices.
            foreach (int sdlId in disconnectedIds)
            {
                _openedSdlInstanceIds.Remove(sdlId);
                _sdlInstanceToDeviceIndex.Remove(sdlId);
            }

            // --- Phase 3: Handle native XInput devices ---
            // Enumerate XInput controller slots 0–3.
            // These are handled separately from SDL devices.
            UpdateXInputDevices(ref changed);

            // --- Notify if anything changed ---
            if (changed)
            {
                DevicesUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        // ─────────────────────────────────────────────
        //  XInput native device enumeration
        // ─────────────────────────────────────────────

        /// <summary>
        /// Well-known instance GUIDs for native XInput controllers (slots 0–3).
        /// These are deterministic so that settings can persist across sessions.
        /// Format: "XINPUT0-0000-0000-0000-000000000001" through "...0004"
        /// </summary>
        private static readonly Guid[] XInputInstanceGuids = new Guid[]
        {
            new Guid("58494E50-5554-3000-0000-000000000001"), // XINPUT0
            new Guid("58494E50-5554-3100-0000-000000000002"), // XINPUT1
            new Guid("58494E50-5554-3200-0000-000000000003"), // XINPUT2
            new Guid("58494E50-5554-3300-0000-000000000004"), // XINPUT3
        };

        /// <summary>
        /// Checks each XInput slot (0–3) for a connected native Xbox controller.
        /// Creates/updates UserDevice records for connected controllers.
        /// Skips slots occupied by our own ViGEm virtual controllers to prevent
        /// loopback (reading our own output back as input).
        /// </summary>
        private void UpdateXInputDevices(ref bool changed)
        {
            // Snapshot the set of XInput slots occupied by our ViGEm virtual controllers.
            HashSet<int> vigemSlots;
            lock (_vigemOccupiedXInputSlots)
            {
                vigemSlots = new HashSet<int>(_vigemOccupiedXInputSlots);
            }

            for (int i = 0; i < MaxPads; i++)
            {
                try
                {
                    // Skip XInput slots that are occupied by our own ViGEm virtual
                    // controllers — reading those would create a feedback loop.
                    if (vigemSlots.Contains(i))
                    {
                        // If we previously had a native XInput device at this slot,
                        // mark it offline since it's now shadowed by our virtual controller.
                        Guid shadowedGuid = XInputInstanceGuids[i];
                        UserDevice shadowedUd = FindOnlineDeviceByInstanceGuid(shadowedGuid);
                        if (shadowedUd != null && shadowedUd.IsOnline && shadowedUd.IsXInput)
                        {
                            MarkDeviceOffline(shadowedUd);
                            changed = true;
                        }
                        continue;
                    }

                    bool connected = XInputInterop.IsControllerConnected(i);
                    Guid instanceGuid = XInputInstanceGuids[i];
                    UserDevice ud = FindOnlineDeviceByInstanceGuid(instanceGuid);

                    if (connected)
                    {
                        if (ud == null || !ud.IsOnline)
                        {
                            // Controller just connected.
                            ud = FindOrCreateUserDevice(instanceGuid);
                            ud.LoadInstance(
                                instanceGuid,
                                $"XInput Controller {i + 1}",
                                SdlDeviceWrapper.BuildXInputProductGuid(0x045E, 0x028E), // Generic Xbox 360
                                $"XInput Controller {i + 1}");
                            ud.LoadCapabilities(6, 16, 1,
                                InputDeviceType.Gamepad, 1, 0);
                            ud.VendorId = 0x045E;
                            ud.ProdId = 0x028E;
                            ud.IsOnline = true;
                            ud.IsXInput = true;
                            ud.XInputUserIndex = i;
                            ud.ForceFeedbackState = new ForceFeedbackState();

                            // Build device objects for XInput.
                            ud.DeviceObjects = BuildXInputDeviceObjects();
                            ud.DeviceEffects = new[] { DeviceEffectItem.CreateRumbleEffect() };

                            changed = true;
                        }
                    }
                    else
                    {
                        if (ud != null && ud.IsOnline)
                        {
                            // Controller disconnected.
                            ud.ClearRuntimeState();
                            changed = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    RaiseError($"Error checking XInput slot {i}", ex);
                }
            }
        }

        /// <summary>
        /// Builds a DeviceObjectItem array for an XInput controller
        /// (6 axes, 1 POV, 16 buttons — standard Xbox layout).
        /// </summary>
        private static DeviceObjectItem[] BuildXInputDeviceObjects()
        {
            var items = new List<DeviceObjectItem>();

            // Axes: LX, LY, RX, RY, LT, RT
            string[] axisNames = { "Left Stick X", "Left Stick Y", "Right Stick X", "Right Stick Y", "Left Trigger", "Right Trigger" };
            Guid[] axisGuids = { ObjectGuid.XAxis, ObjectGuid.YAxis, ObjectGuid.RxAxis, ObjectGuid.RyAxis, ObjectGuid.ZAxis, ObjectGuid.RzAxis };

            for (int i = 0; i < 6; i++)
            {
                items.Add(new DeviceObjectItem
                {
                    InputIndex = i,
                    Name = axisNames[i],
                    ObjectTypeGuid = axisGuids[i],
                    ObjectType = DeviceObjectTypeFlags.AbsoluteAxis,
                    Offset = i * 4,
                    Aspect = ObjectAspect.Position
                });
            }

            // POV (D-pad)
            items.Add(new DeviceObjectItem
            {
                InputIndex = 0,
                Name = "D-Pad",
                ObjectTypeGuid = ObjectGuid.PovController,
                ObjectType = DeviceObjectTypeFlags.PointOfViewController,
                Offset = 6 * 4,
                Aspect = ObjectAspect.Position
            });

            // Buttons: A, B, X, Y, LB, RB, Back, Start, LS, RS, Guide, Share (+ 4 reserved)
            string[] btnNames = {
                "A", "B", "X", "Y", "LB", "RB", "Back", "Start",
                "Left Stick", "Right Stick", "Guide", "Share",
                "Reserved 12", "Reserved 13", "Reserved 14", "Reserved 15"
            };

            for (int i = 0; i < 16; i++)
            {
                items.Add(new DeviceObjectItem
                {
                    InputIndex = i,
                    Name = btnNames[i],
                    ObjectTypeGuid = ObjectGuid.Button,
                    ObjectType = DeviceObjectTypeFlags.PushButton,
                    Offset = (7 + i) * 4,
                    Aspect = ObjectAspect.Position
                });
            }

            return items.ToArray();
        }

        // ─────────────────────────────────────────────
        //  XInput device detection for SDL devices
        // ─────────────────────────────────────────────

        /// <summary>
        /// Known Xbox controller Product IDs (VID = 0x045E, Microsoft).
        /// These devices are handled natively via XInputInterop and should
        /// not also be opened through SDL to avoid double-counting.
        /// </summary>
        private static readonly HashSet<ushort> KnownXboxPids = new HashSet<ushort>
        {
            0x028E, // Xbox 360 Controller
            0x028F, // Xbox 360 Wireless Controller
            0x0291, // Xbox 360 Wireless Adapter
            0x02A1, // Xbox 360 Wireless Controller (3rd party)
            0x02D1, // Xbox One Controller (2013)
            0x02DD, // Xbox One Controller (S)
            0x02E3, // Xbox One Elite Controller
            0x02EA, // Xbox One S Controller
            0x02FD, // Xbox One S Controller (Bluetooth)
            0x02FF, // Xbox One Controller
            0x0B00, // Xbox One Elite Series 2
            0x0B05, // Xbox One Elite Series 2 (Bluetooth)
            0x0B12, // Xbox Series X|S Controller (USB)
            0x0B13, // Xbox Series X|S Controller (Bluetooth)
            0x0B20, // Xbox Series Controller (2023 revision)
            0x0B21, // Xbox Elite Series 2 (USB, 2023)
            0x0B22, // Xbox Elite Series 2 (Bluetooth, 2023)
        };

        /// <summary>
        /// Checks whether an SDL device is a native XInput controller that should
        /// be handled via XInputInterop instead of SDL. Uses VID/PID matching
        /// against known Xbox controller identifiers.
        /// 
        /// Note: The previous PIDVID-based check was incorrect because our synthetic
        /// product GUIDs always included the PIDVID signature, causing ALL SDL devices
        /// to be filtered out.
        /// </summary>
        private static bool IsNativeXInputDevice(SdlDeviceWrapper wrapper)
        {
            // Microsoft Xbox controllers all use VID 0x045E.
            if (wrapper.VendorId == 0x045E && KnownXboxPids.Contains(wrapper.ProductId))
                return true;

            // Also check the SDL joystick type — SDL can identify game controllers
            // even with XInput disabled in its hints.
            // However, we only filter Microsoft VID to avoid blocking third-party
            // DirectInput gamepads that happen to be game controllers.
            return false;
        }

        // ─────────────────────────────────────────────
        //  UserDevice lookup helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Finds an online UserDevice by its instance GUID.
        /// </summary>
        private UserDevice FindOnlineDeviceByInstanceGuid(Guid instanceGuid)
        {
            var devices = SettingsManager.UserDevices?.Items;
            if (devices == null) return null;

            lock (SettingsManager.UserDevices.SyncRoot)
            {
                return devices.FirstOrDefault(d =>
                    d.InstanceGuid == instanceGuid);
            }
        }

        /// <summary>
        /// Finds an online UserDevice by its SDL instance ID.
        /// </summary>
        private UserDevice FindOnlineDeviceBySdlInstanceId(int sdlInstanceId)
        {
            var devices = SettingsManager.UserDevices?.Items;
            if (devices == null) return null;

            lock (SettingsManager.UserDevices.SyncRoot)
            {
                return devices.FirstOrDefault(d =>
                    d.IsOnline && d.Device != null && d.Device.SdlInstanceId == sdlInstanceId);
            }
        }

        /// <summary>
        /// Finds an existing UserDevice by instance GUID or creates a new one
        /// and adds it to the SettingsManager collection.
        /// </summary>
        private UserDevice FindOrCreateUserDevice(Guid instanceGuid)
        {
            var devices = SettingsManager.UserDevices;
            if (devices == null) return new UserDevice();

            lock (devices.SyncRoot)
            {
                var existing = devices.Items.FirstOrDefault(d => d.InstanceGuid == instanceGuid);
                if (existing != null)
                    return existing;

                var ud = new UserDevice { InstanceGuid = instanceGuid };
                devices.Items.Add(ud);
                return ud;
            }
        }

        /// <summary>
        /// Marks a device as offline, disposes its SDL handle, and clears runtime state.
        /// </summary>
        private void MarkDeviceOffline(UserDevice ud)
        {
            if (ud == null) return;

            // Stop rumble before closing.
            if (ud.ForceFeedbackState != null && ud.Device != null)
            {
                try { ud.ForceFeedbackState.StopDeviceForces(ud.Device); }
                catch { /* best effort */ }
            }

            // Dispose SDL handle.
            if (ud.Device != null)
            {
                try { ud.Device.Dispose(); }
                catch { /* best effort */ }
            }

            ud.ClearRuntimeState();
        }
    }

    /// <summary>
    /// Placeholder for XInputInterop methods referenced by Step 1.
    /// The actual implementation is in Common/XInputInterop.cs.
    /// </summary>
    public static partial class XInputInterop
    {
        /// <summary>
        /// Checks if an XInput controller is connected at the given user index (0–3).
        /// </summary>
        public static partial bool IsControllerConnected(int userIndex);

        /// <summary>
        /// Checks if a product GUID matches the PIDVID pattern used by XInput-over-DirectInput
        /// wrapper devices, which should be handled natively via XInput instead of SDL.
        /// </summary>
        public static partial bool IsXInputDeviceViaProductGuid(Guid productGuid);
    }

    /// <summary>
    /// Placeholder for the SettingsManager's UserDevices collection.
    /// </summary>
    public static partial class SettingsManager
    {
        public static DeviceCollection UserDevices { get; set; }
        public static SettingsCollection UserSettings { get; set; }
    }

    /// <summary>
    /// Thread-safe collection of UserDevice records with a sync root for locking.
    /// </summary>
    public class DeviceCollection
    {
        public List<UserDevice> Items { get; } = new List<UserDevice>();
        public object SyncRoot { get; } = new object();
    }

    /// <summary>
    /// Thread-safe collection of UserSetting records.
    /// </summary>
    public class SettingsCollection
    {
        public List<UserSetting> Items { get; } = new List<UserSetting>();
        public object SyncRoot { get; } = new object();

        /// <summary>
        /// Finds the UserSetting that links a device (by InstanceGuid) to a pad slot.
        /// </summary>
        public UserSetting FindByInstanceGuid(Guid instanceGuid)
        {
            lock (SyncRoot)
            {
                return Items.FirstOrDefault(s => s.InstanceGuid == instanceGuid);
            }
        }

        /// <summary>
        /// Returns all UserSettings assigned to a specific pad slot (0–3).
        /// </summary>
        public List<UserSetting> FindByPadIndex(int padIndex)
        {
            lock (SyncRoot)
            {
                return Items.Where(s => s.MapTo == padIndex).ToList();
            }
        }
    }
}
