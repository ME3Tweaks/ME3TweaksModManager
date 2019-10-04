using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Gammtek.Conduit.IO;
using MassEffectModManager;

namespace MassEffectModManagerCore.modmanager.helpers
{
    public class VanillaDatabaseService
    {
        public static CaseInsensitiveDictionary<(int size, string md5)> ME1VanillaDatabase = new CaseInsensitiveDictionary<(int size, string md5)>();
        public static CaseInsensitiveDictionary<(int size, string md5)> ME2VanillaDatabase = new CaseInsensitiveDictionary<(int size, string md5)>();
        public static CaseInsensitiveDictionary<(int size, string md5)> ME3VanillaDatabase = new CaseInsensitiveDictionary<(int size, string md5)>();
        public static void LoadDatabaseFor(Mod.MEGame game, bool isMe1PL = false)
        {
            string assetPrefix = "MassEffectModManagerCore.modmanager.gamemd5.me";
            switch (game)
            {
                case Mod.MEGame.ME1:
                    ME1VanillaDatabase.Clear();
                    var me1stream = Utilities.ExtractInternalFileToStream($"{assetPrefix}1{(isMe1PL ? "pl" : "")}.bin");
                    ParseDatabase(me1stream, ME1VanillaDatabase);
                    break;
                case Mod.MEGame.ME2:
                    if (ME2VanillaDatabase.Count > 0) return;
                    var me2stream = Utilities.ExtractInternalFileToStream($"{assetPrefix}3.bin");
                    ParseDatabase(me2stream, ME2VanillaDatabase);

                    break;
                case Mod.MEGame.ME3:
                    if (ME3VanillaDatabase.Count > 0) return;
                    var me3stream = Utilities.ExtractInternalFileToStream($"{assetPrefix}3.bin");
                    ParseDatabase(me3stream, ME3VanillaDatabase);
                    break;
            }
        }

        private static void ParseDatabase(MemoryStream stream, Dictionary<string, (int size, string md5)> targetDictionary)
        {
            if (stream.ReadStringASCII(4) != "MD5T")
            {
                throw new Exception("Header of MD5 table doesn't match expected value!");
            }

            //Decompress
            byte[] decompressedBuffer = new byte[stream.ReadInt32()];
            var compressedSize = stream.Length - stream.Position;
            byte[] compressedBuffer = stream.ReadToBuffer(compressedSize);
            var bytesCompressed = ZlibHelper.Zlib.Decompress(compressedBuffer, (uint)compressedBuffer.Length, decompressedBuffer);
            if (bytesCompressed != decompressedBuffer.Length)
            {
                throw new Exception("Vanilla database failed to decompress");
            }

            //Read
            File.WriteAllBytes(@"C:\users\public\db.bin", decompressedBuffer);
            MemoryStream table = new MemoryStream(decompressedBuffer);
            int numEntries = table.ReadInt32();
            var packageNames = new List<string>(numEntries);
            //Package names
            for (int i = 0; i < numEntries; i++)
            {
                //Read entry
                packageNames.Add(table.ReadStringASCIINull());
            }

            numEntries = table.ReadInt32(); //Not sure how this could be different from names list?
            for (int i = 0; i < numEntries; i++)
            {
                //Populate database
                var index = table.ReadInt32();
                string path = packageNames[index];
                int size = table.ReadInt32();
                string md5 = table.ReadStringASCII(32);
                targetDictionary[path] = (size, md5);
            }
        }
    }
}
