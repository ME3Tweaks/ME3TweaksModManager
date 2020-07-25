using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using IniParser;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.nexusmodsintegration;
using Serilog;

namespace MassEffectModManagerCore.modmanager
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
            if (Loaded) Save();
            return true;
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

        private static int _webclientTimeout = 5; // Defaults to 5
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

        private static bool _autoUpdateLods = true;
        public static bool AutoUpdateLODs
        {
            get => _autoUpdateLods;
            set => SetProperty(ref _autoUpdateLods, value);
        }




        private static string _modLibraryPath;
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

        public static DateTime LastContentCheck { get; internal set; }

        private static bool _showedPreviewPanel;
        public static bool ShowedPreviewPanel
        {
            get => _showedPreviewPanel;
            set => SetProperty(ref _showedPreviewPanel, value);
        }

        private static bool _logModMakerCompiler;
        public static bool LogModMakerCompiler
        {
            get => _logModMakerCompiler;
            set => SetProperty(ref _logModMakerCompiler, value);
        }

        public static readonly string SettingsPath = Path.Combine(Utilities.GetAppDataFolder(), "settings.ini");

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
                Log.Error("Error reading settings.ini file: " + e.Message);
                Log.Error("Mod Manager will use the defaults instead");
            }
            ShowedPreviewPanel = LoadSettingBool(settingsIni, "ModManager", "ShowedPreviewMessage2", false);
            Language = LoadSettingString(settingsIni, "ModManager", "Language", "int");
            LastContentCheck = LoadSettingDateTime(settingsIni, "ModManager", "LastContentCheck", DateTime.MinValue);
            BetaMode = LoadSettingBool(settingsIni, "ModManager", "BetaMode", false);
            AutoUpdateLODs = LoadSettingBool(settingsIni, "ModManager", "AutoUpdateLODs", true);
            WebClientTimeout = LoadSettingInt(settingsIni, "ModManager", "WebclientTimeout", 5);
            ModMakerControllerModOption = LoadSettingBool(settingsIni, "ModMaker", "AutoAddControllerMixins", false);
            ModMakerAutoInjectCustomKeybindsOption = LoadSettingBool(settingsIni, "ModMaker", "AutoInjectCustomKeybinds", false);


            UpdaterServiceUsername = LoadSettingString(settingsIni, "UpdaterService", "Username", null);
            UpdaterServiceLZMAStoragePath = LoadSettingString(settingsIni, "UpdaterService", "LZMAStoragePath", null);
            UpdaterServiceManifestStoragePath = LoadSettingString(settingsIni, "UpdaterService", "ManifestStoragePath", null);

            LogModStartup = LoadSettingBool(settingsIni, "Logging", "LogModStartup", false);
            LogMixinStartup = LoadSettingBool(settingsIni, "Logging", "LogMixinStartup", false);
            EnableTelemetry = LoadSettingBool(settingsIni, "Logging", "EnableTelemetry", true);
            LogModInstallation = LoadSettingBool(settingsIni, "Logging", "LogModInstallation", false);
            LogModMakerCompiler = LoadSettingBool(settingsIni, "Logging", "LogModMakerCompiler", false);

            ModLibraryPath = LoadSettingString(settingsIni, "ModLibrary", "LibraryPath", null);

            DeveloperMode = LoadSettingBool(settingsIni, "UI", "DeveloperMode", false);
            DarkTheme = LoadSettingBool(settingsIni, "UI", "DarkTheme", false);
            Loaded = true;
        }

        private static bool LoadSettingBool(IniData ini, string section, string key, bool defaultValue)
        {
            if (ini == null) return defaultValue;
            if (bool.TryParse(ini[section][key], out var boolValue))
            {
                return boolValue;
            }
            else
            {
                return defaultValue;
            }
        }

        private static string LoadSettingString(IniData ini, string section, string key, string defaultValue)
        {
            if (ini == null) return defaultValue;

            if (string.IsNullOrEmpty(ini[section][key]))
            {
                return defaultValue;
            }
            else
            {
                return ini[section][key];
            }
        }

        private static DateTime LoadSettingDateTime(IniData ini, string section, string key, DateTime defaultValue)
        {
            if (ini == null) return defaultValue;

            if (string.IsNullOrEmpty(ini[section][key])) return defaultValue;
            try
            {
                if (long.TryParse(ini[section][key], out var dateLong))
                {
                    return DateTime.FromBinary(dateLong);
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
                return intValue;
            }
            else
            {
                return defaultValue;
            }
        }

        public static void SaveUpdaterServiceEncryptedValues(string entropy, string encryptedPW)
        {
            if (!File.Exists(SettingsPath))
            {
                File.Create(SettingsPath).Close();
            }

            var settingsIni = new FileIniDataParser().ReadFile(SettingsPath);
            SaveSettingString(settingsIni, "UpdaterService", "Entropy", entropy);
            SaveSettingString(settingsIni, "UpdaterService", "EncryptedPassword", encryptedPW);
            try
            {
                File.WriteAllText(SettingsPath, settingsIni.ToString());
            }
            catch (Exception e)
            {
                Log.Error("Error commiting settings: " + App.FlattenException(e));
            }
        }

        public static string DecryptUpdaterServicePassword()
        {
            if (!File.Exists(SettingsPath))
            {
                File.Create(SettingsPath).Close();
            }

            var settingsIni = new FileIniDataParser().ReadFile(SettingsPath);
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
                    Log.Error(@"Can't decrypt ME3Tweaks Updater Service password: " + e.Message);
                }
            }
            return null; //No password
        }

        public enum SettingsSaveResult
        {
            SAVED,
            FAILED_UNAUTHORIZED,
            FAILED_OTHER
        }

        /// <summary>
        /// Saves the settings. Note this does not update the Updates/EncryptedPassword value. Returns false if commiting failed
        /// </summary>
        public static SettingsSaveResult Save()
        {
            try
            {
                // ... why?
                //if (!File.Exists(SettingsPath))
                //{
                //    File.Create(SettingsPath).Close();
                //}

                var settingsIni = new FileIniDataParser().ReadFile(SettingsPath);
                SaveSettingBool(settingsIni, "Logging", "LogModStartup", LogModStartup);
                SaveSettingBool(settingsIni, "Logging", "LogMixinStartup", LogMixinStartup);
                SaveSettingBool(settingsIni, "Logging", "LogModMakerCompiler", LogModMakerCompiler);
                SaveSettingBool(settingsIni, "Logging", "EnableTelemetry", EnableTelemetry);
                SaveSettingString(settingsIni, "UpdaterService", "Username", UpdaterServiceUsername);
                SaveSettingString(settingsIni, "UpdaterService", "LZMAStoragePath", UpdaterServiceLZMAStoragePath);
                SaveSettingString(settingsIni, "UpdaterService", "ManifestStoragePath", UpdaterServiceManifestStoragePath);
                SaveSettingBool(settingsIni, "UI", "DeveloperMode", DeveloperMode);
                SaveSettingBool(settingsIni, "UI", "DarkTheme", DarkTheme);
                SaveSettingBool(settingsIni, "Logging", "LogModInstallation", LogModInstallation);
                SaveSettingString(settingsIni, "ModLibrary", "LibraryPath", ModLibraryPath);
                SaveSettingString(settingsIni, "ModManager", "Language", Language);
                SaveSettingDateTime(settingsIni, "ModManager", "LastContentCheck", LastContentCheck);
                SaveSettingBool(settingsIni, "ModManager", "BetaMode", BetaMode);
                SaveSettingBool(settingsIni, "ModManager", "ShowedPreviewMessage2", ShowedPreviewPanel);
                SaveSettingBool(settingsIni, "ModManager", "AutoUpdateLODs", AutoUpdateLODs);
                SaveSettingInt(settingsIni, "ModManager", "WebclientTimeout", WebClientTimeout);
                SaveSettingBool(settingsIni, "ModMaker", "AutoAddControllerMixins", ModMakerControllerModOption);
                SaveSettingBool(settingsIni, "ModMaker", "AutoInjectCustomKeybinds", ModMakerAutoInjectCustomKeybindsOption);

                File.WriteAllText(SettingsPath, settingsIni.ToString());
                return SettingsSaveResult.SAVED;
            }
            catch (UnauthorizedAccessException uae)
            {
                Log.Error("Unauthorized access exception: " + App.FlattenException(uae));
                return SettingsSaveResult.FAILED_UNAUTHORIZED;
            }
            catch (Exception e)
            {
                Log.Error("Error commiting settings: " + App.FlattenException(e));
            }

            return SettingsSaveResult.FAILED_OTHER;
        }

        private static void SaveSettingString(IniData settingsIni, string section, string key, string value)
        {
            settingsIni[section][key] = value;
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
    }
}
