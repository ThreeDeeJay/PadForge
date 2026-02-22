using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PadForge.ViewModels
{
    /// <summary>
    /// Root ViewModel for the application. Manages navigation state,
    /// the collection of 4 pad ViewModels, and app-wide status information.
    /// Serves as the DataContext for MainWindow.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        // ─────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────

        public MainViewModel()
        {
            Title = "PadForge";

            // Create the 4 pad ViewModels (one per virtual controller slot).
            for (int i = 0; i < 4; i++)
            {
                Pads.Add(new PadViewModel(i));
            }

            Dashboard = new DashboardViewModel();
            Devices = new DevicesViewModel();
            Settings = new SettingsViewModel();

            // Default to Dashboard page.
            _selectedNavTag = "Dashboard";
        }

        // ─────────────────────────────────────────────
        //  Child ViewModels
        // ─────────────────────────────────────────────

        /// <summary>
        /// The 4 virtual controller pad ViewModels (Player 1–4).
        /// </summary>
        public ObservableCollection<PadViewModel> Pads { get; } = new ObservableCollection<PadViewModel>();

        /// <summary>Dashboard overview ViewModel.</summary>
        public DashboardViewModel Dashboard { get; }

        /// <summary>Devices list ViewModel.</summary>
        public DevicesViewModel Devices { get; }

        /// <summary>Application settings ViewModel.</summary>
        public SettingsViewModel Settings { get; }

        // ─────────────────────────────────────────────
        //  Navigation
        // ─────────────────────────────────────────────

        private string _selectedNavTag;

        /// <summary>
        /// The tag string of the currently selected navigation item.
        /// Used by MainWindow to determine which page to display.
        /// Values: "Dashboard", "Pad1"–"Pad4", "Devices", "Settings", "About"
        /// </summary>
        public string SelectedNavTag
        {
            get => _selectedNavTag;
            set
            {
                if (SetProperty(ref _selectedNavTag, value))
                {
                    OnPropertyChanged(nameof(IsPadPageSelected));
                    OnPropertyChanged(nameof(SelectedPadIndex));
                }
            }
        }

        /// <summary>
        /// True if a Pad page (Pad1–Pad4) is currently selected.
        /// </summary>
        public bool IsPadPageSelected =>
            _selectedNavTag != null &&
            _selectedNavTag.StartsWith("Pad", StringComparison.Ordinal) &&
            _selectedNavTag.Length == 4 &&
            char.IsDigit(_selectedNavTag[3]);

        /// <summary>
        /// Returns the zero-based pad index for the currently selected Pad page,
        /// or -1 if no Pad page is selected.
        /// </summary>
        public int SelectedPadIndex
        {
            get
            {
                if (IsPadPageSelected && int.TryParse(_selectedNavTag.Substring(3), out int num))
                    return num - 1; // "Pad1" → 0, "Pad2" → 1, etc.
                return -1;
            }
        }

        /// <summary>
        /// Returns the PadViewModel for the currently selected Pad page, or null.
        /// </summary>
        public PadViewModel SelectedPad
        {
            get
            {
                int idx = SelectedPadIndex;
                if (idx >= 0 && idx < Pads.Count)
                    return Pads[idx];
                return null;
            }
        }

        // ─────────────────────────────────────────────
        //  App-wide status
        // ─────────────────────────────────────────────

        private string _statusText = "Ready";

        /// <summary>
        /// Status bar text displayed at the bottom of the main window.
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private bool _isEngineRunning;

        /// <summary>
        /// Whether the input engine polling loop is currently active.
        /// </summary>
        public bool IsEngineRunning
        {
            get => _isEngineRunning;
            set
            {
                if (SetProperty(ref _isEngineRunning, value))
                {
                    OnPropertyChanged(nameof(EngineStatusText));
                }
            }
        }

        /// <summary>
        /// Display string for engine status: "Running" or "Stopped".
        /// </summary>
        public string EngineStatusText => IsEngineRunning ? "Running" : "Stopped";

        private double _pollingFrequency;

        /// <summary>
        /// Current input polling frequency in Hz.
        /// </summary>
        public double PollingFrequency
        {
            get => _pollingFrequency;
            set => SetProperty(ref _pollingFrequency, value);
        }

        private int _connectedDeviceCount;

        /// <summary>
        /// Number of currently connected input devices.
        /// </summary>
        public int ConnectedDeviceCount
        {
            get => _connectedDeviceCount;
            set => SetProperty(ref _connectedDeviceCount, value);
        }

        private bool _isViGEmInstalled;

        /// <summary>
        /// Whether the ViGEmBus driver is installed and available.
        /// </summary>
        public bool IsViGEmInstalled
        {
            get => _isViGEmInstalled;
            set => SetProperty(ref _isViGEmInstalled, value);
        }

        // ─────────────────────────────────────────────
        //  Commands
        // ─────────────────────────────────────────────

        private RelayCommand _startEngineCommand;

        /// <summary>
        /// Command to start the input engine. Bound to a toolbar button.
        /// The actual start logic is in InputService; this command is
        /// wired up by MainWindow code-behind.
        /// </summary>
        public RelayCommand StartEngineCommand =>
            _startEngineCommand ??= new RelayCommand(
                () => StartEngineRequested?.Invoke(this, EventArgs.Empty),
                () => !IsEngineRunning);

        private RelayCommand _stopEngineCommand;

        /// <summary>
        /// Command to stop the input engine.
        /// </summary>
        public RelayCommand StopEngineCommand =>
            _stopEngineCommand ??= new RelayCommand(
                () => StopEngineRequested?.Invoke(this, EventArgs.Empty),
                () => IsEngineRunning);

        /// <summary>Raised when the user requests to start the engine.</summary>
        public event EventHandler StartEngineRequested;

        /// <summary>Raised when the user requests to stop the engine.</summary>
        public event EventHandler StopEngineRequested;

        /// <summary>
        /// Refreshes the CanExecute state of start/stop commands.
        /// Call after IsEngineRunning changes.
        /// </summary>
        public void RefreshCommands()
        {
            _startEngineCommand?.NotifyCanExecuteChanged();
            _stopEngineCommand?.NotifyCanExecuteChanged();
        }
    }
}
