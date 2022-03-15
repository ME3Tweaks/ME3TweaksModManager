using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects;
using Microsoft.AppCenter.Analytics;
using Microsoft.Win32;
using Pathoschild.FluentNexus;
using Pathoschild.FluentNexus.Models;
using WatsonWebsocket;

namespace ME3TweaksModManager.modmanager.nexusmodsintegration
{
    [Localizable(false)]
    public class NexusModsUtilities
    {

        public static readonly string[] AllSupportedNexusDomains = { @"masseffect", @"masseffect2", @"masseffect3", @"masseffectlegendaryedition" };


        /// <summary>
        /// User information for the authenticated NexusMods user
        /// </summary>
        public static User UserInfo { get; private set; }


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

        public static byte[] EncryptStringToStream(string secret, Stream outstream)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(secret);
            byte[] entropy = CreateRandomEntropy();
            int byteswritten = EncryptDataToStream(bytes, entropy, DataProtectionScope.CurrentUser, outstream);
            return entropy;
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
                if (apiKey == null)
                {
                    if (NexusModsUtilities.HasAPIKey)
                    {
                        apiKey = NexusModsUtilities.DecryptNexusmodsAPIKeyFromDisk();
                    }

                    if (apiKey == null) return null;
                }

                var nexus = GetClient(apiKey);
                M3Log.Information("Getting user information from NexusMods");
                var userinfo = await nexus.Users.ValidateAsync();
                if (userinfo.Name != null)
                {
                    M3Log.Information("NexusMods API call returned valid data. API key is valid");
                    UserInfo = userinfo;
                    //Authorized OK.

                    //Track how many users authenticate to nexusmods, but don't track who.
                    return userinfo;
                }
            }
            catch (Exception e)
            {
                M3Log.Error(@"Exception while authenticating to nexusmods: " + e.Message);
            }

            return null;
        }

        public static bool HasAPIKey => File.Exists(Path.Combine(M3Filesystem.GetNexusModsCache(), "nexusmodsapikey"));

        /// <summary>
        /// Wipes the nexus keys from storage and removes the UserInfo variable
        /// </summary>
        /// <returns></returns>
        public static bool WipeKeys()
        {
            UserInfo = null;
            var keyPath = Path.Combine(M3Filesystem.GetNexusModsCache(), "nexusmodsapikey");
            var entropyf = Path.Combine(M3Filesystem.GetNexusModsCache(), "entropy");
            if (File.Exists(keyPath)) File.Delete(keyPath);
            if (File.Exists(entropyf)) File.Delete(entropyf);
            return true;
        }

        public static string DecryptNexusmodsAPIKeyFromDisk()
        {
            var keyPath = Path.Combine(M3Filesystem.GetNexusModsCache(), "nexusmodsapikey");
            var entropyf = Path.Combine(M3Filesystem.GetNexusModsCache(), "entropy");
            if (File.Exists(keyPath) && File.Exists(entropyf))
            {
                var entropy = File.ReadAllBytes(entropyf);
                using MemoryStream fs = new MemoryStream(File.ReadAllBytes(keyPath));
                return Encoding.Unicode.GetString(DecryptDataFromStream(entropy, DataProtectionScope.CurrentUser, fs,
                    (int)fs.Length));
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

        public static async Task<bool?> GetEndorsementStatusForFile(string gamedomain, int fileid)
        {
            if (UserInfo == null) return false;
            var client = NexusModsUtilities.GetClient();
            try
            {
                var modinfo = await client.Mods.GetMod(gamedomain, fileid);
                if (modinfo.User.MemberID == UserInfo.UserID)
                {
                    return null; //cannot endorse your own mods
                }

                var endorsementstatus = modinfo.Endorsement;
                if (endorsementstatus != null)
                {
                    if (endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Undecided ||
                        endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Abstained)
                    {
                        return false;
                    }

                    if (endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Endorsed)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error getting endorsement status for mod: " + e.Message);
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
        public static void EndorseFile(string gamedomain, bool endorse, int fileid,
            Action<bool> newEndorsementStatusCallback = null)
        {
            if (NexusModsUtilities.UserInfo == null) return;
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"EndorseMod");
            nbw.DoWork += (a, b) =>
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
                    M3Log.Error(@"Error endorsing/unendorsing: " + e.ToString());
                    telemetryOverride = e.ToString();
                }

                var newStatus = GetEndorsementStatusForFile(gamedomain, fileid).Result;

                Analytics.TrackEvent(@"Set endorsement for mod", new Dictionary<string, string>
                {
                    {@"Endorsed", endorse.ToString()},
                    {@"Succeeded", telemetryOverride ?? (endorse == newStatus).ToString()}
                });
                b.Result = newStatus;
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    M3Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }

                if (b.Result is bool val)
                {
                    newEndorsementStatusCallback?.Invoke(val);
                }
            };
            nbw.RunWorkerAsync();
        }

        public static int EncryptDataToStream(byte[] Buffer, byte[] Entropy, DataProtectionScope Scope, Stream S)
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

        public static byte[] DecryptDataFromStream(byte[] Entropy, DataProtectionScope Scope, Stream S, int Length)
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

        /// <summary>
        /// Gets a list of download links for the specified file
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="modid"></param>
        /// <param name="fileid"></param>
        /// <returns></returns>
        public static async Task<ModFileDownloadLink[]> GetDownloadLinkForFile(string domain, int modid, int fileid,
            string nxmkey = null, int expiry = 0)
        {
            if (nxmkey == null)
                return await NexusModsUtilities.GetClient().ModFiles.GetDownloadLinks(domain, modid, fileid);
            return await NexusModsUtilities.GetClient().ModFiles
                .GetDownloadLinks(domain, modid, fileid, nxmkey, expiry);
        }

        public static async Task<ModFileDownloadLink[]> GetDownloadLinkForFile(NexusProtocolLink link)
        {
            return await GetDownloadLinkForFile(link.Domain, link.ModId, link.FileId, link.Key, link.KeyExpiry);
        }

        public static void SetupNXMHandling()
        {
            // Register app
            M3Log.Information(@"Registering this application as the nxm:// handler");
            using var subkey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Classes\nxm\shell\open\command");
            subkey.SetValue("", $"\"{App.ExecutableLocation}\" --nxmlink \"%1\"");

            // Register protocol
            var protocolKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Classes\nxm", true);
            protocolKey.SetValue("URL Protocol", "");
            protocolKey.SetValue("", "URL:NXM Protocol");
        }

        private static void SetupM3InNXMHandler(string nxmIniPath)
        {
            DuplicatingIni ini = DuplicatingIni.LoadIni(nxmIniPath);
            var handlers = ini.GetOrAddSection("handlers");
            var numCurrentHandlersStr = handlers.GetValue("size")?.Value;
            int.TryParse(numCurrentHandlersStr, out var numCurrentHandlers);

            // Find if M3 has been registered for me/me2/me3
            bool updated = false;
            for (int i = 1; i <= numCurrentHandlers; i++)
            {
                var games = handlers.GetValue($@"{i}\games");
                if (games == null)
                {
                    // ???
                    // Is ini configured incorrectly?
                    M3Log.Warning(@"NXMHandler ini appears to be configured incorrectly");
                }
                else
                {
                    if (games.Value == "other")
                    {
                        M3Log.Information(@"Updating 'other' in nxmhandler");
                        // We need to update this one
                        handlers.SetSingleEntry($@"{i}\executable", App.ExecutableLocation.Replace("\\", "\\\\"));
                        handlers.SetSingleEntry($@"{i}\arguments", "--nxmlink");
                        updated = true;
                    }
                }
            }

            if (!updated)
            {
                // Add ours
                M3Log.Warning(@"Adding section 'other' in nxmhandler");
                numCurrentHandlers++;
                handlers.SetSingleEntry($@"size", numCurrentHandlers);
                handlers.SetSingleEntry($@"{numCurrentHandlers}\games", "other");
                handlers.SetSingleEntry($@"{numCurrentHandlers}\executable",
                    App.ExecutableLocation.Replace("\\", "\\\\"));
                handlers.SetSingleEntry($@"{numCurrentHandlers}\arguments", "--nxmlink");
            }

            File.WriteAllText(nxmIniPath, ini.ToString());
            M3Log.Information(@"Finished configuring nxmhandler");
            // Register nxm protocol

        }

        public static async Task<string> SetupNexusLogin(Action<string> updateStatus)
        {
            // open a web socket to receive the api key
            var guid = Guid.NewGuid();
            WatsonWsClient client = new WatsonWsClient(new Uri("wss://sso.nexusmods.com"));
            string api_key = null;
            object lockobj = new object();
            object serverConnectedLockObj = new object();

            client.ServerConnected += ServerConnected;
            client.ServerDisconnected += ServerDisconnected;
            client.MessageReceived += MessageReceived;
            client.Start();

            void MessageReceived(object sender, MessageReceivedEventArgs args)
            {
                Debug.WriteLine("Message from server: " + Encoding.UTF8.GetString(args.Data));
                api_key = Encoding.UTF8.GetString(args.Data);
                lock (lockobj)
                {
                    Monitor.Pulse(lockobj);
                }
                //client.
            }

            void ServerConnected(object sender, EventArgs args)
            {
                Debug.WriteLine("Server connected");
                lock (serverConnectedLockObj)
                {
                    Monitor.Pulse(serverConnectedLockObj);
                }
            }

            void ServerDisconnected(object sender, EventArgs args)
            {
                Debug.WriteLine("Server disconnected");
            }

            //await Task.Delay(1000, cancel);
            lock (serverConnectedLockObj)
            {
                Monitor.Wait(serverConnectedLockObj, new TimeSpan(0, 0, 0, 15));
            }

            if (client.Connected)
            {
                await client.SendAsync(
                    Encoding.UTF8.GetBytes("{\"id\": \"" + guid + "\", \"appid\": \"me3tweaks\"}")); //do not localize
                Thread.Sleep(1000); //??
                M3Utilities.OpenWebpage($"https://www.nexusmods.com/sso?id={guid}&application=me3tweaks");
                lock (lockobj)
                {
                    Monitor.Wait(lockobj, new TimeSpan(0, 0, 1, 0));
                }

                client.Dispose();
            }

            return api_key;
        }

        /// <summary>
        /// Gets the content preview for a mod file. This is a blocking call
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static ContentPreview GetFileListing(ModFile file)
        {
            if (NexusModsUtilities.UserInfo == null) return null;
            var client = NexusModsUtilities.GetClient();
            return client.ModFiles.GetContentPreview(file.ContentPreviewLink).Result;
        }
    }
}