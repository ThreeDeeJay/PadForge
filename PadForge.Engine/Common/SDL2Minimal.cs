using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    /// <summary>
    /// Minimal SDL2 P/Invoke declarations for joystick and game controller support.
    /// Only the functions actually used by PadForge are declared here.
    /// String-returning functions use IntPtr + Marshal.PtrToStringAnsi pattern
    /// because SDL owns the returned string memory.
    /// </summary>
    public static class SDL
    {
        private const string lib = "SDL2";

        // ─────────────────────────────────────────────
        //  Init flags
        // ─────────────────────────────────────────────

        public const uint SDL_INIT_JOYSTICK = 0x00000200;
        public const uint SDL_INIT_GAMECONTROLLER = 0x00002000;

        // ─────────────────────────────────────────────
        //  Hat constants
        // ─────────────────────────────────────────────

        public const byte SDL_HAT_CENTERED = 0x00;
        public const byte SDL_HAT_UP = 0x01;
        public const byte SDL_HAT_RIGHT = 0x02;
        public const byte SDL_HAT_DOWN = 0x04;
        public const byte SDL_HAT_LEFT = 0x08;
        public const byte SDL_HAT_RIGHTUP = SDL_HAT_RIGHT | SDL_HAT_UP;
        public const byte SDL_HAT_RIGHTDOWN = SDL_HAT_RIGHT | SDL_HAT_DOWN;
        public const byte SDL_HAT_LEFTUP = SDL_HAT_LEFT | SDL_HAT_UP;
        public const byte SDL_HAT_LEFTDOWN = SDL_HAT_LEFT | SDL_HAT_DOWN;

        // ─────────────────────────────────────────────
        //  Hint strings
        // ─────────────────────────────────────────────

        public const string SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS = "SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS";
        public const string SDL_HINT_JOYSTICK_RAWINPUT = "SDL_JOYSTICK_RAWINPUT";
        public const string SDL_HINT_XINPUT_ENABLED = "SDL_XINPUT_ENABLED";

        // ─────────────────────────────────────────────
        //  Enums
        // ─────────────────────────────────────────────

        public enum SDL_bool : int
        {
            SDL_FALSE = 0,
            SDL_TRUE = 1
        }

        public enum SDL_JoystickType : int
        {
            SDL_JOYSTICK_TYPE_UNKNOWN = 0,
            SDL_JOYSTICK_TYPE_GAMECONTROLLER = 1,
            SDL_JOYSTICK_TYPE_WHEEL = 2,
            SDL_JOYSTICK_TYPE_ARCADE_STICK = 3,
            SDL_JOYSTICK_TYPE_FLIGHT_STICK = 4,
            SDL_JOYSTICK_TYPE_DANCE_PAD = 5,
            SDL_JOYSTICK_TYPE_GUITAR = 6,
            SDL_JOYSTICK_TYPE_DRUM_KIT = 7,
            SDL_JOYSTICK_TYPE_ARCADE_PAD = 8,
            SDL_JOYSTICK_TYPE_THROTTLE = 9
        }

        public enum SDL_JoystickPowerLevel : int
        {
            SDL_JOYSTICK_POWER_UNKNOWN = -1,
            SDL_JOYSTICK_POWER_EMPTY = 0,
            SDL_JOYSTICK_POWER_LOW = 1,
            SDL_JOYSTICK_POWER_MEDIUM = 2,
            SDL_JOYSTICK_POWER_FULL = 3,
            SDL_JOYSTICK_POWER_WIRED = 4,
            SDL_JOYSTICK_POWER_MAX = 5
        }

        // ─────────────────────────────────────────────
        //  Structs
        // ─────────────────────────────────────────────

        /// <summary>
        /// 16-byte GUID structure used by SDL for joystick identification.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SDL_JoystickGUID
        {
            public byte data0;
            public byte data1;
            public byte data2;
            public byte data3;
            public byte data4;
            public byte data5;
            public byte data6;
            public byte data7;
            public byte data8;
            public byte data9;
            public byte data10;
            public byte data11;
            public byte data12;
            public byte data13;
            public byte data14;
            public byte data15;

            /// <summary>
            /// Converts the SDL GUID to a .NET <see cref="System.Guid"/>.
            /// </summary>
            public Guid ToGuid()
            {
                return new Guid(
                    (int)(data0 | (data1 << 8) | (data2 << 16) | (data3 << 24)),
                    (short)(data4 | (data5 << 8)),
                    (short)(data6 | (data7 << 8)),
                    data8, data9, data10, data11,
                    data12, data13, data14, data15);
            }

            /// <summary>
            /// Converts the raw 16 bytes to a byte array.
            /// </summary>
            public byte[] ToByteArray()
            {
                return new byte[]
                {
                    data0, data1, data2, data3,
                    data4, data5, data6, data7,
                    data8, data9, data10, data11,
                    data12, data13, data14, data15
                };
            }
        }

        // ─────────────────────────────────────────────
        //  Core lifecycle
        // ─────────────────────────────────────────────

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_Init(uint flags);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_Quit();

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetError")]
        private static extern IntPtr _SDL_GetError();

        public static string SDL_GetError()
        {
            return Marshal.PtrToStringAnsi(_SDL_GetError()) ?? string.Empty;
        }

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern SDL_bool SDL_SetHint(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string value);

        // ─────────────────────────────────────────────
        //  Joystick enumeration (by device index)
        // ─────────────────────────────────────────────

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_NumJoysticks();

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern SDL_JoystickGUID SDL_JoystickGetDeviceGUID(int device_index);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort SDL_JoystickGetDeviceVendor(int device_index);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort SDL_JoystickGetDeviceProduct(int device_index);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort SDL_JoystickGetDeviceProductVersion(int device_index);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern SDL_JoystickType SDL_JoystickGetDeviceType(int device_index);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickNameForIndex")]
        private static extern IntPtr _SDL_JoystickNameForIndex(int device_index);

        public static string SDL_JoystickNameForIndex(int device_index)
        {
            return Marshal.PtrToStringAnsi(_SDL_JoystickNameForIndex(device_index)) ?? string.Empty;
        }

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickPathForIndex")]
        private static extern IntPtr _SDL_JoystickPathForIndex(int device_index);

        public static string SDL_JoystickPathForIndex(int device_index)
        {
            IntPtr ptr = _SDL_JoystickPathForIndex(device_index);
            return ptr != IntPtr.Zero ? (Marshal.PtrToStringAnsi(ptr) ?? string.Empty) : string.Empty;
        }

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern SDL_bool SDL_IsGameController(int joystick_index);

        // ─────────────────────────────────────────────
        //  Joystick instance (opened device)
        // ─────────────────────────────────────────────

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_JoystickOpen(int device_index);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_JoystickClose(IntPtr joystick);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_JoystickInstanceID(IntPtr joystick);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern SDL_bool SDL_JoystickGetAttached(IntPtr joystick);

        // ─────────────────────────────────────────────
        //  Game controller (higher-level API)
        // ─────────────────────────────────────────────

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_GameControllerOpen(int joystick_index);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_GameControllerClose(IntPtr gamecontroller);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_GameControllerGetJoystick(IntPtr gamecontroller);

        // ─────────────────────────────────────────────
        //  Joystick state polling
        // ─────────────────────────────────────────────

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_JoystickUpdate();

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern short SDL_JoystickGetAxis(IntPtr joystick, int axis);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern byte SDL_JoystickGetButton(IntPtr joystick, int button);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern byte SDL_JoystickGetHat(IntPtr joystick, int hat);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_JoystickNumAxes(IntPtr joystick);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_JoystickNumButtons(IntPtr joystick);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_JoystickNumHats(IntPtr joystick);

        // ─────────────────────────────────────────────
        //  Joystick properties (from opened instance)
        // ─────────────────────────────────────────────

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickName")]
        private static extern IntPtr _SDL_JoystickName(IntPtr joystick);

        public static string SDL_JoystickName(IntPtr joystick)
        {
            return Marshal.PtrToStringAnsi(_SDL_JoystickName(joystick)) ?? string.Empty;
        }

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort SDL_JoystickGetVendor(IntPtr joystick);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort SDL_JoystickGetProduct(IntPtr joystick);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort SDL_JoystickGetProductVersion(IntPtr joystick);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern SDL_JoystickType SDL_JoystickGetType(IntPtr joystick);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickPath")]
        private static extern IntPtr _SDL_JoystickPath(IntPtr joystick);

        public static string SDL_JoystickPath(IntPtr joystick)
        {
            IntPtr ptr = _SDL_JoystickPath(joystick);
            return ptr != IntPtr.Zero ? (Marshal.PtrToStringAnsi(ptr) ?? string.Empty) : string.Empty;
        }

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern SDL_JoystickGUID SDL_JoystickGetGUID(IntPtr joystick);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern SDL_JoystickPowerLevel SDL_JoystickCurrentPowerLevel(IntPtr joystick);

        // ─────────────────────────────────────────────
        //  Rumble / haptics
        // ─────────────────────────────────────────────

        /// <summary>
        /// Rumble a joystick for a specified duration.
        /// </summary>
        /// <param name="joystick">Opened joystick handle.</param>
        /// <param name="low_frequency_rumble">Low-frequency motor intensity (0–65535).</param>
        /// <param name="high_frequency_rumble">High-frequency motor intensity (0–65535).</param>
        /// <param name="duration_ms">Duration in milliseconds (0 to stop).</param>
        /// <returns>0 on success, -1 on error.</returns>
        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_JoystickRumble(
            IntPtr joystick,
            ushort low_frequency_rumble,
            ushort high_frequency_rumble,
            uint duration_ms);

        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern SDL_bool SDL_JoystickHasRumble(IntPtr joystick);

        // ─────────────────────────────────────────────
        //  Version
        // ─────────────────────────────────────────────

        /// <summary>
        /// Gets the version of the SDL library that is linked against.
        /// </summary>
        [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_GetVersion(out SDL_version ver);

        /// <summary>SDL version structure.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SDL_version
        {
            public byte major;
            public byte minor;
            public byte patch;
        }

        /// <summary>
        /// Convenience: returns the linked SDL version.
        /// </summary>
        public static SDL_version SDL_Linked_Version()
        {
            SDL_GetVersion(out SDL_version v);
            return v;
        }
    }
}
