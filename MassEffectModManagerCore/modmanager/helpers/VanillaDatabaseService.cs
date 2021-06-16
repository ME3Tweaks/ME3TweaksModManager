using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using FontAwesome.WPF;
using LegendaryExplorerCore.Helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.objects.mod;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Serilog;

namespace MassEffectModManagerCore.modmanager.helpers
{
    /// <summary>
    /// Class for querying information about game and fetching vanilla files.
    /// </summary>
    public class VanillaDatabaseService
    {
        public static CaseInsensitiveDictionary<List<(int size, string md5)>> ME1VanillaDatabase = new CaseInsensitiveDictionary<List<(int size, string md5)>>();
        public static CaseInsensitiveDictionary<List<(int size, string md5)>> ME2VanillaDatabase = new CaseInsensitiveDictionary<List<(int size, string md5)>>();
        public static CaseInsensitiveDictionary<List<(int size, string md5)>> ME3VanillaDatabase = new CaseInsensitiveDictionary<List<(int size, string md5)>>();

        public static CaseInsensitiveDictionary<List<(int size, string md5)>> LE1VanillaDatabase = new CaseInsensitiveDictionary<List<(int size, string md5)>>();
        public static CaseInsensitiveDictionary<List<(int size, string md5)>> LE2VanillaDatabase = new CaseInsensitiveDictionary<List<(int size, string md5)>>();
        public static CaseInsensitiveDictionary<List<(int size, string md5)>> LE3VanillaDatabase = new CaseInsensitiveDictionary<List<(int size, string md5)>>();
        public static CaseInsensitiveDictionary<List<(int size, string md5)>> LELauncherVanillaDatabase = new CaseInsensitiveDictionary<List<(int size, string md5)>>();


        public static CaseInsensitiveDictionary<List<(int size, string md5)>> LoadDatabaseFor(MEGame game, bool isMe1PL = false)
        {
            string assetPrefix = $@"MassEffectModManagerCore.modmanager.gamemd5.{game.ToString().ToLower()}";
            if (game == MEGame.LELauncher)
                assetPrefix = $@"MassEffectModManagerCore.modmanager.gamemd5.lel";

            switch (game)
            {
                case MEGame.ME1:
                    ME1VanillaDatabase.Clear();
                    var me1stream = Utilities.ExtractInternalFileToStream($@"{assetPrefix}{(isMe1PL ? @"pl" : @"")}.bin"); //do not localize
                    ParseDatabase(me1stream, ME1VanillaDatabase);
                    return ME1VanillaDatabase;
                case MEGame.ME2:
                    if (ME2VanillaDatabase.Count > 0) return ME2VanillaDatabase;
                    var me2stream = Utilities.ExtractInternalFileToStream($@"{assetPrefix}.bin");
                    ParseDatabase(me2stream, ME2VanillaDatabase);
                    return ME2VanillaDatabase;
                case MEGame.ME3:
                    if (ME3VanillaDatabase.Count > 0) return ME3VanillaDatabase;
                    var me3stream = Utilities.ExtractInternalFileToStream($@"{assetPrefix}.bin");
                    ParseDatabase(me3stream, ME3VanillaDatabase);
                    return ME3VanillaDatabase;
                case MEGame.LE1:
                    if (LE1VanillaDatabase.Count > 0) return LE1VanillaDatabase;
                    var le1stream = Utilities.ExtractInternalFileToStream($@"{assetPrefix}.bin");
                    ParseDatabase(le1stream, LE1VanillaDatabase);
                    return LE1VanillaDatabase;
                case MEGame.LE2:
                    if (ME2VanillaDatabase.Count > 0) return LE2VanillaDatabase;
                    var le2stream = Utilities.ExtractInternalFileToStream($@"{assetPrefix}.bin");
                    ParseDatabase(le2stream, LE2VanillaDatabase);
                    return LE2VanillaDatabase;
                case MEGame.LE3:
                    if (LE3VanillaDatabase.Count > 0) return LE3VanillaDatabase;
                    var le3stream = Utilities.ExtractInternalFileToStream($@"{assetPrefix}.bin");
                    ParseDatabase(le3stream, LE3VanillaDatabase);
                    return LE3VanillaDatabase;
                case MEGame.LELauncher:
                    if (LELauncherVanillaDatabase.Count > 0) return LELauncherVanillaDatabase;
                    var lelstream = Utilities.ExtractInternalFileToStream($@"{assetPrefix}.bin");
                    ParseDatabase(lelstream, LELauncherVanillaDatabase);
                    return LELauncherVanillaDatabase;
            }

            return null;
        }

        public static DLCPackage FetchVanillaSFAR(string dlcName, GameTarget target = null)
        {
            var backupPath = BackupService.GetGameBackupPath(MEGame.ME3);
            if (backupPath == null && target == null) return null; //can't fetch

            string sfar;
            if (dlcName == @"DLC_TestPatch" || dlcName == @"TESTPATCH") //DLC name, job header (as shown in BFF)
            {
                //testpatch
                sfar = ME3Directory.GetTestPatchSFARPath(backupPath ?? target.TargetPath);
            }
            else
            {
                //main dlc
                string dlcDir = backupPath == null ? M3Directories.GetDLCPath(target) : MEDirectories.GetDLCPath(MEGame.ME3, backupPath);
                sfar = Path.Combine(dlcDir, dlcName, @"CookedPCConsole", @"Default.sfar");
            }

            if (File.Exists(sfar))
            {
                if (target != null && !IsFileVanilla(target, sfar, false))
                {
                    Log.Error(@"SFAR is not vanilla: " + sfar);
                    return null; //Not vanilla!
                }

                return new DLCPackage(sfar);
            }
            else
            {
                Log.Error($@"SFAR does not exist for requested file fetch: {sfar}");
            }
            return null;
        }

        /// <summary>
        /// Fetches a file from an SFAR from the game backup of ME3.
        /// </summary>
        /// <param name="dlcName">Name of DLC to fetch from. This is the DLC foldername, or TESTPATCH if fetching from there.</param>
        /// <param name="filename">File in archive to fetch.</param>
        /// <param name="target">Optional forced target, in case you just want to fetch from a target (still must be vanilla).</param>
        /// <returns>Null if file could not be fetched or was not vanilla</returns>

        public static MemoryStream FetchFileFromVanillaSFAR(string dlcName, string filename, GameTarget target = null, DLCPackage forcedDLC = null)
        {
            var dlc = forcedDLC ?? FetchVanillaSFAR(dlcName, target);
            if (dlc != null)
            {
                var dlcEntry = dlc.FindFileEntry(filename);
                if (dlcEntry >= 0)
                {
                    //Log.Information($@"Extracting file to memory from {dlcName} SFAR: {filename}");
                    return dlc.DecompressEntry(dlcEntry);
                }
                else
                {
                    //Log.Error($@"Could not find file entry in {dlcName} SFAR: {filename}");
                }
            }
            return null;
        }

        private static void ParseDatabase(MemoryStream stream, Dictionary<string, List<(int size, string md5)>> targetDictionary, bool trimLERoots = false)
        {
            if (stream.ReadStringASCII(4) != @"MD5T")
            {
                throw new Exception(@"Header of MD5 table doesn't match expected value!");
            }

            //Decompress
            var decompressedSize = stream.ReadInt32();
            //var compressedSize = stream.Length - stream.Position;

            var compressedBuffer = stream.ReadToBuffer(stream.Length - stream.Position);
            var decompressedBuffer = LZMA.Decompress(compressedBuffer, (uint)decompressedSize);
            if (decompressedBuffer.Length != decompressedSize)
            {
                throw new Exception(@"Vanilla database failed to decompress");
            }

            //Read
            MemoryStream table = new MemoryStream(decompressedBuffer);
            int numEntries = table.ReadInt32();
            var packageNames = new List<string>(numEntries);
            //Package names
            for (int i = 0; i < numEntries; i++)
            {
                //Read entry
                var readName = table.ReadStringASCIINull().Replace('/', '\\').TrimStart('\\');
                if (trimLERoots)
                    readName = readName.Substring(9); // Game/ME1/
                packageNames.Add(readName);
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
                    sb.Append(c2.ToString(@"x1"));
                    sb.Append(c1.ToString(@"x1"));
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

        /// <summary>
        /// Fetches a file from the backup CookedPC/CookedPCConsole directory.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="filename">FILENAME only of file. Do not pass a relative path</param>
        /// <returns></returns>
        public static MemoryStream FetchBasegameFile(MEGame game, string filename)
        {
            var backupPath = BackupService.GetGameBackupPath(game);
            if (backupPath == null/* && target == null*/) return null; //can't fetch

            string cookedPath = MEDirectories.GetCookedPath(game, backupPath);

            if (game >= MEGame.ME2)
            {
                //Me2,Me3: Game file will exist in this folder
                var file = Path.Combine(cookedPath, Path.GetFileName(filename));
                if (File.Exists(file))
                {
                    //file found
                    return new MemoryStream(File.ReadAllBytes(file));
                }
                else
                {
                    //Log.Error($@"Could not find basegame file in backup for {game}: {file}");
                }
            }
            else
            {
                //Me1: will have to search subdirectories for file with same name.
                string[] files = Directory.GetFiles(cookedPath, Path.GetFileName(filename), SearchOption.AllDirectories);
                if (files.Count() == 1)
                {
                    //file found
                    return new MemoryStream(File.ReadAllBytes(files[0]));
                }
                else
                {
                    //ambiguous or file not found
                    Log.Error($@"Could not find basegame file (or found multiple) in backup for {game}: {filename}");

                }
            }
            return null;
        }

        /// <summary>
        /// Fetches a DLC file from ME1/ME2 backup.
        /// </summary>
        /// <param name="game">game to fetch from</param>
        /// <param name="dlcfoldername">DLC foldername</param>
        /// <param name="filename">filename</param>
        /// <returns></returns>
        internal static MemoryStream FetchME1ME2DLCFile(MEGame game, string dlcfoldername, string filename)
        {
            if (game == MEGame.ME3) throw new Exception(@"Cannot call this method with game = ME3");
            var backupPath = BackupService.GetGameBackupPath(game);
            if (backupPath == null) return null; //can't fetch

            string dlcPath = MEDirectories.GetDLCPath(game, backupPath);
            string dlcFolderPath = Path.Combine(dlcPath, dlcfoldername);

            string[] files = Directory.GetFiles(dlcFolderPath, Path.GetFileName(filename), SearchOption.AllDirectories);
            if (files.Count() == 1)
            {
                //file found
                return new MemoryStream(File.ReadAllBytes(files[0]));
            }
            else
            {
                //ambiguous or file not found
                Log.Error($@"Could not find {filename} DLC file (or found multiple) in backup for {game}: {filename}");
            }

            return null;
        }


        public static bool IsFileVanilla(GameTarget target, string file, bool md5check = false)
        {
            var relativePath = file.Substring(target.TargetPath.Length + 1);
            return IsFileVanilla(target.Game, file, relativePath, target.IsPolishME1, md5check);
        }

        public static bool IsFileVanilla(MEGame game, string fullpath, string relativepath, bool isME1Polish, bool md5check = false)
        {
            var database = LoadDatabaseFor(game, isME1Polish);
            if (database.TryGetValue(relativepath, out var info))
            {
                //foreach (var c in info)
                //{
                //    Debug.WriteLine("Sizes accepted: " + c.size);
                //}
                FileInfo f = new FileInfo(fullpath);
                bool hasSameSize = info.Any(x => x.size == f.Length);
                if (!hasSameSize)
                {
                    return false;
                }

                if (md5check)
                {
                    var md5 = Utilities.CalculateMD5(fullpath);
                    return info.Any(x => x.md5 == md5);
                }
                return true;
            }

            return false;
        }

        private static readonly string[] BasegameTFCs = { @"CharTextures", @"Movies", @"Textures", @"Lighting" };
        internal static bool IsBasegameTFCName(string tfcName, MEGame game)
        {
            if (BasegameTFCs.Contains(tfcName)) return true;
            //Might be DLC.
            var dlcs = MEDirectories.OfficialDLC(game);
            foreach (var dlc in dlcs)
            {
                string dlcTfcName = @"Textures_" + dlc;
                if (dlcTfcName == tfcName)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ValidateTargetAgainstVanilla(GameTarget target, Action<string> failedValidationCallback)
        {
            bool isVanilla = true;
            CaseInsensitiveDictionary<List<(int size, string md5)>> vanillaDB = null;
            switch (target.Game)
            {
                case MEGame.ME1:
                    if (ME1VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.ME1, target.IsPolishME1);
                    vanillaDB = ME1VanillaDatabase;
                    break;
                case MEGame.ME2:
                    if (ME2VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.ME2);
                    vanillaDB = ME2VanillaDatabase;
                    break;
                case MEGame.ME3:
                    if (ME3VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.ME3);
                    vanillaDB = ME3VanillaDatabase;
                    break;
                case MEGame.LE1:
                    if (LE1VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.LE1);
                    vanillaDB = LE1VanillaDatabase;
                    break;
                case MEGame.LE2:
                    if (LE2VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.LE2);
                    vanillaDB = LE2VanillaDatabase;
                    break;
                case MEGame.LE3:
                    if (LE3VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.LE3);
                    vanillaDB = LE3VanillaDatabase;
                    break;
                case MEGame.LELauncher:
                    if (LELauncherVanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.LELauncher);
                    vanillaDB = LELauncherVanillaDatabase;
                    break;
                default:
                    throw new Exception(@"Cannot vanilla check against game that is not ME1/ME2/ME3");
            }
            if (Directory.Exists(target.TargetPath))
            {

                foreach (string file in Directory.EnumerateFiles(target.TargetPath, @"*", SearchOption.AllDirectories))
                {
                    var shortname = file.Substring(target.TargetPath.Length + 1);
                    if (vanillaDB.TryGetValue(shortname, out var fileInfo))
                    {
                        var localFileInfo = new FileInfo(file);
                        bool sfar = Path.GetExtension(file) == @".sfar";
                        bool correctSize;
                        if (sfar && localFileInfo.Length == 32)
                        {
                            correctSize = false; //We don't treat 32byte as "correct" for vanilla purposes.
                        }
                        else
                        {
                            correctSize = fileInfo.Any(x => x.size == localFileInfo.Length);
                        }
                        if (correctSize && !sfar) continue; //OK
                        if (sfar && correctSize)
                        {
                            //Inconsistency check
                            if (!GameTarget.SFARObject.HasUnpackedFiles(file)) continue; //Consistent
                        }
                        failedValidationCallback?.Invoke(file);
                        isVanilla = false;
                    }
                    else
                    {
                        Debug.WriteLine(@"File not in Vanilla DB: " + shortname);
                    }
                }
            }
            else
            {
                Log.Error(@"Directory to validate doesn't exist: " + target.TargetPath);
            }

            return isVanilla;
        }

        /// <summary>
        /// Gets list of DLC directories that are not made by BioWare
        /// </summary>
        /// <param name="target">Target to get mods from</param>
        /// <returns>List of DLC foldernames</returns>
        internal static List<string> GetInstalledDLCMods(GameTarget target)
        {
            return M3Directories.GetInstalledDLC(target).Where(x => !MEDirectories.OfficialDLC(target.Game).Contains(x, StringComparer.InvariantCultureIgnoreCase)).ToList();
        }

        /// <summary>
        /// Gets list of DLC directories that are made by BioWare
        /// </summary>
        /// <param name="target">Target to get dlc from</param>
        /// <returns>List of DLC foldernames</returns>
        internal static List<string> GetInstalledOfficialDLC(GameTarget target, bool includeDisabled = false)
        {
            return M3Directories.GetInstalledDLC(target, includeDisabled).Where(x => MEDirectories.OfficialDLC(target.Game).Contains(x.TrimStart('x'), StringComparer.InvariantCultureIgnoreCase)).ToList();
        }

        public static readonly string[] UnpackedFileExtensions = { @".pcc", @".tlk", @".bin", @".dlc", @".afc", @".tfc" };

        internal static bool ValidateTargetDLCConsistency(GameTarget target, Action<string> inconsistentDLCCallback = null)
        {
            if (target.Game != MEGame.ME3) return true; //No consistency check except for ME3
            bool allConsistent = true;
            var dlcDir = M3Directories.GetDLCPath(target);
            var dlcFolders = M3Directories.GetInstalledDLC(target).Where(x => MEDirectories.OfficialDLC(target.Game).Contains(x)).Select(x => Path.Combine(dlcDir, x)).ToList();
            foreach (var dlcFolder in dlcFolders)
            {
                string unpackedDir = Path.Combine(dlcFolder, @"CookedPCConsole");
                string sfar = Path.Combine(unpackedDir, @"Default.sfar");
                if (File.Exists(sfar))
                {
                    FileInfo fi = new FileInfo(sfar);
                    var sfarsize = fi.Length;
                    if (sfarsize > 32)
                    {
                        //Packed
                        var filesInSfarDir = Directory.EnumerateFiles(unpackedDir).ToList();
                        if (filesInSfarDir.Any(d =>
                            !Path.GetFileName(d).Equals(@"PCConsoleTOC.bin", StringComparison.InvariantCultureIgnoreCase) && //pcconsoletoc will be produced for all folders even with autotoc asi even if its not needed
                                                    UnpackedFileExtensions.Contains(Path.GetExtension(d.ToLower()))))
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

        public static List<(int size, string md5)> GetVanillaFileInfo(GameTarget target, string filepath)
        {
            CaseInsensitiveDictionary<List<(int size, string md5)>> vanillaDB = null;
            switch (target.Game)
            {
                case MEGame.ME1:
                    if (ME1VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.ME1, target.IsPolishME1);
                    vanillaDB = ME1VanillaDatabase;
                    break;
                case MEGame.ME2:
                    if (ME2VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.ME2);
                    vanillaDB = ME2VanillaDatabase;
                    break;
                case MEGame.ME3:
                    if (ME3VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.ME3);
                    vanillaDB = ME3VanillaDatabase;
                    break;
                case MEGame.LE1:
                    if (LE1VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.LE1);
                    vanillaDB = LE1VanillaDatabase;
                    break;
                case MEGame.LE2:
                    if (LE2VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.LE2);
                    vanillaDB = LE2VanillaDatabase;
                    break;
                case MEGame.LE3:
                    if (LE3VanillaDatabase.Count == 0) LoadDatabaseFor(MEGame.LE3);
                    vanillaDB = LE3VanillaDatabase;
                    break;
                default:
                    throw new Exception(@"Cannot vanilla check against game that is not ME1/ME2/ME3");
            }
            if (vanillaDB.TryGetValue(filepath, out var info))
            {
                return info;
            }

            return null;
        }

        /// <summary>
        /// Gets the game source string for the specified target.
        /// </summary>
        /// <param name="target">Target to get source for</param>
        /// <returns>Game source if supported, null otherwise</returns>
        internal static (string hash, string result) GetGameSource(GameTarget target, bool reverseME1 = true)
        {
            var exe = M3Directories.GetExecutablePath(target);
            if (!File.Exists(exe))
            {
                return (@"Executable Missing!", null);
            }

            string md5 = null;
            if (target.Game != MEGame.ME1 || !reverseME1)
            {
                md5 = Utilities.CalculateMD5(exe);
            }

            switch (target.Game)
            {
                case MEGame.ME1:
                    if (reverseME1)
                    {
                        ME1ExecutableInfo me1ExecutableInfo = ME1ExecutableInfo.GetExecutableInfo(M3Directories.GetExecutablePath(target), true);
                        md5 = me1ExecutableInfo.OriginalExecutableHash;
                    }
                    SUPPORTED_HASHES_ME1.TryGetValue(md5, out var me1result);
                    return (md5, me1result);
                case MEGame.ME2:
                    SUPPORTED_HASHES_ME2.TryGetValue(md5, out var me2result);
                    return (md5, me2result);
                case MEGame.ME3:
                    SUPPORTED_HASHES_ME3.TryGetValue(md5, out var me3result);
                    return (md5, me3result);
                case MEGame.LE1:
                    SUPPORTED_HASHES_LE1.TryGetValue(md5, out var le1result);
                    return (md5, le1result);
                case MEGame.LE2:
                    SUPPORTED_HASHES_LE2.TryGetValue(md5, out var le2result);
                    return (md5, le2result);
                case MEGame.LE3:
                    SUPPORTED_HASHES_LE3.TryGetValue(md5, out var le3result);
                    return (md5, le3result);
                case MEGame.LELauncher:
                    SUPPORTED_HASHES_LEL.TryGetValue(md5, out var lelresult);
                    return (md5, lelresult);
                default:
                    throw new Exception(@"Cannot vanilla check against game that is not ME1/ME2/ME3 LE1/LE2/LE3");
            }
        }

        private static Dictionary<string, string> SUPPORTED_HASHES_ME1 = new Dictionary<string, string>
        {
            [@"647b93621389709cab8d268379bd4c47"] = @"Steam",
            [@"fef464b4b92c19ce76add75de6732ccf"] = @"Steam, BioWare signed",
            [@"ff1f894fa1c2dbf4d4b9f0de85c166e5"] = @"Origin",
            [@"73b76699d4e245c92110a93c54980b78"] = @"DVD",
            [@"298c30a399d0959e5e997a9d64b42548"] = @"DVD, Polish",
            [@"8bba14d838d9c95e10d8ceeb5c958976"] = @"Origin, Vanilla, Alternate1?",
            [@"a8d61af97159cb62040c07b94e44299e"] = @"Origin, Vanilla, Alternate2?",
        };

        private static Dictionary<string, string> SUPPORTED_HASHES_ME2 = new Dictionary<string, string>
        {
            [@"73827026bc9629562c4a3f61a752541c"] = @"Origin, ME2Game/MassEffect2 swapped",
            [@"32fb31b80804040996ed78d14110b54b"] = @"Origin",
            [@"229173ca9057baeb4fd9f0fb2e569051"] = @"Origin, ME2Game",
            [@"16f214ce81ba228347bce7b93fb0f37a"] = @"Steam",
            [@"e26f142d44057628efd086c605623dcf"] = @"DVD, Alternate",
            [@"b1d9c44be87acac610dfa9947e114096"] = @"DVD",
        };

        private static Dictionary<string, string> SUPPORTED_HASHES_ME3 = new Dictionary<string, string>
        {
            [@"1d09c01c94f01b305f8c25bb56ce9ab4"] = @"Origin",
            [@"90d51c84b278b273e41fbe75682c132e"] = @"Origin, Alternate",
            [@"70dc87862da9010aad1acd7d0c2c857b"] = @"Origin, Russian",
        };

        private static Dictionary<string, string> SUPPORTED_HASHES_LE1 = new Dictionary<string, string>
        {
            [@"f4331d60672509b342da12bc42b4622f"] = @"Origin 2.0.0.47902", // May 14 Launch Version
            [@"ade71fcaa13224e383a848b90db807f2"] = @"Origin 2.0.0.48204", // May 17
            [@"92c9915cf4da2b8fb0b8ebb0795ba2d9"] = @"Origin 2.0.0.48602", // June 7
        };

        private static Dictionary<string, string> SUPPORTED_HASHES_LE2 = new Dictionary<string, string>
        {
            [@"3b4dd0078a122476126c3b5c6665db16"] = @"Origin 2.0.0.47902",
            [@"fcb4c06b21853ece31a1897e136df45c"] = @"Origin 2.0.0.48204",
            [@"b584a70f56cfed7b8f35a081420227b0"] = @"Origin 2.0.0.48602",
        };

        private static Dictionary<string, string> SUPPORTED_HASHES_LE3 = new Dictionary<string, string>
        {
            [@"a64622ed97309563a5597adbed4055ca"] = @"Origin 2.0.0.47902",
            [@"c55689e54c921739532beb033f0f0ebf"] = @"Origin 2.0.0.48204",
            [@"11e222d4f8f7c2a123b80a938b76c922"] = @"Origin 2.0.0.48602",
        };

        private static Dictionary<string, string> SUPPORTED_HASHES_LEL = new Dictionary<string, string>
        {
            [@"e03dd006f2d4f56f46a8f6c014230ba8"] = @"Origin 2.0.0.47902",
            [@"a50a5ab69a0de8356ddd8ab69f8ecdc2"] = @"Origin 2.0.0.48204",
            [@"5e8dc210b4adda2eda1dc367a781c3a8"] = @"Origin 2.0.0.48602",
        };
        
        /// <summary>
        /// Checks the existing listed backup and tags it with cmm_vanilla if determined to be vanilla. This is because ALOT Installer allows modified backups where as Mod Manager will not
        /// </summary>
        /// <param name="game"></param>
        internal static void CheckAndTagBackup(MEGame game)
        {
            Log.Information(@"Validating backup for " + game.ToGameName());
            var targetPath = BackupService.GetGameBackupPath(game, false);
            Log.Information(@"Backup location: " + targetPath);
            BackupService.SetStatus(game, M3L.GetString(M3L.string_checkingBackup), M3L.GetString(M3L.string_pleaseWait));
            BackupService.SetActivity(game, true);
            BackupService.SetIcon(game, FontAwesomeIcon.Spinner);
            GameTarget target = new GameTarget(game, targetPath, false);
            var validationFailedReason = target.ValidateTarget();
            if (target.IsValid)
            {
                List<string> nonVanillaFiles = new List<string>();
                void nonVanillaFileFoundCallback(string filepath)
                {
                    Log.Error($@"Non-vanilla file found: {filepath}");
                    nonVanillaFiles.Add(filepath);
                }

                List<string> inconsistentDLC = new List<string>();
                void inconsistentDLCFoundCallback(string filepath)
                {
                    if (target.Supported)
                    {
                        Log.Error($@"DLC is in an inconsistent state: {filepath}");
                        inconsistentDLC.Add(filepath);
                    }
                    else
                    {
                        Log.Error(@"Detected an inconsistent DLC, likely due to an unofficial copy of the game");
                    }
                }
                Log.Information(@"Validating backup...");

                VanillaDatabaseService.LoadDatabaseFor(game, target.IsPolishME1);
                bool isVanilla = VanillaDatabaseService.ValidateTargetAgainstVanilla(target, nonVanillaFileFoundCallback);
                bool isDLCConsistent = VanillaDatabaseService.ValidateTargetDLCConsistency(target, inconsistentDLCCallback: inconsistentDLCFoundCallback);
                List<string> dlcModsInstalled = VanillaDatabaseService.GetInstalledDLCMods(target);
                var memTextures = Directory.GetFiles(target.TargetPath, @"TexturesMEM*.tfc", SearchOption.AllDirectories);

                if (isVanilla && isDLCConsistent && !dlcModsInstalled.Any() && !memTextures.Any())
                {
                    //Backup is OK
                    //Tag
                    File.WriteAllText(Path.Combine(targetPath, @"cmm_vanilla"), App.AppVersionHR);
                    Log.Information(@"Wrote cmm_vanilla to validated backup");
                    BackupService.SetBackedUp(game, true);
                }
                else
                {
                    Log.Warning($@"Backup verification has failed for the backup at {target.TargetPath}. This backup will not be used");
                }
            }
            else
            {
                Log.Information(@"Backup target is invalid. This backup cannot not be used. Reason: " + validationFailedReason);
            }
            BackupService.RefreshBackupStatus(null, game);
            BackupService.SetActivity(game, false);
            BackupService.ResetIcon(game);
        }

        private static Dictionary<MEGame, List<string>> threeWayMergeMap = new Dictionary<MEGame, List<string>>()
        {
            { MEGame.ME1, new List<string>()
                {
                    @"BIOC_Base.u",
                    @""
                }
            },{ MEGame.ME2, new List<string>()
                {
                    @"SFXGame.pcc",
                    @"Engine.pcc"
                }
            },
            { MEGame.ME3, new List<string>()
                {
                    @"SFXGame.pcc",
                    @"Engine.pcc"
                }
            }
        };

        /// <summary>
        /// Returns the list of files that can potentially be three way merged
        /// </summary>
        /// <returns></returns>
        internal static object GetThreeWayMergeFiles(GameTarget gameTarget, (Dictionary<ModJob, (Dictionary<string, Mod.InstallSourceFile> fileMapping, List<string> dlcFoldersBeingInstalled)> unpackedJobMappings, List<(ModJob job, string sfarPath, Dictionary<string, Mod.InstallSourceFile> sfarInstallationMapping)> sfarJobs) installationQueues)
        {
            var bgJob = installationQueues.unpackedJobMappings.FirstOrDefault(x => x.Key.Header == ModJob.JobHeader.BASEGAME);
            if (bgJob.Key != null)
            {
                //Has basegame job.
                var filemapping = bgJob.Value.fileMapping;
                foreach (var f in filemapping)
                {
                    var fname = Path.GetFileName(f.Value.FilePath);
                    if (IsThreeWayMergeEligible(gameTarget.Game, fname))
                    {
                        Debug.WriteLine(@"TWM ELIG");
                    }
                }

            }
            return new object();
        }

        private static bool IsThreeWayMergeEligible(MEGame game, string fname)
        {
            return threeWayMergeMap.TryGetValue(game, out var flist) && flist.Contains(fname, StringComparer.InvariantCultureIgnoreCase);
        }
    }
}
