using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PadForge.Common.Input;
using PadForge.Engine;

namespace PadForge.ViewModels
{
    /// <summary>
    /// ViewModel for a single virtual controller slot (one of 4 pads).
    /// Contains the current mapped XInput output state, the list of mapping
    /// rows, force feedback settings, and properties for the controller visualizer.
    /// 
    /// Updated at 30Hz from <see cref="Services.InputService"/> with the combined
    /// gamepad state from the engine.
    /// </summary>
    public partial class PadViewModel : ViewModelBase
    {
        public PadViewModel(int padIndex)
        {
            PadIndex = padIndex;
            Title = $"Controller {padIndex + 1}";
            SlotLabel = $"Player {padIndex + 1}";

            // Initialize default mapping rows for all Xbox controller inputs.
            InitializeDefaultMappings();
        }

        /// <summary>Zero-based pad slot index (0–3).</summary>
        public int PadIndex { get; }

        /// <summary>Display label (e.g., "Player 1").</summary>
        public string SlotLabel { get; }

        // ─────────────────────────────────────────────
        //  Mapped device info
        // ─────────────────────────────────────────────

        /// <summary>
        /// Info about a single physical device mapped to this virtual controller slot.
        /// </summary>
        public class MappedDeviceInfo : ObservableObject
        {
            private string _name = "Unknown";
            private Guid _instanceGuid;
            private bool _isOnline;

            public string Name
            {
                get => _name;
                set => SetProperty(ref _name, value);
            }

            public Guid InstanceGuid
            {
                get => _instanceGuid;
                set => SetProperty(ref _instanceGuid, value);
            }

            public bool IsOnline
            {
                get => _isOnline;
                set => SetProperty(ref _isOnline, value);
            }
        }

        /// <summary>All physical devices currently mapped to this slot.</summary>
        public ObservableCollection<MappedDeviceInfo> MappedDevices { get; } = new();

        private string _mappedDeviceName = "No device mapped";

        /// <summary>
        /// Summary name of mapped devices. Shows all device names joined by " + ",
        /// or "No device mapped" if empty.
        /// </summary>
        public string MappedDeviceName
        {
            get => _mappedDeviceName;
            set => SetProperty(ref _mappedDeviceName, value);
        }

        private Guid _mappedDeviceGuid;

        /// <summary>Instance GUID of the primary mapped device (first in the list).</summary>
        public Guid MappedDeviceGuid
        {
            get => _mappedDeviceGuid;
            set => SetProperty(ref _mappedDeviceGuid, value);
        }

        private bool _isDeviceOnline;

        /// <summary>Whether any mapped device is currently connected.</summary>
        public bool IsDeviceOnline
        {
            get => _isDeviceOnline;
            set => SetProperty(ref _isDeviceOnline, value);
        }

        // ─────────────────────────────────────────────
        //  XInput output state (for visualizer)
        //  These are the FINAL mapped values sent to the virtual controller.
        //  Updated at 30Hz from InputService.
        // ─────────────────────────────────────────────

        // Buttons (bool properties for data binding)
        private bool _buttonA;
        public bool ButtonA { get => _buttonA; set => SetProperty(ref _buttonA, value); }

        private bool _buttonB;
        public bool ButtonB { get => _buttonB; set => SetProperty(ref _buttonB, value); }

        private bool _buttonX;
        public bool ButtonX { get => _buttonX; set => SetProperty(ref _buttonX, value); }

        private bool _buttonY;
        public bool ButtonY { get => _buttonY; set => SetProperty(ref _buttonY, value); }

        private bool _leftShoulder;
        public bool LeftShoulder { get => _leftShoulder; set => SetProperty(ref _leftShoulder, value); }

        private bool _rightShoulder;
        public bool RightShoulder { get => _rightShoulder; set => SetProperty(ref _rightShoulder, value); }

        private bool _buttonBack;
        public bool ButtonBack { get => _buttonBack; set => SetProperty(ref _buttonBack, value); }

        private bool _buttonStart;
        public bool ButtonStart { get => _buttonStart; set => SetProperty(ref _buttonStart, value); }

        private bool _leftThumbButton;
        public bool LeftThumbButton { get => _leftThumbButton; set => SetProperty(ref _leftThumbButton, value); }

        private bool _rightThumbButton;
        public bool RightThumbButton { get => _rightThumbButton; set => SetProperty(ref _rightThumbButton, value); }

        private bool _buttonGuide;
        public bool ButtonGuide { get => _buttonGuide; set => SetProperty(ref _buttonGuide, value); }

        private bool _dpadUp;
        public bool DPadUp { get => _dpadUp; set => SetProperty(ref _dpadUp, value); }

        private bool _dpadDown;
        public bool DPadDown { get => _dpadDown; set => SetProperty(ref _dpadDown, value); }

        private bool _dpadLeft;
        public bool DPadLeft { get => _dpadLeft; set => SetProperty(ref _dpadLeft, value); }

        private bool _dpadRight;
        public bool DPadRight { get => _dpadRight; set => SetProperty(ref _dpadRight, value); }

        // Triggers (0–255 → normalized 0.0–1.0 for visualizer)
        private double _leftTrigger;

        /// <summary>Left trigger value normalized 0.0–1.0.</summary>
        public double LeftTrigger { get => _leftTrigger; set => SetProperty(ref _leftTrigger, value); }

        private double _rightTrigger;

        /// <summary>Right trigger value normalized 0.0–1.0.</summary>
        public double RightTrigger { get => _rightTrigger; set => SetProperty(ref _rightTrigger, value); }

        // Thumbsticks (normalized 0.0–1.0, where 0.5 = center)
        private double _thumbLX = 0.5;

        /// <summary>Left thumbstick X position, normalized 0.0–1.0 (0.5 = center).</summary>
        public double ThumbLX { get => _thumbLX; set => SetProperty(ref _thumbLX, value); }

        private double _thumbLY = 0.5;

        /// <summary>Left thumbstick Y position, normalized 0.0–1.0 (0.5 = center).</summary>
        public double ThumbLY { get => _thumbLY; set => SetProperty(ref _thumbLY, value); }

        private double _thumbRX = 0.5;

        /// <summary>Right thumbstick X position, normalized 0.0–1.0 (0.5 = center).</summary>
        public double ThumbRX { get => _thumbRX; set => SetProperty(ref _thumbRX, value); }

        private double _thumbRY = 0.5;

        /// <summary>Right thumbstick Y position, normalized 0.0–1.0 (0.5 = center).</summary>
        public double ThumbRY { get => _thumbRY; set => SetProperty(ref _thumbRY, value); }

        // Raw signed values (for numeric display)
        private short _rawThumbLX;
        public short RawThumbLX { get => _rawThumbLX; set => SetProperty(ref _rawThumbLX, value); }

        private short _rawThumbLY;
        public short RawThumbLY { get => _rawThumbLY; set => SetProperty(ref _rawThumbLY, value); }

        private short _rawThumbRX;
        public short RawThumbRX { get => _rawThumbRX; set => SetProperty(ref _rawThumbRX, value); }

        private short _rawThumbRY;
        public short RawThumbRY { get => _rawThumbRY; set => SetProperty(ref _rawThumbRY, value); }

        private byte _rawLeftTrigger;
        public byte RawLeftTrigger { get => _rawLeftTrigger; set => SetProperty(ref _rawLeftTrigger, value); }

        private byte _rawRightTrigger;
        public byte RawRightTrigger { get => _rawRightTrigger; set => SetProperty(ref _rawRightTrigger, value); }

        // ─────────────────────────────────────────────
        //  Mapping rows
        // ─────────────────────────────────────────────

        /// <summary>
        /// Collection of mapping rows, one per Xbox controller target
        /// (buttons, triggers, thumbstick axes, D-pad directions).
        /// Each row links a physical input source to an XInput output.
        /// </summary>
        public ObservableCollection<MappingItem> Mappings { get; } =
            new ObservableCollection<MappingItem>();

        /// <summary>
        /// Initializes the default set of mapping rows for all standard
        /// Xbox controller inputs.
        /// </summary>
        private void InitializeDefaultMappings()
        {
            Mappings.Add(new MappingItem("A", "ButtonA", MappingCategory.Buttons));
            Mappings.Add(new MappingItem("B", "ButtonB", MappingCategory.Buttons));
            Mappings.Add(new MappingItem("X", "ButtonX", MappingCategory.Buttons));
            Mappings.Add(new MappingItem("Y", "ButtonY", MappingCategory.Buttons));

            Mappings.Add(new MappingItem("Left Bumper", "LeftShoulder", MappingCategory.Buttons));
            Mappings.Add(new MappingItem("Right Bumper", "RightShoulder", MappingCategory.Buttons));

            Mappings.Add(new MappingItem("Back", "ButtonBack", MappingCategory.Buttons));
            Mappings.Add(new MappingItem("Start", "ButtonStart", MappingCategory.Buttons));
            Mappings.Add(new MappingItem("Guide", "ButtonGuide", MappingCategory.Buttons));

            Mappings.Add(new MappingItem("Left Stick Click", "LeftThumbButton", MappingCategory.Buttons));
            Mappings.Add(new MappingItem("Right Stick Click", "RightThumbButton", MappingCategory.Buttons));

            Mappings.Add(new MappingItem("D-Pad Up", "DPadUp", MappingCategory.DPad));
            Mappings.Add(new MappingItem("D-Pad Down", "DPadDown", MappingCategory.DPad));
            Mappings.Add(new MappingItem("D-Pad Left", "DPadLeft", MappingCategory.DPad));
            Mappings.Add(new MappingItem("D-Pad Right", "DPadRight", MappingCategory.DPad));

            Mappings.Add(new MappingItem("Left Trigger", "LeftTrigger", MappingCategory.Triggers));
            Mappings.Add(new MappingItem("Right Trigger", "RightTrigger", MappingCategory.Triggers));

            Mappings.Add(new MappingItem("Left Stick X", "LeftThumbAxisX", MappingCategory.LeftStick));
            Mappings.Add(new MappingItem("Left Stick Y", "LeftThumbAxisY", MappingCategory.LeftStick));
            Mappings.Add(new MappingItem("Right Stick X", "RightThumbAxisX", MappingCategory.RightStick));
            Mappings.Add(new MappingItem("Right Stick Y", "RightThumbAxisY", MappingCategory.RightStick));
        }

        // ─────────────────────────────────────────────
        //  Force feedback settings
        // ─────────────────────────────────────────────

        private int _forceOverallGain = 100;

        /// <summary>Overall force feedback gain percentage (0–100).</summary>
        public int ForceOverallGain
        {
            get => _forceOverallGain;
            set => SetProperty(ref _forceOverallGain, Math.Clamp(value, 0, 100));
        }

        private int _leftMotorStrength = 100;

        /// <summary>Left motor strength percentage (0–100).</summary>
        public int LeftMotorStrength
        {
            get => _leftMotorStrength;
            set => SetProperty(ref _leftMotorStrength, Math.Clamp(value, 0, 100));
        }

        private int _rightMotorStrength = 100;

        /// <summary>Right motor strength percentage (0–100).</summary>
        public int RightMotorStrength
        {
            get => _rightMotorStrength;
            set => SetProperty(ref _rightMotorStrength, Math.Clamp(value, 0, 100));
        }

        private bool _swapMotors;

        /// <summary>Whether to swap left and right rumble motors.</summary>
        public bool SwapMotors
        {
            get => _swapMotors;
            set => SetProperty(ref _swapMotors, value);
        }

        // Rumble feedback display (shows what the game is requesting)
        private double _leftMotorDisplay;

        /// <summary>Left motor activity level 0.0–1.0 for visual display.</summary>
        public double LeftMotorDisplay
        {
            get => _leftMotorDisplay;
            set => SetProperty(ref _leftMotorDisplay, value);
        }

        private double _rightMotorDisplay;

        /// <summary>Right motor activity level 0.0–1.0 for visual display.</summary>
        public double RightMotorDisplay
        {
            get => _rightMotorDisplay;
            set => SetProperty(ref _rightMotorDisplay, value);
        }

        // ─────────────────────────────────────────────
        //  Dead zone settings
        // ─────────────────────────────────────────────

        private int _leftDeadZone;
        public int LeftDeadZone { get => _leftDeadZone; set => SetProperty(ref _leftDeadZone, Math.Clamp(value, 0, 100)); }

        private int _rightDeadZone;
        public int RightDeadZone { get => _rightDeadZone; set => SetProperty(ref _rightDeadZone, Math.Clamp(value, 0, 100)); }

        private int _leftAntiDeadZone;
        public int LeftAntiDeadZone { get => _leftAntiDeadZone; set => SetProperty(ref _leftAntiDeadZone, Math.Clamp(value, 0, 100)); }

        private int _rightAntiDeadZone;
        public int RightAntiDeadZone { get => _rightAntiDeadZone; set => SetProperty(ref _rightAntiDeadZone, Math.Clamp(value, 0, 100)); }

        // ─────────────────────────────────────────────
        //  Commands
        // ─────────────────────────────────────────────

        private RelayCommand _testRumbleCommand;

        /// <summary>Command to send a brief test rumble to the mapped device.</summary>
        public RelayCommand TestRumbleCommand =>
            _testRumbleCommand ??= new RelayCommand(
                () => TestRumbleRequested?.Invoke(this, EventArgs.Empty),
                () => IsDeviceOnline);

        /// <summary>Raised when the user clicks the test rumble button.</summary>
        public event EventHandler TestRumbleRequested;

        private RelayCommand _clearMappingsCommand;

        /// <summary>Command to clear all mapping assignments for this pad.</summary>
        public RelayCommand ClearMappingsCommand =>
            _clearMappingsCommand ??= new RelayCommand(ClearAllMappings);

        /// <summary>
        /// Clears the source assignment on all mapping rows.
        /// </summary>
        private void ClearAllMappings()
        {
            foreach (var m in Mappings)
                m.SourceDescriptor = string.Empty;
        }

        // ─────────────────────────────────────────────
        //  State update (called by InputService at 30Hz)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Updates all output state properties from a combined Gamepad struct.
        /// Called on the UI thread by InputService at 30Hz.
        /// </summary>
        /// <param name="gp">The combined gamepad state for this slot.</param>
        /// <param name="vibration">The current vibration state for this slot.</param>
        public void UpdateFromEngineState(Gamepad gp, Engine.Vibration vibration)
        {
            // Buttons
            ButtonA = gp.IsButtonPressed(Gamepad.A);
            ButtonB = gp.IsButtonPressed(Gamepad.B);
            ButtonX = gp.IsButtonPressed(Gamepad.X);
            ButtonY = gp.IsButtonPressed(Gamepad.Y);
            LeftShoulder = gp.IsButtonPressed(Gamepad.LEFT_SHOULDER);
            RightShoulder = gp.IsButtonPressed(Gamepad.RIGHT_SHOULDER);
            ButtonBack = gp.IsButtonPressed(Gamepad.BACK);
            ButtonStart = gp.IsButtonPressed(Gamepad.START);
            LeftThumbButton = gp.IsButtonPressed(Gamepad.LEFT_THUMB);
            RightThumbButton = gp.IsButtonPressed(Gamepad.RIGHT_THUMB);
            ButtonGuide = gp.IsButtonPressed(Gamepad.GUIDE);
            DPadUp = gp.IsButtonPressed(Gamepad.DPAD_UP);
            DPadDown = gp.IsButtonPressed(Gamepad.DPAD_DOWN);
            DPadLeft = gp.IsButtonPressed(Gamepad.DPAD_LEFT);
            DPadRight = gp.IsButtonPressed(Gamepad.DPAD_RIGHT);

            // Triggers
            RawLeftTrigger = gp.LeftTrigger;
            RawRightTrigger = gp.RightTrigger;
            LeftTrigger = gp.LeftTrigger / 255.0;
            RightTrigger = gp.RightTrigger / 255.0;

            // Thumbsticks — convert signed (-32768..32767) to normalized (0.0..1.0)
            RawThumbLX = gp.ThumbLX;
            RawThumbLY = gp.ThumbLY;
            RawThumbRX = gp.ThumbRX;
            RawThumbRY = gp.ThumbRY;
            ThumbLX = (gp.ThumbLX - (double)short.MinValue) / 65535.0;
            ThumbLY = 1.0 - ((gp.ThumbLY - (double)short.MinValue) / 65535.0); // Invert Y for display
            ThumbRX = (gp.ThumbRX - (double)short.MinValue) / 65535.0;
            ThumbRY = 1.0 - ((gp.ThumbRY - (double)short.MinValue) / 65535.0);

            // Vibration display
            if (vibration != null)
            {
                LeftMotorDisplay = vibration.LeftMotorSpeed / 65535.0;
                RightMotorDisplay = vibration.RightMotorSpeed / 65535.0;
            }
        }

        /// <summary>
        /// Refreshes command CanExecute states. Call after IsDeviceOnline changes.
        /// </summary>
        public void RefreshCommands()
        {
            _testRumbleCommand?.NotifyCanExecuteChanged();
        }
    }
}
