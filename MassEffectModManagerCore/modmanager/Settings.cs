using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using IniParser;
using IniParser.Model;
using IniParser.Parser;
using MassEffectModManager;
using Microsoft.VisualBasic;
using Serilog;

namespace MassEffectModManagerCore.modmanager
{
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

        

        private static string _modLibraryPath;
        public static string ModLibraryPath
        {
            get => _modLibraryPath;
            set => SetProperty(ref _modLibraryPath, value);
        }
        public static DateTime LastContentCheck { get; internal set; }

        private static string SettingsPath = Path.Combine(Utilities.GetAppDataFolder(), "settings.ini");
        public static void Load()
        {
            if (!File.Exists(SettingsPath))
            {
                File.Create(SettingsPath).Close();
            }

            var settingsIni = new FileIniDataParser().ReadFile(SettingsPath);
            LogModStartup = LoadSettingBool(settingsIni, "Logging", "LogModStartup", false);
            LogMixinStartup = LoadSettingBool(settingsIni, "Logging", "LogMixinStartup", false);
            LogModInstallation = LoadSettingBool(settingsIni, "Logging", "LogModInstallation", false);
            ModLibraryPath = LoadSettingString(settingsIni, "ModLibrary", "LibraryPath", null);
            LastContentCheck = LoadSettingDateTime(settingsIni, "ModManager", "LastContentCheck", DateTime.MinValue);
            Loaded = true;
        }

        private static bool LoadSettingBool(IniData ini, string section, string key, bool defaultValue)
        {
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
            if (int.TryParse(ini[section][key], out var intValue))
            {
                return intValue;
            }
            else
            {
                return defaultValue;
            }
        }

        public static void Save()
        {
            //implement later
            if (!File.Exists(SettingsPath))
            {
                File.Create(SettingsPath).Close();
            }

            var settingsIni = new FileIniDataParser().ReadFile(SettingsPath);
            SaveSettingBool(settingsIni, "Logging", "LogModStartup", LogModStartup);
            SaveSettingBool(settingsIni, "Logging", "LogMixinStartup", LogMixinStartup);
            SaveSettingBool(settingsIni, "Logging", "LogModInstallation", LogModInstallation);
            SaveSettingString(settingsIni, "ModLibrary", "LibraryPath", ModLibraryPath);
            SaveSettingDateTime(settingsIni, "ModManager", "LastContentCheck", LastContentCheck);
            try
            {
                File.WriteAllText(SettingsPath, settingsIni.ToString());
            }
            catch (Exception e)
            {
                Log.Error("Error commiting settings: " + App.FlattenException(e));
            }
        }

        private static void SaveSettingString(IniData settingsIni, string section, string key, string value)
        {
            settingsIni[section][key] = value;
        }

        private static void SaveSettingBool(IniData settingsIni, string section, string key, bool value)
        {
            settingsIni[section][key] = value.ToString();
        }

        private static void SaveSettingDateTime(IniData settingsIni, string section, string key, DateTime value)
        {
            settingsIni[section][key] = value.ToBinary().ToString();
        }
    }
}
