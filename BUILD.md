# PadForge — Build & Project Reference

## Overview

PadForge is a modern controller mapping utility (fork of x360ce) rebuilt with:
- **SDL3** for device input (replaces DirectInput/SharpDX)
- **XInput P/Invoke** for native Xbox controller support
- **ViGEmBus** for virtual Xbox 360 controller output
- **.NET 8 WPF** with ModernWpf Fluent Design
- **MVVM** architecture with CommunityToolkit.Mvvm

## Solution Structure

```
PadForge.sln
├── PadForge.Engine/          (Class library — net8.0-windows)
│   ├── Common/
│   │   ├── SDL3Minimal.cs         SDL3 P/Invoke declarations
│   │   ├── InputTypes.cs          Enums: MapType, ObjectGuid, InputDeviceType, etc.
│   │   ├── SdlDeviceWrapper.cs    SDL joystick wrapper (open, read, rumble, GUID)
│   │   ├── CustomInputState.cs    Unified input state (axes, buttons, POVs, sliders)
│   │   ├── CustomInputHelper.cs   State comparison and update helpers
│   │   ├── CustomInputUpdate.cs   Buffered input change records
│   │   ├── DeviceObjectItem.cs    Device axis/button/POV capability metadata
│   │   ├── DeviceEffectItem.cs    Force feedback effect metadata
│   │   └── ForceFeedbackState.cs  Rumble state management + Vibration class
│   ├── Data/
│   │   ├── UserDevice.cs          Physical device record (serializable + runtime)
│   │   ├── UserSetting.cs         Device-to-slot link (serializable)
│   │   └── PadSetting.cs          Mapping configuration (21 mapping + 8 dead zone + 5 FF properties)
│   └── Properties/
│       └── AssemblyInfo.cs
│
└── PadForge.App/             (WPF Application — net8.0-windows)
    ├── App.xaml / .cs             Application entry, ModernWpf resources, converter registration
    ├── MainWindow.xaml / .cs      Shell: NavigationView + status bar + page switching + service wiring
    ├── Common/
    │   ├── SettingsManager.cs     Static class: device/setting collections, assignment, defaults
    │   └── Input/
    │       ├── InputManager.cs                 Main partial: background thread, 6-step pipeline, Gamepad struct
    │       ├── InputManager.Step1.*.cs         Device enumeration (SDL + XInput native)
    │       ├── InputManager.Step2.*.cs         State reading + force feedback
    │       ├── InputManager.Step3.*.cs         CustomInputState → Gamepad mapping
    │       ├── InputManager.Step4.*.cs         Multi-device combination per slot
    │       ├── InputManager.Step5.*.cs         ViGEmBus virtual controller output
    │       ├── InputManager.Step6.*.cs         XInput state readback for UI
    │       ├── InputManager.XInputLibrary.cs   XInput DLL detection and loading
    │       ├── InputExceptionEventArgs.cs      Error event args
    │       ├── InputEventArgs.cs               Input event args + InputEventType enum
    │       ├── InputException.cs               Custom exception with pipeline context
    │       ├── UserSetting.Partial.cs          Runtime fields: XiState, cached PadSetting
    │       └── PadSetting.Partial.cs           Documents carried-over mapping properties
    ├── XInputInterop.cs           XInput P/Invoke, state conversion, PIDVID detection, Share button HID
    ├── Converters/
    │   ├── BoolToColorConverter.cs
    │   ├── BoolToVisibilityConverter.cs
    │   ├── BoolToInstallTextConverter.cs
    │   ├── StatusToColorConverter.cs
    │   ├── NormToCanvasConverter.cs
    │   ├── AxisToPercentConverter.cs
    │   └── PovToAngleConverter.cs
    ├── ViewModels/
    │   ├── ViewModelBase.cs            INotifyPropertyChanged base
    │   ├── MainViewModel.cs            Root: navigation, pads, engine status, commands
    │   ├── DashboardViewModel.cs       Overview: slot summaries, engine stats, driver info
    │   ├── PadViewModel.cs             Per-slot: visualizer state, mappings, dead zones, FF
    │   ├── MappingItem.cs              Single mapping row: target, source, recording, options
    │   ├── DevicesViewModel.cs         Device list, raw state display, assign/hide commands
    │   ├── DeviceRowViewModel.cs       Single device row: identity, status, capabilities
    │   └── SettingsViewModel.cs        App settings: theme, engine, ViGEm, file, diagnostics
    ├── Views/
    │   ├── DashboardPage.xaml / .cs    Slot cards, engine card, driver status
    │   ├── PadPage.xaml / .cs          Visualizer, mapping grid, dead zones, force feedback
    │   ├── DevicesPage.xaml / .cs      DataGrid + detail panel with raw state
    │   ├── SettingsPage.xaml / .cs     Theme, engine, ViGEm, file, diagnostics sections
    │   └── AboutPage.xaml / .cs        App info, technology list, license
    ├── Services/
    │   ├── InputService.cs             Engine ↔ UI bridge: 30Hz DispatcherTimer, state sync
    │   ├── SettingsService.cs          XML persistence: load/save/reset/reload
    │   ├── RecorderService.cs          Input recording: baseline→detection→descriptor
    │   └── DeviceService.cs            Device assignment and hiding
    └── Properties/
        └── AssemblyInfo.cs
```

## Prerequisites

- .NET 8 SDK (net8.0-windows)
- Windows 10/11 (for WPF, XInput, HID APIs)
- SDL3.dll in the output directory (copy from SDL3 releases or use NuGet)
- ViGEmBus driver (optional — for virtual controller output)

## NuGet Dependencies

**PadForge.Engine.csproj:**
```
(none — pure P/Invoke, no third-party packages)
```

**PadForge.App.csproj:**
```
ModernWpfUI (>= 0.9.6)
CommunityToolkit.Mvvm (>= 8.2.0)
```

## Build

```bash
dotnet restore PadForge.sln
dotnet build PadForge.sln -c Release
```

## Runtime Requirements

1. **SDL3.dll** — Place `SDL3.dll` in the output directory next to the .exe.
   Download from https://github.com/libsdl-org/SDL/releases.
   Copy the x64 DLL for 64-bit builds.

2. **ViGEmBus** (optional) — Install from https://github.com/nefarius/ViGEmBus/releases.
   Required for virtual Xbox 360 controller output. The app will run without it
   but won't be able to feed game input.

3. **XInput** — Ships with Windows (xinput1_4.dll). No action needed.

## Architecture Notes

### Threading Model
- **InputManager** runs a background thread at ~1000Hz (1ms sleep).
- **InputService** runs a DispatcherTimer on the UI thread at ~30Hz.
- State transfer: InputManager writes to `CombinedXiStates[]` arrays;
  InputService reads them and pushes to ViewModels.
- All ViewModel property sets happen on the UI thread.

### 6-Step Pipeline (per cycle)
1. **UpdateDevices** — SDL enumeration, open new, detect disconnections, XInput native
2. **UpdateInputStates** — Read axes/buttons/POVs from SDL or XInput
3. **UpdateXiStates** — Map CustomInputState → Gamepad via PadSetting descriptors
4. **CombineXiStates** — Merge multiple devices per slot (OR/MAX/largest-magnitude)
5. **UpdateVirtualDevices** — Feed ViGEmBus virtual controllers
6. **RetrieveXiStates** — Read back from system XInput for UI display

### Mapping Descriptors
String format: `"[I][H]{Type} {Index} [{Direction}]"`
- `Button 0`, `Axis 1`, `IHAxis 2`, `POV 0 Up`, `Slider 0`
- Prefixes: `I` = inverted, `H` = half-axis, `IH` = inverted half

### Settings File (PadForge.xml)
```xml
<PadForgeSettings>
  <Devices><Device>...</Device></Devices>
  <UserSettings><Setting>...</Setting></UserSettings>
  <PadSettings><PadSetting>...</PadSetting></PadSettings>
  <AppSettings>...</AppSettings>
</PadForgeSettings>
```
