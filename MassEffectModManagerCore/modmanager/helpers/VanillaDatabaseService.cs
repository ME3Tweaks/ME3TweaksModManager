using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Gammtek.Conduit.IO;
using MassEffectModManager;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.objects;

namespace MassEffectModManagerCore.modmanager.helpers
{
    public class VanillaDatabaseService
    {
        public static CaseInsensitiveDictionary<List<(int size, string md5)>> ME1VanillaDatabase = new CaseInsensitiveDictionary<List<(int size, string md5)>>();
        public static CaseInsensitiveDictionary<List<(int size, string md5)>> ME2VanillaDatabase = new CaseInsensitiveDictionary<List<(int size, string md5)>>();
        public static CaseInsensitiveDictionary<List<(int size, string md5)>> ME3VanillaDatabase = new CaseInsensitiveDictionary<List<(int size, string md5)>>();

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

        private static void ParseDatabase(MemoryStream stream, Dictionary<string, List<(int size, string md5)>> targetDictionary)
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
                byte[] md5bytes = table.ReadToBuffer(16);
                StringBuilder sb = new StringBuilder();
                foreach (var b in md5bytes)
                {
                    var c1 = (b & 0x0F);
                    var c2 = (b & 0xF0) >> 4;
                    //Debug.WriteLine(c1.ToString("x1"));
                    //Debug.WriteLine(c2.ToString("x1"));

                    //Reverse order
                    sb.Append(c2.ToString("x1"));
                    sb.Append(c1.ToString("x1"));
                    //Debug.WriteLine(sb.ToString());
                }

                //var t = sb.ToString();
                List<(int size, string md5)> list;
                targetDictionary.TryGetValue(path, out list);
                if (list == null)
                {
                    list = new List<(int size, string md5)>();
                    targetDictionary[path] = list;
                }
                list.Add((size, sb.ToString()));
            }
        }

        public static bool ValidateTargetAgainstVanilla(GameTarget target, Action<string> failedValidationCallback)
        {
            bool isValid = true;
            CaseInsensitiveDictionary<List<(int size, string md5)>> vanillaDB = null;
            switch (target.Game)
            {
                case Mod.MEGame.ME1:
                    if (ME1VanillaDatabase.Count == 0) LoadDatabaseFor(Mod.MEGame.ME1); //Have tofigure out how to load PL
                    vanillaDB = ME1VanillaDatabase;
                    break;
                case Mod.MEGame.ME2:
                    if (ME2VanillaDatabase.Count == 0) LoadDatabaseFor(Mod.MEGame.ME2); 
                    vanillaDB = ME2VanillaDatabase;
                    break;
                case Mod.MEGame.ME3:
                    if (ME2VanillaDatabase.Count == 0) LoadDatabaseFor(Mod.MEGame.ME3); 
                    vanillaDB = ME3VanillaDatabase;
                    break;
                default:
                    throw new Exception("Cannot vanilla check against game that is not ME1/ME2/ME3");
            }

            foreach (string file in Directory.EnumerateFiles(target.TargetPath, "*", SearchOption.AllDirectories))
            {
                var shortname = file.Substring(target.TargetPath.Length);
                if (vanillaDB.TryGetValue(shortname, out var fileInfo))
                {
                    var localFileInfo = new FileInfo(file);
                    bool sfar = Path.GetExtension(file) == ".sfar";
                    bool correctSize = fileInfo.Any(x => x.size == localFileInfo.Length);
                    if (correctSize && !sfar) continue; //OK
                    if (sfar && correctSize)
                    {
                        //Inconsistency check
                        if (!GameTarget.SFARObject.HasUnpackedFiles(file)) continue; //Consistent
                    }
                    failedValidationCallback?.Invoke(file);
                    isValid = false;
                }
                else
                {
                    //Debug.WriteLine("File not in Vanilla DB: " + file);
                }
            }

            return isValid;
        }

        /// <summary>
        /// Gets list of DLC directories that are not made by BioWare
        /// </summary>
        /// <param name="target">Target to get mods from</param>
        /// <returns>List of DLC foldernames</returns>
        internal static List<string> GetInstalledDLCMods(GameTarget target)
        {
            var installedDLC = MEDirectories.GetInstalledDLC(target);
            var officialDLC = MEDirectories.OfficialDLC(target.Game);
            return installedDLC.Except(officialDLC).ToList();
        }

        internal static bool ValidateTargetDLCConsistency(GameTarget target, Action<string> inconsistentDLCCallback = null)
        {
            if (target.Game != Mod.MEGame.ME3) return true; //No consistency check except for ME3
            bool allConsistent = true;
            var unpackedFileExtensions = new List<string>() { ".pcc", ".tlk", ".bin", ".dlc" };
            var dlcDir = MEDirectories.DLCPath(target);
            var dlcFolders = MEDirectories.GetInstalledDLC(target).Where(x => MEDirectories.OfficialDLC(target.Game).Contains(x)).Select(x => Path.Combine(dlcDir, x)).ToList();
            foreach (var dlcFolder in dlcFolders)
            {
                string unpackedDir = Path.Combine(dlcFolder, "CookedPCConsole");
                string sfar = Path.Combine(unpackedDir, "Default.sfar");
                if (File.Exists(sfar))
                {
                    FileInfo fi = new FileInfo(sfar);
                    var sfarsize = fi.Length;
                    if (sfarsize > 32)
                    {
                        //Packed
                        var filesInSfarDir = Directory.EnumerateFiles(unpackedDir).ToList();
                        if (filesInSfarDir.Any(d => unpackedFileExtensions.Contains(Path.GetExtension(d.ToLower()))))
                        {
                            inconsistentDLCCallback?.Invoke(dlcFolder);
                            allConsistent = false;
                        }
                    }
                    else
                    {
                        //We do not consider unpacked DLC when checking for consistency
                    }
                }
            }

            return allConsistent;

        }

        /// <summary>
        /// Gets the game source string for the specified target.
        /// </summary>
        /// <param name="target">Target to get source for</param>
        /// <returns>Game source if supported, null otherwise</returns>
        internal static string GetGameSource(GameTarget target)
        {
            var md5 = Utilities.CalculateMD5(MEDirectories.ExecutablePath(target));
            Dictionary<string, string> lookupDict = null;
            switch (target.Game)
            {
                case Mod.MEGame.ME1:
                    SUPPORTED_HASHES_ME1.TryGetValue(md5, out var me1result);
                    return me1result;
                case Mod.MEGame.ME2:
                    SUPPORTED_HASHES_ME2.TryGetValue(md5, out var me2result);
                    return me2result;
                case Mod.MEGame.ME3:
                    SUPPORTED_HASHES_ME3.TryGetValue(md5, out var me3result);
                    return me3result;
                default:
                    throw new Exception("Cannot vanilla check against game that is not ME1/ME2/ME3");
            }

        }

        private static Dictionary<string, string> SUPPORTED_HASHES_ME1 = new Dictionary<string, string>
        {
            ["647b93621389709cab8d268379bd4c47"] = "Steam",
            ["78ac3d9b4aad1989dae74505ea65aa6c"] = "Steam, MEM patched",
            ["2390143503635f3c4cfaed0afe0b8c71"] = "Origin, MEM patched",
            ["ff1f894fa1c2dbf4d4b9f0de85c166e5"] = "Origin",
            ["73b76699d4e245c92110a93c54980b78"] = "DVD",
            ["298c30a399d0959e5e997a9d64b42548"] = "DVD, Polish",
            ["9a89527800722ec308c01a421bfeb478"] = "DVD, Polish, MEM Patched",
            ["8bba14d838d9c95e10d8ceeb5c958976"] = "Origin - German"
        };

        private static Dictionary<string, string> SUPPORTED_HASHES_ME2 = new Dictionary<string, string>
        {
            ["73827026bc9629562c4a3f61a752541c"] = "Origin, ME2Game/MassEffect2 swapped",
            ["32fb31b80804040996ed78d14110b54b"] = "Origin",
            ["229173ca9057baeb4fd9f0fb2e569051"] = "Origin - ME2Game",
            ["16f214ce81ba228347bce7b93fb0f37a"] = "Origin",
            ["73b76699d4e245c92110a93c54980b78"] = "Steam",
            ["e26f142d44057628efd086c605623dcf"] = "DVD - Alternate",
            ["b1d9c44be87acac610dfa9947e114096"] = "DVD"
        };

        private static Dictionary<string, string> SUPPORTED_HASHES_ME3 = new Dictionary<string, string>
        {
            ["1d09c01c94f01b305f8c25bb56ce9ab4"] = "Origin",
            ["90d51c84b278b273e41fbe75682c132e"] = "Origin - Alternate",
            ["70dc87862da9010aad1acd7d0c2c857b"] = "Origin - Russian",
        };
    }
}
