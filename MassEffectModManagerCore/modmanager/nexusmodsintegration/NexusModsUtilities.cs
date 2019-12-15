using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.AppCenter.Analytics;
using Pathoschild.FluentNexus;
using Pathoschild.FluentNexus.Models;
using Pathoschild.Http.Client;
using Serilog;

namespace MassEffectModManagerCore.modmanager.nexusmodsintegration
{
    [Localizable(false)]
    public class NexusModsUtilities
    {

        private static byte[] CreateRandomEntropy()
        {
            // Create a byte array to hold the random value.
            byte[] entropy = new byte[16];

            // Create a new instance of the RNGCryptoServiceProvider.
            // Fill the array with a random value.
            new RNGCryptoServiceProvider().GetBytes(entropy);

            // Return the array.
            return entropy;
        }

        public static bool EncryptAPIKeyToDisk(string apikey)
        {
            using FileStream fs = new FileStream(Path.Combine(Utilities.GetNexusModsCache(), "nexusmodsapikey"), FileMode.Create);
            byte[] bytes = Encoding.Unicode.GetBytes(apikey);
            byte[] entropy = CreateRandomEntropy();
            File.WriteAllBytes(Path.Combine(Utilities.GetNexusModsCache(), "entropy"), entropy);
            int byteswritten = EncryptDataToStream(bytes, entropy, DataProtectionScope.CurrentUser, fs);
            return byteswritten > 0;
        }

        /// <summary>
        /// Gets user information from NexusMods using their API key. This should only be called when setting API key or app boot
        /// </summary>
        /// <param name="apiKey"></param>
        /// <returns></returns>
        public static async Task<User> AuthToNexusMods(string apiKey = null)
        {
            try
            {
                var nexus = GetClient(apiKey);
                Log.Information("Getting user information from NexusMods");
                var userinfo = await nexus.Users.ValidateAsync();
                if (userinfo.Name != null)
                {
                    Log.Information("API call returned valid data. API key is valid");

                    //Authorized OK.

                    //Track how many users authenticate to nexusmods, but don't track who.
                    return userinfo;
                }
            }
            catch (Exception e)
            {
                Log.Error(@"Exception while authenticating to nexusmods: "+e.Message);
            }

            return null;
        }

        public static bool HasAPIKey => File.Exists(Path.Combine(Utilities.GetNexusModsCache(), "nexusmodsapikey"));

        public static bool WipeKeys()
        {
            var keyPath = Path.Combine(Utilities.GetNexusModsCache(), "nexusmodsapikey");
            var entropyf = Path.Combine(Utilities.GetNexusModsCache(), "entropy");
            if (File.Exists(keyPath)) File.Delete(keyPath);
            if (File.Exists(entropyf)) File.Delete(entropyf);
            return true;
        }

        public static string DecryptNexusmodsAPIKeyFromDisk()
        {
            var keyPath = Path.Combine(Utilities.GetNexusModsCache(), "nexusmodsapikey");
            var entropyf = Path.Combine(Utilities.GetNexusModsCache(), "entropy");
            if (File.Exists(keyPath) && File.Exists(entropyf))
            {
                var entropy = File.ReadAllBytes(entropyf);
                using MemoryStream fs = new MemoryStream(File.ReadAllBytes(keyPath));
                return Encoding.Unicode.GetString(DecryptDataFromStream(entropy, DataProtectionScope.CurrentUser, fs, (int)fs.Length));
            }

            return null; //no key
        }

        /// <summary>
        /// Gets a nexusmods client. If no API is available this returns null
        /// </summary>
        /// <param name="apikey"></param>
        /// <returns></returns>
        internal static NexusClient GetClient(string apikey = null)
        {
            string key = apikey ?? DecryptNexusmodsAPIKeyFromDisk();
            if (key == null) return null; //no key to use
            return new NexusClient(key, "ME3Tweaks Mod Manager", App.BuildNumber.ToString());
        }

        public static async Task<bool?> GetEndorsementStatusForFile(string gamedomain, int fileid, int currentuserid)
        {
            if (!NexusModsUtilities.HasAPIKey) return false;
            var client = NexusModsUtilities.GetClient();
            var modinfo = await client.Mods.GetMod(gamedomain, fileid);
            if (modinfo.User.MemberID == currentuserid)
            {
                return null; //cannot endorse your own mods
            }
            var endorsementstatus = modinfo.Endorsement;
            if (endorsementstatus != null)
            {
                if (endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Undecided || endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Abstained)
                {
                    return false;
                }

                if (endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Endorsed)
                {
                    return true;
                }
            }
            return null; //Cannot endorse this (could not get endorsement status)
        }

        /// <summary>
        /// Asynchronously endorses a file. This call does not wait for a result of the operation.
        /// </summary>
        /// <param name="gamedomain"></param>
        /// <param name="endorse"></param>
        /// <param name="fileid"></param>
        /// <param name="currentuserid"></param>
        public static void EndorseFile(string gamedomain, bool endorse, int fileid, int currentuserid, Action<bool> newEndorsementStatusCallback = null)
        {
            if (!NexusModsUtilities.HasAPIKey) return;
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (a, b) =>
            {
                var client = NexusModsUtilities.GetClient();
                string telemetryOverride = null;
                try
                {
                    if (endorse)
                    {
                        client.Mods.Endorse(gamedomain, fileid, @"1.0").Wait();
                    }
                    else
                    {
                        client.Mods.Unendorse(gamedomain, fileid, @"1.0").Wait();
                    }

                }
                catch (Exception e)
                {
                    Log.Error(@"Error endorsing/unendorsing: " + e.ToString());
                    telemetryOverride = e.ToString();
                }

                var newStatus = GetEndorsementStatusForFile(gamedomain, fileid, currentuserid).Result;

                Analytics.TrackEvent(@"Set endorsement for mod", new Dictionary<string, string>
                {
                    {@"Endorsed", endorse.ToString() },
                    {@"Succeeded", telemetryOverride ?? (endorse == newStatus).ToString() }
                });
                b.Result = newStatus;
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Result is bool val)
                {
                    newEndorsementStatusCallback?.Invoke(val);
                }
            };
            bw.RunWorkerAsync();
        }


        private static int EncryptDataToStream(byte[] Buffer, byte[] Entropy, DataProtectionScope Scope, Stream S)
        {
            if (Buffer == null)
                throw new ArgumentNullException("Buffer");
            if (Buffer.Length <= 0)
                throw new ArgumentException("Buffer");
            if (Entropy == null)
                throw new ArgumentNullException("Entropy");
            if (Entropy.Length <= 0)
                throw new ArgumentException("Entropy");
            if (S == null)
                throw new ArgumentNullException("S");

            int length = 0;

            // Encrypt the data and store the result in a new byte array. The original data remains unchanged.
            byte[] encryptedData = ProtectedData.Protect(Buffer, Entropy, Scope);

            // Write the encrypted data to a stream.
            if (S.CanWrite && encryptedData != null)
            {
                S.Write(encryptedData, 0, encryptedData.Length);

                length = encryptedData.Length;
            }

            // Return the length that was written to the stream. 
            return length;

        }

        private static byte[] DecryptDataFromStream(byte[] Entropy, DataProtectionScope Scope, Stream S, int Length)
        {
            if (S == null)
                throw new ArgumentNullException("S");
            if (Length <= 0)
                throw new ArgumentException("Length");
            if (Entropy == null)
                throw new ArgumentNullException("Entropy");
            if (Entropy.Length <= 0)
                throw new ArgumentException("Entropy");



            byte[] inBuffer = new byte[Length];
            byte[] outBuffer;

            // Read the encrypted data from a stream.
            if (S.CanRead)
            {
                S.Read(inBuffer, 0, Length);

                outBuffer = ProtectedData.Unprotect(inBuffer, Entropy, Scope);
            }
            else
            {
                throw new IOException("Could not read the stream.");
            }

            // Return the length that was written to the stream. 
            return outBuffer;

        }
    }
}
