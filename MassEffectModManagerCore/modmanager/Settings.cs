using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using IniParser;
using IniParser.Model;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.nexusmodsintegration;

namespace ME3TweaksModManager.modmanager
{
    [Localizable(false)]
    public static class Settings
    {
        #region Static Property Changed

        private static bool Loaded = false;
        public static event PropertyChangedEventHandler StaticPropertyChanged;
        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        private static bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
            if (Loaded)
            {
                LogSettingChanging(propertyName, value);
                Save();
            }
            return true;
        }

        private static void LogSettingChanging(string propertyName, object value)
        {
            if (Loaded && propertyName == nameof(LastSelectedTarget)) return; // This wil just generate a bunch of mostly useless logs. So only log the first one
            M3Log.Information($@"Setting changing: {propertyName} -> {value}");
        }

        #endregion
        private static bool _logModStartup = false;
        public static bool LogModStartup
        {
            get => _logModStartup;
            set => SetProperty(ref _logModStartup, value);
        }


        private static bool _logModUpdater = true;
        public static bool LogModUpdater
        {
            get => _logModUpdater;
            set => SetProperty(ref _logModUpdater, value);
        }

        private static bool _logBackupAndRestore;
        public static bool LogBackupAndRestore
        {
            get => _logBackupAndRestore;
            set => SetProperty(ref _logBackupAndRestore, value);
        }

        private static bool _launchGamesThroughOrigin = false; //Affects only ME1/ME2. ME3 always uses origin.
        public static bool LaunchGamesThroughOrigin
        {
            get => _launchGamesThroughOrigin;
            set => SetProperty(ref _launchGamesThroughOrigin, value);
        }

        public static bool _useOptimizedTextureRestore;
        public static bool UseOptimizedTextureRestore
        {
            get => _useOptimizedTextureRestore;
            set => SetProperty(ref _useOptimizedTextureRestore, value);
        }

        private static bool _generationSettingOT = true;
        public static bool GenerationSettingOT
        {
            get => _generationSettingOT;
            set
            {
                if (GenerationSettingLE || value)
                {
                    SetProperty(ref _generationSettingOT, value);
                }
                // Do not allow turning both options off
            }
        }

        private static bool _generationSettingLE = true;
        public static bool GenerationSettingLE
        {
            get => _generationSettingLE;
            set
            {
                if (GenerationSettingOT || value)
                {
                    SetProperty(ref _generationSettingLE, value);
                }
                // Do not allow turning both options off
            }
        }

        private static bool _ssuiLoadAllSAves = false;

        public static bool SSUILoadAllSaves
        {
            get => _ssuiLoadAllSAves;
            set => SetProperty(ref _ssuiLoadAllSAves, value);
        }

        private static bool _enableTelemetry = true;
        public static bool EnableTelemetry
        {
            get => _enableTelemetry;
            set => SetProperty(ref _enableTelemetry, value);
        }
        private static bool _betaMode = false;
        public static bool BetaMode
        {
            get => _betaMode;
            set => SetProperty(ref _betaMode, value);
        }

        private static bool _configureNXMHandlerOnBoot = true;

        public static bool ConfigureNXMHandlerOnBoot
        {
            get => _configureNXMHandlerOnBoot;
            set => SetProperty(ref _configureNXMHandlerOnBoot, value);
        }

        private static bool _modMakerAutoInjectCustomKeybindsOption = false;

        public static bool ModMakerAutoInjectCustomKeybindsOption
        {
            get => _modMakerAutoInjectCustomKeybindsOption;
            set => SetProperty(ref _modMakerAutoInjectCustomKeybindsOption, value);
        }

        private static bool _modMakerControllerModOption = false;

        public static bool ModMakerControllerModOption
        {
            get => _modMakerControllerModOption;
            set => SetProperty(ref _modMakerControllerModOption, value);
        }

        private static string _updaterServiceUsername;
        public static string UpdaterServiceUsername
        {
            get => _updaterServiceUsername;
            set => SetProperty(ref _updaterServiceUsername, value);
        }

        private static string _selectedFilters;
        public static string SelectedFilters
        {
            get => _selectedFilters;
            set => SetProperty(ref _selectedFilters, value);
        }

        private static string _modDownloadCacheFolder;
        public static string ModDownloadCacheFolder
        {
            get => _modDownloadCacheFolder;
            set => SetProperty(ref _modDownloadCacheFolder, value);
        }

        private static string _lastSelectedTarget;
        public static string LastSelectedTarget
        {
            get => _lastSelectedTarget;
            set => SetProperty(ref _lastSelectedTarget, value);
        }

        private static int _webclientTimeout = 7; // Defaults to 7
        public static int WebClientTimeout
        {
            get => _webclientTimeout;
            set => SetProperty(ref _webclientTimeout, value);
        }

        private static string _updateServiceLZMAStoragePath;
        public static string UpdaterServiceLZMAStoragePath
        {
            get => _updateServiceLZMAStoragePath;
            set => SetProperty(ref _updateServiceLZMAStoragePath, value);
        }

        private static string _updateServiceManifestStoragePath;
        public static string UpdaterServiceManifestStoragePath
        {
            get => _updateServiceManifestStoragePath;
            set => SetProperty(ref _updateServiceManifestStoragePath, value);
        }

        private static bool _logMixinStartup = false;
        public static bool LogMixinStartup
        {
            get => _logMixinStartup;
            set => SetProperty(ref _logMixinStartup, value);
        }

        private static bool _logModInstallation = false;
        public static bool LogModInstallation
        {
            get => _logModInstallation;
            set => SetProperty(ref _logModInstallation, value);
        }

        private static bool _developerMode;
        public static bool DeveloperMode
        {
            get => _developerMode;
            set => SetProperty(ref _developerMode, value);
        }

        private static bool _darkTheme;
        public static bool DarkTheme
        {
            get => _darkTheme;
            set => SetProperty(ref _darkTheme, value);
        }

        private static bool _autoUpdateLods4K = true;
        public static bool AutoUpdateLODs4K
        {
            get => _autoUpdateLods4K;
            set
            {
                SetProperty(ref _autoUpdateLods4K, value);
                if (!changingLODSetting && value)
                {
                    changingLODSetting = true;
                    AutoUpdateLODs2K = false;
                    changingLODSetting = false;
                }
            }
        }

        private static bool _autoUpdateLods2K = true;
        public static bool AutoUpdateLODs2K
        {
            get => _autoUpdateLods2K;
            set
            {
                SetProperty(ref _autoUpdateLods2K, value);
                if (!changingLODSetting && value)
                {
                    changingLODSetting = true;
                    AutoUpdateLODs4K = false;
                    changingLODSetting = false;
                }
            }
        }

        private static bool _preferCompressingPackages = true;
        public static bool PreferCompressingPackages
        {
            get => _preferCompressingPackages;
            set
            {
                SetProperty(ref _preferCompressingPackages, value);
            }
        }

        private static bool changingLODSetting;

        private static string _modLibraryPath;
        /// <summary>
        /// The path to the EXPLICITLY SET mod library. This value can be null; use the value from M3LoadedMods instead.
        /// </summary>
        public static string ModLibraryPath
        {
            get => _modLibraryPath;
            set => SetProperty(ref _modLibraryPath, value);
        }

        private static string _language;
        public static string Language
        {
            get => _language;
            set => SetProperty(ref _language, value);
        }

        private static DateTime _lastContentCheck = DateTime.MinValue;
        public static DateTime LastContentCheck
        {
            get => _lastContentCheck;
            set => SetProperty(ref _lastContentCheck, value);
        }

        private static bool _showedPreviewPanel;
        public static bool ShowedPreviewPanel
        {
            get => _showedPreviewPanel;
            set => SetProperty(ref _showedPreviewPanel, value);
        }

        /// <summary>
        /// This option should be used to gate telemetry submissions; only after the onboarding dialog has shown will we allow telemetry data to be submitted.
        /// </summary>
        public static bool CanSendTelemetry => ShowedPreviewPanel && EnableTelemetry;

        private static bool _logModMakerCompiler;
        public static bool LogModMakerCompiler
        {
            get => _logModMakerCompiler;
            set => SetProperty(ref _logModMakerCompiler, value);
        }

        private static bool _skipLELauncher = true;
        public static bool SkipLELauncher
        {
            get => _skipLELauncher;
            set => SetProperty(ref _skipLELauncher, value);
        }

        private static bool _doubleClickModInstall = false;
        public static bool DoubleClickModInstall
        {
            get => _doubleClickModInstall;
            set => SetProperty(ref _doubleClickModInstall, value);
        }

        private static bool _oneTimeMessageModListIsNotListOfInstalledMods = true;
        public static bool OneTimeMessage_ModListIsNotListOfInstalledMods
        {
            get => _oneTimeMessageModListIsNotListOfInstalledMods;
            set => SetProperty(ref _oneTimeMessageModListIsNotListOfInstalledMods, value);
        }

        private static Guid _selectedLE1LaunchOption = Guid.Empty;
        public static Guid SelectedLE1LaunchOption
        {
            get => _selectedLE1LaunchOption;
            set => SetProperty(ref _selectedLE1LaunchOption, value);
        }

        private static Guid _selectedLE2LaunchOption = Guid.Empty;
        public static Guid SelectedLE2LaunchOption
        {
            get => _selectedLE2LaunchOption;
            set => SetProperty(ref _selectedLE2LaunchOption, value);
        }

        private static Guid _selectedLE3LaunchOption = Guid.Empty;
        public static Guid SelectedLE3LaunchOption
        {
            get => _selectedLE3LaunchOption;
            set => SetProperty(ref _selectedLE3LaunchOption, value);
        }


        private static bool _enableLE1CoalescedMerge = true;
        public static bool EnableLE1CoalescedMerge
        {
            get => _enableLE1CoalescedMerge;
            set => SetProperty(ref _enableLE1CoalescedMerge, value);
        }

        private static bool _enableTextureSafetyChecks = true;
        public static bool EnableTextureSafetyChecks
        {
            get => _enableTextureSafetyChecks;
            set => SetProperty(ref _enableTextureSafetyChecks, value);
        }


        public static readonly string SettingsPath = Path.Combine(M3Filesystem.GetAppDataFolder(), "settings.ini");

        public static void Load()
        {
            if (!File.Exists(SettingsPath))
            {
                File.Create(SettingsPath).Close();
            }
            IniData settingsIni = null;
            try
            {
                settingsIni = new FileIniDataParser().ReadFile(SettingsPath);
            }
            catch (Exception e)
            {
                M3Log.Error("Error reading settings.ini file: " + e.Message);
                M3Log.Error("Mod Manager will use the defaults instead");
            }
            ShowedPreviewPanel = LoadSettingBool(settingsIni, "ModManager", "ShowedPreviewMessage2", false);
            Language = LoadSettingString(settingsIni, "ModManager", "Language", "int");
            LastSelectedTarget = LoadSettingString(settingsIni, "ModManager", "LastSelectedTarget", null);
            LastContentCheck = LoadSettingDateTime(settingsIni, "ModManager", "LastContentCheck", DateTime.MinValue);
            BetaMode = LoadSettingBool(settingsIni, "ModManager", "BetaMode", false);
            AutoUpdateLODs2K = LoadSettingBool(settingsIni, "ModManager", "AutoUpdateLODs2K", false);
            AutoUpdateLODs4K = LoadSettingBool(settingsIni, "ModManager", "AutoUpdateLODs4K", true);
            PreferCompressingPackages = LoadSettingBool(settingsIni, "ModManager", "PreferCompressingPackages", false); // 'May set to true in the future.
            WebClientTimeout = LoadSettingInt(settingsIni, "ModManager", "WebclientTimeout", 5);
            SelectedFilters = LoadSettingString(settingsIni, "ModManager", "SelectedFilters", null);
            ModMakerControllerModOption = LoadSettingBool(settingsIni, "ModMaker", "AutoAddControllerMixins", false);
            ModMakerAutoInjectCustomKeybindsOption = LoadSettingBool(settingsIni, "ModMaker", "AutoInjectCustomKeybinds", false);
            ModDownloadCacheFolder = LoadSettingString(settingsIni, "ModManager", "ModDownloadCacheFolder", null);


            UpdaterServiceUsername = LoadSettingString(settingsIni, "UpdaterService", "Username", null);
            UpdaterServiceLZMAStoragePath = LoadSettingString(settingsIni, "UpdaterService", "LZMAStoragePath", null);
            UpdaterServiceManifestStoragePath = LoadSettingString(settingsIni, "UpdaterService", "ManifestStoragePath", null);

            LogModStartup = LoadSettingBool(settingsIni, "Logging", "LogModStartup", false);
            LogModUpdater = LoadSettingBool(settingsIni, "Logging", "LogModUpdater", false);
            LogBackupAndRestore = LoadSettingBool(settingsIni, "Logging", "LogBackupAndRestore", false);
            LogMixinStartup = LoadSettingBool(settingsIni, "Logging", "LogMixinStartup", false);
            EnableTelemetry = LoadSettingBool(settingsIni, "Logging", "EnableTelemetry", true);
            LogModInstallation = LoadSettingBool(settingsIni, "Logging", "LogModInstallation", false);
            LogModMakerCompiler = LoadSettingBool(settingsIni, "Logging", "LogModMakerCompiler", false);

            ModLibraryPath = LoadSettingString(settingsIni, "ModLibrary", "LibraryPath", null);

            DeveloperMode = LoadSettingBool(settingsIni, "UI", "DeveloperMode", false);
            DarkTheme = LoadSettingBool(settingsIni, "UI", "DarkTheme", false);

            ConfigureNXMHandlerOnBoot = LoadSettingBool(settingsIni, "ModManager", "ConfigureNXMHandlerOnBoot", true);
            DoubleClickModInstall = LoadSettingBool(settingsIni, "ModManager", "DoubleClickModInstall", false);

            SSUILoadAllSaves = LoadSettingBool(settingsIni, "SaveSelector", "SSUILoadAllSaves", false);

            // LEGENDARY
            SkipLELauncher = LoadSettingBool(settingsIni, "ModManager", "SkipLELauncher", true);
            EnableLE1CoalescedMerge = LoadSettingBool(settingsIni, "ModManager", "EnableLE1CoalescedMerge", true);
            GenerationSettingLE = LoadSettingBool(settingsIni, "ModManager", "GenerationSettingLE", true);
            GenerationSettingOT = LoadSettingBool(settingsIni, "ModManager", "GenerationSettingOT", true);

            SelectedLE1LaunchOption = LoadSettingGuid(settingsIni, "ModManager", "SelectedLE1LaunchOption", Guid.Empty);
            SelectedLE2LaunchOption = LoadSettingGuid(settingsIni, "ModManager", "SelectedLE2LaunchOption", Guid.Empty);
            SelectedLE3LaunchOption = LoadSettingGuid(settingsIni, "ModManager", "SelectedLE3LaunchOption", Guid.Empty);

            // Debugging options - these have no UI.
            UseOptimizedTextureRestore = LoadSettingBool(settingsIni, "ModManagerDebug", "UseOptimizedTextureRestore", true);
            EnableTextureSafetyChecks = LoadSettingBool(settingsIni, "ModManagerDebug", "EnableTextureSafetyChecks", true);

            // Dismiss messages
            OneTimeMessage_ModListIsNotListOfInstalledMods = LoadSettingBool(settingsIni, "ModManager", "ShowModListNotInstalledModsMessage", true);


            Loaded = true;
        }

        private static Guid LoadSettingGuid(IniData ini, string section, string key, Guid defaultValue)
        {
            // This is stored as string but is parsed on load
            if (ini == null) return defaultValue;
            var value = LoadSettingString(ini, section, key, null);
            if (value == null) return defaultValue;
            if (Guid.TryParse(value, out var result))
            {
                return result;
            }

            return defaultValue;
        }

        private static bool LoadSettingBool(IniData ini, string section, string key, bool defaultValue)
        {
            if (ini == null) return defaultValue;
            if (bool.TryParse(ini[section][key], out var boolValue))
            {
                if (boolValue != defaultValue)
                {
                    LogSettingChanging(key, boolValue);
                }
                return boolValue;
            }

            return defaultValue;
        }

        private static string LoadSettingString(IniData ini, string section, string key, string defaultValue)
        {
            if (ini == null) return defaultValue;

            if (string.IsNullOrEmpty(ini[section][key]))
            {
                return defaultValue;
            }

            if (ini[section][key] != defaultValue)
            {
                LogSettingChanging(key, ini[section][key]);
            }
            return ini[section][key];
        }

        private static DateTime LoadSettingDateTime(IniData ini, string section, string key, DateTime defaultValue)
        {
            if (ini == null) return defaultValue;

            if (string.IsNullOrEmpty(ini[section][key])) return defaultValue;
            try
            {
                if (long.TryParse(ini[section][key], out var dateLong))
                {
                    var dt = DateTime.FromBinary(dateLong);
                    if (!dt.Equals(defaultValue))
                    {
                        LogSettingChanging(key, dt);
                    }
                    return dt;
                }
            }
            catch (Exception)
            {
            }
            return defaultValue;
        }

        private static int LoadSettingInt(IniData ini, string section, string key, int defaultValue)
        {
            if (ini == null) return defaultValue;

            if (int.TryParse(ini[section][key], out var intValue))
            {
                if (intValue != defaultValue) { LogSettingChanging(key, intValue); }
                return intValue;
            }
            else
            {
                return defaultValue;
            }
        }

        public static void SaveUpdaterServiceEncryptedValues(string entropy, string encryptedPW)
        {
            IniData settingsIni = new IniData();
            if (File.Exists(SettingsPath))
            {
                settingsIni = new FileIniDataParser().ReadFile(SettingsPath);
            }
            SaveSettingString(settingsIni, "UpdaterService", "Entropy", entropy);
            SaveSettingString(settingsIni, "UpdaterService", "EncryptedPassword", encryptedPW);
            try
            {
                File.WriteAllText(SettingsPath, settingsIni.ToString());
            }
            catch (Exception e)
            {
                M3Log.Error("Error commiting settings: " + App.FlattenException(e));
            }
        }

        public static string DecryptUpdaterServicePassword()
        {

            IniData settingsIni = new IniData();
            if (File.Exists(SettingsPath))
            {
                settingsIni = new FileIniDataParser().ReadFile(SettingsPath);
            }

            var entropy = LoadSettingString(settingsIni, "UpdaterService", "Entropy", null);
            var encryptedPW = LoadSettingString(settingsIni, "UpdaterService", "EncryptedPassword", null);

            if (entropy != null && encryptedPW != null)
            {
                try
                {
                    using MemoryStream fs = new MemoryStream(Convert.FromBase64String(encryptedPW));
                    return Encoding.Unicode.GetString(NexusModsUtilities.DecryptDataFromStream(Convert.FromBase64String(entropy), DataProtectionScope.CurrentUser, fs, (int)fs.Length));
                }
                catch (Exception e)
                {
                    M3Log.Error(@"Can't decrypt ME3Tweaks Updater Service password: " + e.Message);
                }
            }
            return null; //No password
        }

        public enum SettingsSaveResult
        {
            SAVED,
            FAILED_UNAUTHORIZED,
            FAILED_OTHER,
        }

        /// <summary>
        /// Saves the settings. Note this does not update the Updates/EncryptedPassword value. Returns false if commiting failed
        /// </summary>
        private static SettingsSaveResult Save()
        {
            Debug.WriteLine(@"Saving settings");
            try
            {
                var settingsIni = new IniData();
                if (File.Exists(SettingsPath))
                {
                    settingsIni = new FileIniDataParser().ReadFile(SettingsPath); // Read file in so we don't lose any settings
                }

                SaveSettingBool(settingsIni, "Logging", "LogModStartup", LogModStartup);
                SaveSettingBool(settingsIni, "Logging", "LogMixinStartup", LogMixinStartup);
                SaveSettingBool(settingsIni, "Logging", "LogModUpdater", LogModUpdater);
                SaveSettingBool(settingsIni, "Logging", "LogBackupAndRestore", LogBackupAndRestore);

                SaveSettingBool(settingsIni, "Logging", "LogModMakerCompiler", LogModMakerCompiler);
                SaveSettingBool(settingsIni, "Logging", "EnableTelemetry", EnableTelemetry);
                SaveSettingString(settingsIni, "UpdaterService", "Username", UpdaterServiceUsername);
                SaveSettingString(settingsIni, "UpdaterService", "LZMAStoragePath", UpdaterServiceLZMAStoragePath);
                SaveSettingString(settingsIni, "UpdaterService", "ManifestStoragePath", UpdaterServiceManifestStoragePath);
                SaveSettingString(settingsIni, "UpdaterService", "ManifestStoragePath", UpdaterServiceManifestStoragePath);
                SaveSettingString(settingsIni, "UpdaterService", "ManifestStoragePath", UpdaterServiceManifestStoragePath);
                SaveSettingBool(settingsIni, "UI", "DeveloperMode", DeveloperMode);
                SaveSettingBool(settingsIni, "UI", "DarkTheme", DarkTheme);
                SaveSettingBool(settingsIni, "Logging", "LogModInstallation", LogModInstallation);
                SaveSettingString(settingsIni, "ModLibrary", "LibraryPath", ModLibraryPath);
                SaveSettingString(settingsIni, "ModManager", "Language", Language);
                SaveSettingString(settingsIni, "ModManager", "LastSelectedTarget", LastSelectedTarget);
                SaveSettingDateTime(settingsIni, "ModManager", "LastContentCheck", LastContentCheck);
                SaveSettingBool(settingsIni, "ModManager", "BetaMode", BetaMode);
                SaveSettingBool(settingsIni, "ModManager", "ShowedPreviewMessage2", ShowedPreviewPanel);
                SaveSettingBool(settingsIni, "ModManager", "AutoUpdateLODs4K", AutoUpdateLODs4K);
                SaveSettingBool(settingsIni, "ModManager", "AutoUpdateLODs2K", AutoUpdateLODs2K);
                SaveSettingBool(settingsIni, "ModManager", "PreferCompressingPackages", PreferCompressingPackages);
                SaveSettingInt(settingsIni, "ModManager", "WebclientTimeout", WebClientTimeout);
                SaveSettingBool(settingsIni, "ModManager", "ConfigureNXMHandlerOnBoot", ConfigureNXMHandlerOnBoot);
                SaveSettingBool(settingsIni, "ModManager", "SkipLELauncher", SkipLELauncher);
                SaveSettingBool(settingsIni, "ModManager", "GenerationSettingOT", GenerationSettingOT);
                SaveSettingBool(settingsIni, "ModManager", "GenerationSettingLE", GenerationSettingLE);
                SaveSettingString(settingsIni, "ModManager", "SelectedFilters", SelectedFilters);
                SaveSettingBool(settingsIni, "ModManager", "DoubleClickModInstall", DoubleClickModInstall);
                SaveSettingBool(settingsIni, "ModManager", "ShowModListNotInstalledModsMessage", OneTimeMessage_ModListIsNotListOfInstalledMods);
                SaveSettingString(settingsIni, "ModManager", "ModDownloadCacheFolder", ModDownloadCacheFolder);
                SaveSettingGuid(settingsIni, "ModManager", "SelectedLE1LaunchOption", SelectedLE1LaunchOption);
                SaveSettingGuid(settingsIni, "ModManager", "SelectedLE2LaunchOption", SelectedLE2LaunchOption);
                SaveSettingGuid(settingsIni, "ModManager", "SelectedLE3LaunchOption", SelectedLE3LaunchOption);

                SaveSettingBool(settingsIni, "ModManager", "EnableLE1CoalescedMerge", EnableLE1CoalescedMerge);

                SaveSettingBool(settingsIni, "ModMaker", "AutoAddControllerMixins", ModMakerControllerModOption);
                SaveSettingBool(settingsIni, "ModMaker", "AutoInjectCustomKeybinds", ModMakerAutoInjectCustomKeybindsOption);

                // Save Selector
                SaveSettingBool(settingsIni, "SaveSelector", "SSUILoadAllSaves", SSUILoadAllSaves);

                // Debug options
                SaveSettingBool(settingsIni, "ModManagerDebug", "UseOptimizedTextureRestore", UseOptimizedTextureRestore);
                SaveSettingBool(settingsIni, "ModManagerDebug", "EnableTextureSafetyChecks", EnableTextureSafetyChecks);

                File.WriteAllText(SettingsPath, settingsIni.ToString());
                return SettingsSaveResult.SAVED;
            }
            catch (UnauthorizedAccessException uae)
            {
                M3Log.Error("Unauthorized access exception: " + App.FlattenException(uae));
                return SettingsSaveResult.FAILED_UNAUTHORIZED;
            }
            catch (Exception e)
            {
                M3Log.Error("Error commiting settings: " + App.FlattenException(e));
            }

            return SettingsSaveResult.FAILED_OTHER;
        }

        private static void SaveSettingString(IniData settingsIni, string section, string key, string value)
        {
            settingsIni[section][key] = value;
        }

        private static void SaveSettingGuid(IniData settingsIni, string section, string key, Guid value)
        {
            settingsIni[section][key] = value.ToString();
        }

        private static void SaveSettingBool(IniData settingsIni, string section, string key, bool value)
        {
            settingsIni[section][key] = value.ToString();
        }

        private static void SaveSettingInt(IniData settingsIni, string section, string key, int value)
        {
            settingsIni[section][key] = value.ToString();
        }

        private static void SaveSettingDateTime(IniData settingsIni, string section, string key, DateTime value)
        {
            settingsIni[section][key] = value.ToBinary().ToString();
        }

        /// <summary>
        /// Tests saving the settings and returns the save result.
        /// </summary>
        /// <returns></returns>
        public static SettingsSaveResult SaveTest()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(SettingsPath);
                    if (bytes.Length > 0 && bytes.Sum(x => x) == 0)
                    {
                        // all bytes are zero. The ini file has become corrupt. 
                        // This issue has been observed but not replicated
                        // Delete the settings.ini
                        App.SubmitAnalyticTelemetryEvent(@"Corrupted settings.ini detected",
                            new Dictionary<string, string>()
                            {
                                {@"Filesize", bytes.Length.ToString()}
                            });
                        M3Log.Fatal(
                            @"DETECTED CORRUPT SETTINGS.INI FILE. This file will be deleted and reset to defaults.");
                        File.Delete(SettingsPath);
                    }
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Unable to test if settings.ini file is corrupted: {e.Message}");
                }
            }

            return Save();
        }
    }
}
