using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using PadForge.Common.Input;
using PadForge.Engine.Data;
using PadForge.ViewModels;

namespace PadForge.Services
{
    /// <summary>
    /// Service responsible for loading and saving PadForge settings to XML files.
    /// Handles the bidirectional sync between the SettingsManager's data collections
    /// and the WPF ViewModels.
    /// 
    /// Settings file search order:
    ///   1. PadForge.xml (preferred for new installs)
    ///   2. Settings.xml (generic fallback)
    /// 
    /// The settings file lives next to the executable.
    /// </summary>
    public class SettingsService
    {
        // ─────────────────────────────────────────────
        //  Constants
        // ─────────────────────────────────────────────

        /// <summary>Primary settings file name.</summary>
        public const string PrimaryFileName = "PadForge.xml";

        /// <summary>Fallback settings file name.</summary>
        public const string FallbackFileName = "Settings.xml";

        // ─────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────

        private readonly MainViewModel _mainVm;
        private string _settingsFilePath;

        /// <summary>
        /// Full path to the active settings file.
        /// </summary>
        public string SettingsFilePath => _settingsFilePath;

        /// <summary>
        /// Whether settings have been modified since last save.
        /// </summary>
        public bool IsDirty { get; private set; }

        // ─────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────

        public SettingsService(MainViewModel mainVm)
        {
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));
        }

        // ─────────────────────────────────────────────
        //  Initialize
        // ─────────────────────────────────────────────

        /// <summary>
        /// Initializes the settings service: ensures SettingsManager collections
        /// exist, finds the settings file, and loads it.
        /// </summary>
        public void Initialize()
        {
            // Ensure SettingsManager collections are initialized.
            if (SettingsManager.UserDevices == null)
                SettingsManager.UserDevices = new DeviceCollection();
            if (SettingsManager.UserSettings == null)
                SettingsManager.UserSettings = new SettingsCollection();

            // Find or create the settings file.
            _settingsFilePath = FindSettingsFile();

            // Load settings from disk.
            if (File.Exists(_settingsFilePath))
            {
                LoadFromFile(_settingsFilePath);
            }

            // Push file path to ViewModel.
            _mainVm.Settings.SettingsFilePath = _settingsFilePath;
            _mainVm.Settings.HasUnsavedChanges = false;
            IsDirty = false;
        }

        // ─────────────────────────────────────────────
        //  File discovery
        // ─────────────────────────────────────────────

        /// <summary>
        /// Finds the settings file. Checks for the primary file first,
        /// then fallback, then creates the primary file path for new installs.
        /// </summary>
        private static string FindSettingsFile()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;

            // Check primary file.
            string primaryPath = Path.Combine(appDir, PrimaryFileName);
            if (File.Exists(primaryPath))
                return primaryPath;

            // Check fallback file.
            string fallbackPath = Path.Combine(appDir, FallbackFileName);
            if (File.Exists(fallbackPath))
                return fallbackPath;

            // Neither exists — use primary path for new file.
            return primaryPath;
        }

        // ─────────────────────────────────────────────
        //  Load
        // ─────────────────────────────────────────────

        /// <summary>
        /// Loads settings from an XML file into the SettingsManager collections.
        /// </summary>
        /// <param name="filePath">Path to the settings XML file.</param>
        public void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            try
            {
                SettingsFileData data;
                var serializer = new XmlSerializer(typeof(SettingsFileData));

                using (var stream = File.OpenRead(filePath))
                {
                    data = (SettingsFileData)serializer.Deserialize(stream);
                }

                if (data == null)
                    return;

                // Populate SettingsManager collections.
                lock (SettingsManager.UserDevices.SyncRoot)
                {
                    SettingsManager.UserDevices.Items.Clear();
                    if (data.Devices != null)
                    {
                        foreach (var ud in data.Devices)
                            SettingsManager.UserDevices.Items.Add(ud);
                    }
                }

                lock (SettingsManager.UserSettings.SyncRoot)
                {
                    SettingsManager.UserSettings.Items.Clear();
                    if (data.Settings != null)
                    {
                        foreach (var us in data.Settings)
                        {
                            // Link PadSetting.
                            if (data.PadSettings != null && us.PadSettingChecksum != null)
                            {
                                var ps = data.PadSettings.FirstOrDefault(
                                    p => p.PadSettingChecksum == us.PadSettingChecksum);
                                us.SetPadSetting(ps);
                            }

                            SettingsManager.UserSettings.Items.Add(us);
                        }
                    }
                }

                // Load app settings into ViewModel.
                if (data.AppSettings != null)
                    LoadAppSettings(data.AppSettings);

                // Load pad-specific settings.
                if (data.PadSettings != null)
                    LoadPadSettings(data.Settings, data.PadSettings);
            }
            catch (Exception ex)
            {
                _mainVm.StatusText = $"Error loading settings: {ex.Message}";
            }
        }

        /// <summary>
        /// Pushes application-level settings to the SettingsViewModel.
        /// </summary>
        private void LoadAppSettings(AppSettingsData appSettings)
        {
            var vm = _mainVm.Settings;
            vm.AutoStartEngine = appSettings.AutoStartEngine;
            vm.MinimizeToTray = appSettings.MinimizeToTray;
            vm.StartMinimized = appSettings.StartMinimized;
            vm.EnablePollingOnFocusLoss = appSettings.EnablePollingOnFocusLoss;
            vm.PollingRateMs = appSettings.PollingRateMs;
            vm.SelectedThemeIndex = appSettings.ThemeIndex;
        }

        /// <summary>
        /// Pushes per-pad settings to PadViewModels.
        /// </summary>
        private void LoadPadSettings(UserSetting[] settings, PadSetting[] padSettings)
        {
            if (settings == null || padSettings == null)
                return;

            foreach (var us in settings)
            {
                int padIndex = us.MapTo;
                if (padIndex < 0 || padIndex >= _mainVm.Pads.Count)
                    continue;

                var padVm = _mainVm.Pads[padIndex];
                var ps = us.GetPadSetting();
                if (ps == null)
                    continue;

                // Load force feedback settings.
                padVm.ForceOverallGain = TryParseInt(ps.ForceOverall, 100);
                padVm.LeftMotorStrength = TryParseInt(ps.LeftMotorStrength, 100);
                padVm.RightMotorStrength = TryParseInt(ps.RightMotorStrength, 100);
                padVm.SwapMotors = ps.ForceSwapMotor == "1" ||
                    (ps.ForceSwapMotor ?? "").Equals("true", StringComparison.OrdinalIgnoreCase);

                // Load dead zone settings.
                padVm.LeftDeadZone = TryParseInt(ps.LeftThumbDeadZoneX, 0);
                padVm.RightDeadZone = TryParseInt(ps.RightThumbDeadZoneX, 0);
                padVm.LeftAntiDeadZone = TryParseInt(ps.LeftThumbAntiDeadZone, 0);
                padVm.RightAntiDeadZone = TryParseInt(ps.RightThumbAntiDeadZone, 0);

                // Load mapping descriptors into mapping rows.
                LoadMappingDescriptors(padVm, ps);
            }
        }

        /// <summary>
        /// Populates PadViewModel mapping rows from a PadSetting's descriptor strings.
        /// </summary>
        private static void LoadMappingDescriptors(PadViewModel padVm, PadSetting ps)
        {
            foreach (var mapping in padVm.Mappings)
            {
                string value = GetPadSettingProperty(ps, mapping.TargetSettingName);
                mapping.SourceDescriptor = value ?? string.Empty;
            }
        }

        // ─────────────────────────────────────────────
        //  Save
        // ─────────────────────────────────────────────

        /// <summary>
        /// Saves current settings to the active settings file.
        /// </summary>
        public void Save()
        {
            SaveToFile(_settingsFilePath);
        }

        /// <summary>
        /// Saves all settings to the specified XML file.
        /// </summary>
        /// <param name="filePath">Output file path.</param>
        public void SaveToFile(string filePath)
        {
            try
            {
                var data = new SettingsFileData();

                // Collect devices.
                lock (SettingsManager.UserDevices.SyncRoot)
                {
                    data.Devices = SettingsManager.UserDevices.Items.ToArray();
                }

                // Collect user settings and pad settings.
                lock (SettingsManager.UserSettings.SyncRoot)
                {
                    data.Settings = SettingsManager.UserSettings.Items.ToArray();

                    // Collect unique PadSettings.
                    data.PadSettings = SettingsManager.UserSettings.Items
                        .Select(s => s.GetPadSetting())
                        .Where(p => p != null)
                        .Distinct()
                        .ToArray();
                }

                // Collect app settings from ViewModel.
                data.AppSettings = BuildAppSettings();

                // Update PadSettings from ViewModels before saving.
                UpdatePadSettingsFromViewModels();

                // Serialize.
                var serializer = new XmlSerializer(typeof(SettingsFileData));
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var stream = File.Create(filePath))
                {
                    serializer.Serialize(stream, data);
                }

                IsDirty = false;
                _mainVm.Settings.HasUnsavedChanges = false;
                _mainVm.StatusText = $"Settings saved to {Path.GetFileName(filePath)}.";
            }
            catch (Exception ex)
            {
                _mainVm.StatusText = $"Error saving settings: {ex.Message}";
            }
        }

        /// <summary>
        /// Builds an AppSettingsData from the current SettingsViewModel state.
        /// </summary>
        private AppSettingsData BuildAppSettings()
        {
            var vm = _mainVm.Settings;
            return new AppSettingsData
            {
                AutoStartEngine = vm.AutoStartEngine,
                MinimizeToTray = vm.MinimizeToTray,
                StartMinimized = vm.StartMinimized,
                EnablePollingOnFocusLoss = vm.EnablePollingOnFocusLoss,
                PollingRateMs = vm.PollingRateMs,
                ThemeIndex = vm.SelectedThemeIndex
            };
        }

        /// <summary>
        /// Pushes ViewModel values back into PadSetting objects before saving.
        /// </summary>
        private void UpdatePadSettingsFromViewModels()
        {
            var settings = SettingsManager.UserSettings?.Items;
            if (settings == null) return;

            lock (SettingsManager.UserSettings.SyncRoot)
            {
                foreach (var us in settings)
                {
                    var ps = us.GetPadSetting();
                    if (ps == null) continue;

                    int padIndex = us.MapTo;
                    if (padIndex < 0 || padIndex >= _mainVm.Pads.Count)
                        continue;

                    var padVm = _mainVm.Pads[padIndex];

                    // Write force feedback settings.
                    ps.ForceOverall = padVm.ForceOverallGain.ToString();
                    ps.LeftMotorStrength = padVm.LeftMotorStrength.ToString();
                    ps.RightMotorStrength = padVm.RightMotorStrength.ToString();
                    ps.ForceSwapMotor = padVm.SwapMotors ? "1" : "0";

                    // Write dead zone settings.
                    ps.LeftThumbDeadZoneX = padVm.LeftDeadZone.ToString();
                    ps.LeftThumbDeadZoneY = padVm.LeftDeadZone.ToString();
                    ps.RightThumbDeadZoneX = padVm.RightDeadZone.ToString();
                    ps.RightThumbDeadZoneY = padVm.RightDeadZone.ToString();
                    ps.LeftThumbAntiDeadZone = padVm.LeftAntiDeadZone.ToString();
                    ps.RightThumbAntiDeadZone = padVm.RightAntiDeadZone.ToString();

                    // Write mapping descriptors.
                    foreach (var mapping in padVm.Mappings)
                    {
                        SetPadSettingProperty(ps, mapping.TargetSettingName, mapping.SourceDescriptor);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────
        //  Reset
        // ─────────────────────────────────────────────

        /// <summary>
        /// Resets all settings to defaults. Clears all mappings and device records.
        /// </summary>
        public void ResetToDefaults()
        {
            lock (SettingsManager.UserDevices.SyncRoot)
            {
                SettingsManager.UserDevices.Items.Clear();
            }

            lock (SettingsManager.UserSettings.SyncRoot)
            {
                SettingsManager.UserSettings.Items.Clear();
            }

            // Reset ViewModels.
            foreach (var padVm in _mainVm.Pads)
            {
                foreach (var mapping in padVm.Mappings)
                    mapping.SourceDescriptor = string.Empty;

                padVm.ForceOverallGain = 100;
                padVm.LeftMotorStrength = 100;
                padVm.RightMotorStrength = 100;
                padVm.SwapMotors = false;
                padVm.LeftDeadZone = 0;
                padVm.RightDeadZone = 0;
                padVm.LeftAntiDeadZone = 0;
                padVm.RightAntiDeadZone = 0;
            }

            var settingsVm = _mainVm.Settings;
            settingsVm.AutoStartEngine = true;
            settingsVm.MinimizeToTray = false;
            settingsVm.StartMinimized = false;
            settingsVm.EnablePollingOnFocusLoss = true;
            settingsVm.PollingRateMs = 1;
            settingsVm.SelectedThemeIndex = 0;

            IsDirty = true;
            settingsVm.HasUnsavedChanges = true;
            _mainVm.StatusText = "Settings reset to defaults.";
        }

        // ─────────────────────────────────────────────
        //  Reload
        // ─────────────────────────────────────────────

        /// <summary>
        /// Reloads settings from disk, discarding any unsaved changes.
        /// </summary>
        public void Reload()
        {
            if (File.Exists(_settingsFilePath))
            {
                LoadFromFile(_settingsFilePath);
                _mainVm.StatusText = "Settings reloaded from disk.";
            }
            else
            {
                _mainVm.StatusText = "No settings file found on disk.";
            }

            IsDirty = false;
            _mainVm.Settings.HasUnsavedChanges = false;
        }

        /// <summary>
        /// Marks settings as dirty (unsaved changes).
        /// </summary>
        public void MarkDirty()
        {
            IsDirty = true;
            _mainVm.Settings.HasUnsavedChanges = true;
        }

        // ─────────────────────────────────────────────
        //  PadSetting reflection helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Gets a string property value from a PadSetting by property name.
        /// </summary>
        private static string GetPadSettingProperty(PadSetting ps, string propertyName)
        {
            if (ps == null || string.IsNullOrEmpty(propertyName))
                return string.Empty;

            var prop = typeof(PadSetting).GetProperty(propertyName);
            if (prop == null || prop.PropertyType != typeof(string))
                return string.Empty;

            return prop.GetValue(ps) as string ?? string.Empty;
        }

        /// <summary>
        /// Sets a string property value on a PadSetting by property name.
        /// </summary>
        private static void SetPadSettingProperty(PadSetting ps, string propertyName, string value)
        {
            if (ps == null || string.IsNullOrEmpty(propertyName))
                return;

            var prop = typeof(PadSetting).GetProperty(propertyName);
            if (prop == null || prop.PropertyType != typeof(string) || !prop.CanWrite)
                return;

            prop.SetValue(ps, value ?? string.Empty);
        }

        // ─────────────────────────────────────────────
        //  Parse helper
        // ─────────────────────────────────────────────

        private static int TryParseInt(string value, int defaultValue)
        {
            if (string.IsNullOrEmpty(value))
                return defaultValue;
            return int.TryParse(value, out int result) ? result : defaultValue;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Serialization data classes
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Root element for the PadForge settings XML file.
    /// </summary>
    [XmlRoot("PadForgeSettings")]
    public class SettingsFileData
    {
        [XmlArray("Devices")]
        [XmlArrayItem("Device")]
        public UserDevice[] Devices { get; set; }

        [XmlArray("UserSettings")]
        [XmlArrayItem("Setting")]
        public UserSetting[] Settings { get; set; }

        [XmlArray("PadSettings")]
        [XmlArrayItem("PadSetting")]
        public PadSetting[] PadSettings { get; set; }

        [XmlElement("AppSettings")]
        public AppSettingsData AppSettings { get; set; }
    }

    /// <summary>
    /// Application-level settings stored in the XML file.
    /// </summary>
    public class AppSettingsData
    {
        [XmlElement]
        public bool AutoStartEngine { get; set; } = true;

        [XmlElement]
        public bool MinimizeToTray { get; set; }

        [XmlElement]
        public bool StartMinimized { get; set; }

        [XmlElement]
        public bool EnablePollingOnFocusLoss { get; set; } = true;

        [XmlElement]
        public int PollingRateMs { get; set; } = 1;

        [XmlElement]
        public int ThemeIndex { get; set; }
    }
}
