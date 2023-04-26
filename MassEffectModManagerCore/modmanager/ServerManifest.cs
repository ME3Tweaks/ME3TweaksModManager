using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;

namespace ME3TweaksModManager.modmanager
{
    /// <summary>
    /// Class for interacting with the ME3Tweaks Mod Manager server manifest
    /// </summary>
    public class ServerManifest
    {
        // MANIFEST KEYS
        private const string SERVER_ALIGNMENT = @"serveralignment";
        public static bool HasManifest => _serverManifest != null;

        private static CaseInsensitiveDictionary<object> _serverManifest { get; set; }

        /// <summary>
        /// Fetches a string value from the server manifest
        /// </summary>
        /// <param name="key"></param>
        /// <returns>null if not found</returns>
        public static string GetString(string key)
        {
            if (_serverManifest == null) return null;
            if (_serverManifest.TryGetValue(key, out object value) && value is string strval)
            {
                return strval;
            }

            // Key not found or of wrong type
            return null;
        }

        /// <summary>
        /// Fetches a int value from the server manifest
        /// </summary>
        /// <param name="key">manifest key to fetch</param>
        /// <returns>null if not found</returns>
        public static int? GetInt(string key)
        {
            if (_serverManifest == null) return null;
            if (_serverManifest.TryGetValue(key, out object value) && value is int intValue)
            {
                return intValue;
            }

            // Key not found or of wrong type
            return null;
        }
    }
}
