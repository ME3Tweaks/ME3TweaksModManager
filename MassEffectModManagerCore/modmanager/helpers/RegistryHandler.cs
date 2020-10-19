using Microsoft.Win32;

namespace MassEffectModManagerCore.modmanager.helpers
{
    public static class RegistryHandler
    {
        //internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, string data)
        //{

        //    int i = 0;
        //    string[] subkeys = subpath.Split('\\');
        //    while (i < subkeys.Length)
        //    {
        //        subkey = subkey.CreateSubKey(subkeys[i]);
        //        i++;
        //    }
        //    subkey.SetValue(value, data);
        //}

        //internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, bool data)
        //{

        //    WriteRegistryKey(subkey, subpath, value, data ? 1 : 0);
        //}

        /// <summary>
        /// Writes the specified data to the lsited key/subpath/value.
        /// </summary>
        /// <param name="subkey"></param>
        /// <param name="subpath"></param>
        /// <param name="value"></param>
        /// <param name="data"></param>
        //internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, object data)
        //{

        //    int i = 0;
        //    string[] subkeys = subpath.Split('\\');
        //    while (i < subkeys.Length)
        //    {
        //        subkey = subkey.CreateSubKey(subkeys[i]);
        //        i++;
        //    }

        //    if (data is long l)
        //    {
        //        subkey.SetValue(value, data, RegistryValueKind.QWord);
        //    }
        //    else
        //    {
        //        subkey.SetValue(value, data);
        //    }
        //}

        //internal static void WriteRegistrySettingBool(string keyname, bool value)
        //{
        //    WriteRegistryKey(Registry.CurrentUser, "Software\\ALOTAddon", keyname, value);
        //}

        //internal static void WriteRegistrySettingString(string keyname, string value)
        //{
        //    WriteRegistryKey(Registry.CurrentUser, "Software\\ALOTAddon", keyname, value);
        //}

        //internal static void WriteRegistrySettingInt(string keyname, int value)
        //{
        //    WriteRegistryKey(Registry.CurrentUser, "Software\\ALOTAddon", keyname, value);
        //}

        //internal static void WriteRegistrySettingLong(string keyname, long value)
        //{
        //    WriteRegistryKey(Registry.CurrentUser, "Software\\ALOTAddon", keyname, value);
        //}

        //internal static void WriteRegistrySettingFloat(string keyname, float value)
        //{
        //    WriteRegistryKey(Registry.CurrentUser, "Software\\ALOTAddon", keyname, value);
        //}

        /// <summary>
        /// Gets an ALOT registry setting string.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        //public static string GetRegistryString(string name)
        //{
        //    string softwareKey = @"HKEY_CURRENT_USER\Software\ALOTAddon";
        //    return (string)Registry.GetValue(softwareKey, name, null);
        //}

        /// <summary>
        /// Gets a string value from the registry from the specified key and value name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="valueName"></param>
        /// <returns></returns>
        //public static string GetRegistryString(string key, string valueName)
        //{

        //    return (string)Registry.GetValue(key, valueName, null);
        //}

        /// <summary>
        /// Gets a settings value for ALOT Installer from the registry
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        //public static bool? GetRegistrySettingBool(string name)
        //{

        //    string softwareKey = @"HKEY_CURRENT_USER\Software\ALOTAddon";

        //    int? value = (int?)Registry.GetValue(softwareKey, name, null);
        //    if (value != null)
        //    {
        //        return value > 0;
        //    }
        //    return null;
        //}

        /// <summary>
        /// Deletes a registry key from the registry. USE WITH CAUTION
        /// </summary>
        /// <param name="primaryKey"></param>
        /// <param name="subkey"></param>
        /// <param name="valuename"></param>
        public static void DeleteRegistryKey(RegistryKey primaryKey, string subkey, string valuename)
        {
            using RegistryKey key = primaryKey.OpenSubKey(subkey, true);
            key?.DeleteValue(valuename, false);
        }

        //public static long? GetRegistrySettingLong(string name)
        //{
        //    string softwareKey = @"HKEY_CURRENT_USER\Software\ALOTAddon";
        //    return (long?)Registry.GetValue(softwareKey, name, null);
        //}
    }
}
