using System;
using System.Runtime.InteropServices;
using System.Threading;
using PadForge.Engine;

namespace PadForge.Common.Input
{
    /// <summary>
    /// P/Invoke interop for native XInput (xinput1_4.dll) and HID access
    /// for the Xbox Series controller Share button.
    /// 
    /// Provides:
    ///   - XInput state reading (GetStateEx with undocumented ordinal #100 for Guide button)
    ///   - Vibration control
    ///   - Controller connection detection
    ///   - Conversion from XInput state to <see cref="CustomInputState"/>
    ///   - PIDVID-based XInput device detection
    ///   - HID side-channel polling for the Share button on Xbox Series controllers
    /// </summary>
    public static partial class XInputInterop
    {
        // ─────────────────────────────────────────────
        //  XInput DLL constants
        // ─────────────────────────────────────────────

        private const string XInputDll = "xinput1_4.dll";
        private const uint ERROR_SUCCESS = 0;
        private const uint ERROR_DEVICE_NOT_CONNECTED = 0x048F;

        // ─────────────────────────────────────────────
        //  XInput P/Invoke — State reading
        // ─────────────────────────────────────────────

        /// <summary>
        /// XInputGetState — standard, documented API.
        /// Does NOT report the Guide button.
        /// </summary>
        [DllImport(XInputDll, EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState_Native(
            uint dwUserIndex,
            out XINPUT_STATE pState);

        /// <summary>
        /// XInputGetStateEx — undocumented ordinal #100.
        /// Reports the Guide button in the button flags.
        /// Falls back to XInputGetState if ordinal #100 is unavailable.
        /// </summary>
        [DllImport(XInputDll, EntryPoint = "#100")]
        private static extern uint XInputGetStateEx_Native(
            uint dwUserIndex,
            out XINPUT_STATE pState);

        /// <summary>
        /// Native XINPUT_STATE structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        /// <summary>
        /// Native XINPUT_GAMEPAD structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        // ─────────────────────────────────────────────
        //  XInput P/Invoke — Vibration
        // ─────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_VIBRATION
        {
            public ushort wLeftMotorSpeed;
            public ushort wRightMotorSpeed;
        }

        [DllImport(XInputDll, EntryPoint = "XInputSetState")]
        private static extern uint XInputSetState_Native(
            uint dwUserIndex,
            ref XINPUT_VIBRATION pVibration);

        // ─────────────────────────────────────────────
        //  XInput P/Invoke — Capabilities
        // ─────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_CAPABILITIES
        {
            public byte Type;
            public byte SubType;
            public ushort Flags;
            public XINPUT_GAMEPAD Gamepad;
            public XINPUT_VIBRATION Vibration;
        }

        [DllImport(XInputDll, EntryPoint = "XInputGetCapabilities")]
        private static extern uint XInputGetCapabilities_Native(
            uint dwUserIndex,
            uint dwFlags,
            out XINPUT_CAPABILITIES pCapabilities);

        // ─────────────────────────────────────────────
        //  XInput P/Invoke — Battery
        // ─────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_BATTERY_INFORMATION
        {
            public byte BatteryType;
            public byte BatteryLevel;
        }

        [DllImport(XInputDll, EntryPoint = "XInputGetBatteryInformation")]
        private static extern uint XInputGetBatteryInformation_Native(
            uint dwUserIndex,
            byte devType,
            out XINPUT_BATTERY_INFORMATION pBatteryInformation);

        // ─────────────────────────────────────────────
        //  Managed API — State reading
        // ─────────────────────────────────────────────

        private static bool _useGetStateEx = true;

        /// <summary>
        /// Reads the extended state of an XInput controller.
        /// Uses the undocumented GetStateEx (ordinal #100) to capture the Guide button.
        /// Falls back to standard GetState if GetStateEx fails.
        /// </summary>
        /// <param name="userIndex">XInput user index (0–3).</param>
        /// <param name="state">Output XInputState.</param>
        /// <returns>True if the controller is connected and state was read.</returns>
        public static partial bool GetStateEx(int userIndex, out XInputState state)
        {
            state = default;

            try
            {
                XINPUT_STATE nativeState;
                uint result;

                if (_useGetStateEx)
                {
                    try
                    {
                        result = XInputGetStateEx_Native((uint)userIndex, out nativeState);
                    }
                    catch (EntryPointNotFoundException)
                    {
                        // Ordinal #100 not available — fall back permanently.
                        _useGetStateEx = false;
                        result = XInputGetState_Native((uint)userIndex, out nativeState);
                    }
                }
                else
                {
                    result = XInputGetState_Native((uint)userIndex, out nativeState);
                }

                if (result != ERROR_SUCCESS)
                    return false;

                // Convert native struct to managed struct.
                state.PacketNumber = nativeState.dwPacketNumber;
                state.Gamepad.Buttons = nativeState.Gamepad.wButtons;
                state.Gamepad.LeftTrigger = nativeState.Gamepad.bLeftTrigger;
                state.Gamepad.RightTrigger = nativeState.Gamepad.bRightTrigger;
                state.Gamepad.ThumbLX = nativeState.Gamepad.sThumbLX;
                state.Gamepad.ThumbLY = nativeState.Gamepad.sThumbLY;
                state.Gamepad.ThumbRX = nativeState.Gamepad.sThumbRX;
                state.Gamepad.ThumbRY = nativeState.Gamepad.sThumbRY;

                // Poll the Share button via HID side-channel for Xbox Series controllers.
                if (GetShareButtonState(userIndex))
                {
                    state.Gamepad.Buttons |= Gamepad.SHARE;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────────────────────────
        //  Managed API — Vibration
        // ─────────────────────────────────────────────

        /// <summary>
        /// Sets vibration on a native XInput controller.
        /// </summary>
        /// <param name="userIndex">XInput user index (0–3).</param>
        /// <param name="leftMotor">Left (low-frequency) motor speed (0–65535).</param>
        /// <param name="rightMotor">Right (high-frequency) motor speed (0–65535).</param>
        public static partial void SetVibration(int userIndex, ushort leftMotor, ushort rightMotor)
        {
            try
            {
                var vibration = new XINPUT_VIBRATION
                {
                    wLeftMotorSpeed = leftMotor,
                    wRightMotorSpeed = rightMotor
                };
                XInputSetState_Native((uint)userIndex, ref vibration);
            }
            catch { /* best effort */ }
        }

        // ─────────────────────────────────────────────
        //  Managed API — Connection check
        // ─────────────────────────────────────────────

        /// <summary>
        /// Checks if an XInput controller is connected at the given user index.
        /// Uses GetCapabilities for efficiency (doesn't allocate state struct).
        /// </summary>
        public static partial bool IsControllerConnected(int userIndex)
        {
            try
            {
                uint result = XInputGetCapabilities_Native(
                    (uint)userIndex,
                    1, // XINPUT_FLAG_GAMEPAD
                    out _);
                return result == ERROR_SUCCESS;
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────────────────────────
        //  Managed API — State conversion
        // ─────────────────────────────────────────────

        /// <summary>
        /// Converts an XInput state to a <see cref="CustomInputState"/> using unsigned
        /// axis conventions (0–65535) for compatibility with the mapping pipeline.
        /// 
        /// XInput axis layout → CustomInputState.Axis[] mapping:
        ///   [0] Left Stick X   (ThumbLX: signed → unsigned)
        ///   [1] Left Stick Y   (ThumbLY: signed → unsigned, Y inverted)
        ///   [2] Right Stick X  (ThumbRX: signed → unsigned)
        ///   [3] Right Stick Y  (ThumbRY: signed → unsigned, Y inverted)
        ///   [4] Left Trigger   (0–255 → 0–65535)
        ///   [5] Right Trigger  (0–255 → 0–65535)
        /// 
        /// XInput buttons → CustomInputState.Buttons[]:
        ///   Buttons are mapped by bit position to sequential indices.
        /// 
        /// D-pad → CustomInputState.Povs[0]:
        ///   Converted from button flags to centidegrees.
        /// </summary>
        public static partial CustomInputState ConvertToInputState(XInputState xiState)
        {
            var state = new CustomInputState();
            var gp = xiState.Gamepad;

            // ── Axes: signed → unsigned ──
            // XInput thumb range: -32768 to 32767
            // CustomInputState range: 0 to 65535 (center = 32767/32768)
            //
            // X axes: straightforward signed→unsigned offset.
            //   -32768 → 0, 0 → 32768, 32767 → 65535
            state.Axis[0] = (ushort)(gp.ThumbLX - short.MinValue);   // Left Stick X
            state.Axis[2] = (ushort)(gp.ThumbRX - short.MinValue);   // Right Stick X

            // Y axes: XInput convention is +Y = up, but our unsigned convention
            // uses 0 = up, 65535 = down. We invert via (MaxValue - value) which
            // avoids the integer overflow that occurs when negating short.MinValue.
            //   32767 (up)    → 0
            //   0     (center)→ 32767
            //  -32768 (down)  → 65535
            state.Axis[1] = (ushort)(short.MaxValue - gp.ThumbLY);   // Left Stick Y (inverted)
            state.Axis[3] = (ushort)(short.MaxValue - gp.ThumbRY);   // Right Stick Y (inverted)

            // Triggers: 0–255 → 0–65535
            state.Axis[4] = (int)(gp.LeftTrigger / 255.0 * 65535.0);
            state.Axis[5] = (int)(gp.RightTrigger / 255.0 * 65535.0);

            // ── D-pad → POV ──
            state.Povs[0] = DpadButtonsToCentidegrees(gp.Buttons);

            // ── Buttons ──
            // Map XInput button flags to sequential button indices.
            // Bit 0 (DPAD_UP) through bit 15 (Y).
            for (int bit = 0; bit < 16; bit++)
            {
                if ((gp.Buttons & (1 << bit)) != 0)
                {
                    state.Buttons[bit] = true;
                }
            }

            return state;
        }

        /// <summary>
        /// Converts XInput D-pad button flags to centidegrees POV value.
        /// </summary>
        private static int DpadButtonsToCentidegrees(ushort buttons)
        {
            bool up = (buttons & Gamepad.DPAD_UP) != 0;
            bool down = (buttons & Gamepad.DPAD_DOWN) != 0;
            bool left = (buttons & Gamepad.DPAD_LEFT) != 0;
            bool right = (buttons & Gamepad.DPAD_RIGHT) != 0;

            if (up && right) return 4500;
            if (up && left) return 31500;
            if (down && right) return 13500;
            if (down && left) return 22500;
            if (up) return 0;
            if (right) return 9000;
            if (down) return 18000;
            if (left) return 27000;

            return -1; // Centered
        }

        // ─────────────────────────────────────────────
        //  PIDVID-based XInput device detection
        // ─────────────────────────────────────────────

        /// <summary>
        /// Checks if a product GUID contains the "PIDVID" signature at bytes 10–15,
        /// which indicates the device is an XInput controller exposed through
        /// DirectInput's XInput compatibility layer. Such devices should be handled
        /// via the native XInput API rather than SDL for best compatibility.
        /// </summary>
        /// <param name="productGuid">The product GUID to check.</param>
        /// <returns>True if the GUID has the PIDVID signature.</returns>
        public static partial bool IsXInputDeviceViaProductGuid(Guid productGuid)
        {
            byte[] bytes = productGuid.ToByteArray();
            if (bytes.Length < 16)
                return false;

            // Check for ASCII "PIDVID" at bytes 10–15.
            return bytes[10] == 0x50  // P
                && bytes[11] == 0x49  // I
                && bytes[12] == 0x44  // D
                && bytes[13] == 0x56  // V
                && bytes[14] == 0x49  // I
                && bytes[15] == 0x44; // D
        }

        // ─────────────────────────────────────────────
        //  Share button — HID side-channel polling
        //  Xbox Series controllers expose the Share button
        //  via a HID feature report, not through XInput.
        //  We poll HID devices matching the Xbox Series VID/PID
        //  to detect the Share button state.
        // ─────────────────────────────────────────────

        // Xbox Series controller identification
        private const ushort XBOX_SERIES_VID = 0x045E;
        private static readonly ushort[] XBOX_SERIES_PIDS = new ushort[]
        {
            0x0B12, // Xbox Series X|S (USB)
            0x0B13, // Xbox Series X|S (Bluetooth)
            0x0B20, // Xbox Series (2023 revision)
            0x0B21, // Xbox Elite Series 2 (USB)
            0x0B22, // Xbox Elite Series 2 (Bluetooth)
        };

        /// <summary>
        /// Per-controller Share button state, set by background HID polling.
        /// </summary>
        private static readonly bool[] _shareButtonStates = new bool[4];

        /// <summary>
        /// Background threads for HID polling (one per controller slot).
        /// </summary>
        private static readonly Thread[] _hidPollingThreads = new Thread[4];
        private static readonly bool[] _hidPollingActive = new bool[4];

        // ── HID P/Invoke declarations ──

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        [DllImport("hid.dll", SetLastError = true)]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetAttributes(IntPtr hidDeviceObject,
            ref HIDD_ATTRIBUTES attributes);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetFeature(IntPtr hidDeviceObject,
            byte[] reportBuffer, uint reportBufferLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDD_ATTRIBUTES
        {
            public uint Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            IntPtr enumerator,
            IntPtr hwndParent,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            uint memberIndex,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize,
            out uint requiredSize,
            IntPtr deviceInfoData);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public IntPtr Reserved;
        }

        private const uint DIGCF_PRESENT = 0x02;
        private const uint DIGCF_DEVICEINTERFACE = 0x10;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        /// <summary>
        /// Gets the Share button state for the given XInput controller index.
        /// Returns the last polled state from the HID background thread.
        /// </summary>
        public static bool GetShareButtonState(int userIndex)
        {
            if (userIndex < 0 || userIndex >= 4)
                return false;

            return _shareButtonStates[userIndex];
        }

        /// <summary>
        /// Starts background HID polling for the Share button on a controller slot.
        /// Should be called when a native XInput Xbox Series controller is detected.
        /// </summary>
        public static void StartShareButtonPolling(int userIndex)
        {
            if (userIndex < 0 || userIndex >= 4)
                return;

            if (_hidPollingActive[userIndex])
                return;

            _hidPollingActive[userIndex] = true;
            _hidPollingThreads[userIndex] = new Thread(() => ShareButtonPollingLoop(userIndex))
            {
                Name = $"PadForge.ShareButton.{userIndex}",
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            _hidPollingThreads[userIndex].Start();
        }

        /// <summary>
        /// Stops Share button polling for a controller slot.
        /// </summary>
        public static void StopShareButtonPolling(int userIndex)
        {
            if (userIndex < 0 || userIndex >= 4)
                return;

            _hidPollingActive[userIndex] = false;
            _shareButtonStates[userIndex] = false;

            // Thread will exit on its own due to _hidPollingActive flag.
        }

        /// <summary>
        /// Background polling loop for the Share button via HID feature reports.
        /// Enumerates HID devices to find Xbox Series controllers, then reads
        /// feature report 0x01 to check the Share button bit.
        /// </summary>
        private static void ShareButtonPollingLoop(int userIndex)
        {
            while (_hidPollingActive[userIndex])
            {
                try
                {
                    bool sharePressed = PollShareButtonFromHid(userIndex);
                    _shareButtonStates[userIndex] = sharePressed;
                }
                catch
                {
                    _shareButtonStates[userIndex] = false;
                }

                // Poll at ~60Hz.
                Thread.Sleep(16);
            }
        }

        /// <summary>
        /// Enumerates HID devices, finds Xbox Series controllers, and reads
        /// the Share button state from the HID feature report.
        /// </summary>
        private static bool PollShareButtonFromHid(int userIndex)
        {
            HidD_GetHidGuid(out Guid hidGuid);

            IntPtr deviceInfoSet = SetupDiGetClassDevs(
                ref hidGuid,
                IntPtr.Zero,
                IntPtr.Zero,
                DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == INVALID_HANDLE_VALUE)
                return false;

            try
            {
                uint memberIndex = 0;
                int xboxControllerCount = 0;

                var interfaceData = new SP_DEVICE_INTERFACE_DATA();
                interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);

                while (SetupDiEnumDeviceInterfaces(
                    deviceInfoSet,
                    IntPtr.Zero,
                    ref hidGuid,
                    memberIndex,
                    ref interfaceData))
                {
                    memberIndex++;

                    // Get device path.
                    string devicePath = GetDevicePath(deviceInfoSet, ref interfaceData);
                    if (string.IsNullOrEmpty(devicePath))
                        continue;

                    // Open the HID device.
                    IntPtr handle = CreateFile(
                        devicePath,
                        GENERIC_READ | GENERIC_WRITE,
                        FILE_SHARE_READ | FILE_SHARE_WRITE,
                        IntPtr.Zero,
                        OPEN_EXISTING,
                        0,
                        IntPtr.Zero);

                    if (handle == INVALID_HANDLE_VALUE)
                        continue;

                    try
                    {
                        // Check if this is an Xbox Series controller.
                        var attrs = new HIDD_ATTRIBUTES();
                        attrs.Size = (uint)Marshal.SizeOf(attrs);

                        if (!HidD_GetAttributes(handle, ref attrs))
                            continue;

                        if (attrs.VendorID != XBOX_SERIES_VID)
                            continue;

                        bool isXboxSeries = false;
                        foreach (ushort pid in XBOX_SERIES_PIDS)
                        {
                            if (attrs.ProductID == pid)
                            {
                                isXboxSeries = true;
                                break;
                            }
                        }

                        if (!isXboxSeries)
                            continue;

                        // Match controller to XInput slot by index.
                        // The Nth Xbox Series HID device corresponds to XInput slot N.
                        if (xboxControllerCount != userIndex)
                        {
                            xboxControllerCount++;
                            continue;
                        }

                        // Read feature report 0x01 to get the Share button state.
                        byte[] report = new byte[64];
                        report[0] = 0x01; // Report ID

                        if (HidD_GetFeature(handle, report, (uint)report.Length))
                        {
                            // Share button is typically at byte offset 3, bit 0.
                            // This varies by firmware; the most common layout is checked.
                            return (report[3] & 0x01) != 0;
                        }

                        return false;
                    }
                    finally
                    {
                        CloseHandle(handle);
                    }
                }

                return false;
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }

        /// <summary>
        /// Gets the device path for a HID device interface.
        /// </summary>
        private static string GetDevicePath(IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA interfaceData)
        {
            // First call: get required size.
            SetupDiGetDeviceInterfaceDetail(
                deviceInfoSet,
                ref interfaceData,
                IntPtr.Zero,
                0,
                out uint requiredSize,
                IntPtr.Zero);

            if (requiredSize == 0)
                return null;

            // Allocate buffer. The detail structure has a 4-byte cbSize header
            // followed by the device path string.
            IntPtr detailData = Marshal.AllocHGlobal((int)requiredSize);
            try
            {
                // Set cbSize: 8 on x64, 6 on x86 (4 + sizeof(TCHAR) aligned).
                int cbSize = IntPtr.Size == 8 ? 8 : 6;
                Marshal.WriteInt32(detailData, cbSize);

                if (!SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet,
                    ref interfaceData,
                    detailData,
                    requiredSize,
                    out _,
                    IntPtr.Zero))
                {
                    return null;
                }

                // Device path starts at offset 4.
                return Marshal.PtrToStringUni(detailData + 4);
            }
            finally
            {
                Marshal.FreeHGlobal(detailData);
            }
        }

        // ─────────────────────────────────────────────
        //  Battery information (optional, for UI display)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Reads the battery information for an XInput controller.
        /// </summary>
        /// <param name="userIndex">XInput user index (0–3).</param>
        /// <param name="batteryType">Output: battery type (0=Disconnected, 1=Wired, 2=Alkaline, 3=NiMH).</param>
        /// <param name="batteryLevel">Output: battery level (0=Empty, 1=Low, 2=Medium, 3=Full).</param>
        /// <returns>True if battery info was read successfully.</returns>
        public static bool GetBatteryInformation(int userIndex, out byte batteryType, out byte batteryLevel)
        {
            batteryType = 0;
            batteryLevel = 0;

            try
            {
                uint result = XInputGetBatteryInformation_Native(
                    (uint)userIndex,
                    0, // BATTERY_DEVTYPE_GAMEPAD
                    out XINPUT_BATTERY_INFORMATION info);

                if (result != ERROR_SUCCESS)
                    return false;

                batteryType = info.BatteryType;
                batteryLevel = info.BatteryLevel;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────────────────────────
        //  Diagnostic helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns the name of the XInput DLL being used.
        /// </summary>
        public static string GetLoadedLibraryName()
        {
            return XInputDll;
        }
    }
}
