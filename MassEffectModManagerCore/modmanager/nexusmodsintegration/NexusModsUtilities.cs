using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Pathoschild.FluentNexus;

namespace MassEffectModManagerCore.modmanager.nexusmodsintegration
{
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

        public static bool IsAuthenticated => File.Exists(Path.Combine(Utilities.GetNexusModsCache(), "nexusmodsapikey"));

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
                using FileStream fs = new FileStream(keyPath, FileMode.Open);
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
