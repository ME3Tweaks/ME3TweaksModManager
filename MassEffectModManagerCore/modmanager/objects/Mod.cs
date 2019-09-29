using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IniParser;
using IniParser.Parser;
using MassEffectModManager.modmanager.helpers;
using MassEffectModManager.modmanager.objects;
using MassEffectModManagerCore.modmanager;
using ME3Explorer.Packages;
using Serilog;
using SevenZip;

namespace MassEffectModManager.modmanager
{
    public class Mod : INotifyPropertyChanged
    {
        public enum MEGame
        {
            Unknown = 0,
            ME1,
            ME2,
            ME3
        }

        public const string DefaultWebsite = "http://example.com"; //this is required to prevent exceptions when binding the navigateuri
        public event PropertyChangedEventHandler PropertyChanged;

        // Constants

        //Mod variables
        public bool ValidMod;
        private bool ignoreLoadErrors;
        public List<ModJob> InstallationJobs = new List<ModJob>();

        //private List<ModJob> jobs;


        public MEGame Game { get; set; }
        public string ModName { get; set; }
        public string ModDeveloper { get; set; }
        public string ModDescription { get; set; }
        public int ModModMakerID { get; set; }

        public string DisplayedModDescription
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(ModDescription);
                sb.AppendLine("=============================");
                //Todo: Mod Deltas

                //Todo: Automatic configuration

                //Todo: Optional manuals

                sb.AppendLine($"Mod version: {ModVersionString ?? "1.0"}");
                sb.AppendLine($"Mod developer: {ModDeveloper}");
                if (ModModMakerID > 0)
                {
                    sb.AppendLine($"ModMaker code: {ModModMakerID}");
                }

                sb.AppendLine("-------Installation information--------");
                sb.AppendLine("Targets ModDesc " + ModDescTargetVersion);
                var modifiesList = InstallationJobs.Where(x => x.Header != ModJob.JobHeader.CUSTOMDLC).Select(x => x.Header.ToString()).ToList();
                if (modifiesList.Count > 0)
                {
                    sb.AppendLine("Modifies: " + String.Join(", ", modifiesList));
                }

                var customDLCJob = InstallationJobs.FirstOrDefault(x => x.Header == ModJob.JobHeader.CUSTOMDLC);
                if (customDLCJob != null)
                {
                    sb.AppendLine("Add Custom DLCs: " + String.Join(", ", customDLCJob.CustomDLCFolderMapping.Values));
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Get's the installation job associated with the header, or null if that job is not defined for this mod.
        /// </summary>
        /// <param name="header">Header to find job for</param>
        /// <returns>Associated job with this header, null otherwise</returns>
        public ModJob GetJob(ModJob.JobHeader header) => InstallationJobs.FirstOrDefault(x => x.Header == header);

        public string ModVersionString { get; set; }
        public double ParsedModVersion { get; set; }
        public string ModWebsite { get; set; } = ""; //not null default I guess.
        public double ModDescTargetVersion { get; set; }
        public int ModClassicUpdateCode { get; set; }
        public string LoadFailedReason { get; set; }
        public List<string> RequiredDLC = new List<string>();
        private List<string> AdditionalDeploymentFolders;
        private List<string> AdditionalDeploymentFiles;
        private bool emptyModIsOK;

        public string ModPath { get; private set; }

        public SevenZipExtractor Archive;

        public string ModDescPath => FilesystemInterposer.PathCombine(IsInArchive, ModPath, "moddesc.ini");


        public bool IsInArchive { get; }
        public bool IsVirtualized { get; private set; }

        private readonly string VirtualizedIniText;

        /// <summary>
        /// Loads a moddesc from a stream. Used when reading data from an archive. 
        /// </summary>
        /// <param name="moddescArchiveEntry">File entry in archive for this moddesc.ini</param>
        /// <param name="archive">Archive to inspect for</param>
        public Mod(ArchiveFileInfo moddescArchiveEntry, SevenZipExtractor archive)
        {
            Log.Information($"Loading moddesc.ini from archive: {Path.GetFileName(archive.FileName)} => {moddescArchiveEntry.FileName}");
            MemoryStream ms = new MemoryStream();
            archive.ExtractFile(moddescArchiveEntry.FileName, ms);
            ms.Position = 0;
            string iniText = new StreamReader(ms).ReadToEnd();
            ModPath = Path.GetDirectoryName(moddescArchiveEntry.FileName);
            Archive = archive;
            IsInArchive = true;
            try
            {
                loadMod(iniText, MEGame.Unknown);
            }
            catch (Exception e)
            {
                LoadFailedReason = "Error occured parsing archive moddesc.ini " + moddescArchiveEntry.FileName + ": " + e.Message;
            }

            Archive = null; //dipose of the mod
        }

        /// <summary>
        /// Initializes a mod from a moddesc.ini file
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="expectedGame"></param>
        public Mod(string filePath, MEGame expectedGame)
        {
            ModPath = Path.GetDirectoryName(filePath);
            Log.Information("Loading moddesc: " + filePath);
            try
            {
                loadMod(File.ReadAllText(filePath), expectedGame);
            }
            catch (Exception e)
            {
                LoadFailedReason = "Error occured parsing " + filePath + ": " + e.Message;
            }
        }

        /// <summary>
        /// Loads a mod from a virtual moddesc.ini file, forcing the ini path. This is used to load a third party mod through a virtual moddesc.ini file.
        /// </summary>
        /// <param name="iniText">Virtual Ini text</param>
        /// <param name="forcedModPath">Path where this moddesc.ini would be if it existed in the archive</param>
        /// <param name="archive">Archive file to parse against</param>
        public Mod(string iniText, string forcedModPath, SevenZipExtractor archive)
        {
            ModPath = forcedModPath;
            Archive = archive;
            IsInArchive = true;
            IsVirtualized = true;
            VirtualizedIniText = iniText;
            Log.Information("Loading virutalized moddesc.ini");
            try
            {
                loadMod(iniText, MEGame.Unknown);
            }
            catch (Exception e)
            {
                LoadFailedReason = "Error occured parsing virtualized moddesc.ini: " + e.Message;
            }
        }

        private void loadMod(string iniText, MEGame expectedGame)
        {
            Game = expectedGame; //we will assign this later. This is for startup errors only
            var parser = new IniDataParser();
            var iniData = parser.Parse(iniText);
            if (double.TryParse(iniData["ModManager"]["cmmver"], out double parsedModCmmVer))
            {
                ModDescTargetVersion = parsedModCmmVer;
            }
            else
            {
                //Run in legacy mode (ME3CMM 1.0)
                ModDescTargetVersion = 1.0;
            }

            ModName = iniData["ModInfo"]["modname"];
            if (string.IsNullOrEmpty(ModName))
            {
                ModName = (ModPath == "" && IsInArchive) ? Path.GetFileNameWithoutExtension(Archive.FileName) : Path.GetFileName(ModPath);
                Log.Error($"Moddesc.ini in {ModPath} does not set the modname descriptor.");
                LoadFailedReason = $"The moddesc.ini file located at {ModPath} does not have a value set for modname. This value is required.";
                return; //Won't set valid
            }

            ModDescription = Utilities.ConvertBrToNewline(iniData["ModInfo"]["moddesc"]);
            ModDeveloper = iniData["ModInfo"]["moddev"];
            ModVersionString = iniData["ModInfo"]["modver"];
            Double.TryParse(ModVersionString, out double parsedValue);
            ParsedModVersion = parsedValue;

            ModWebsite = iniData["ModInfo"]["modsite"] ?? DefaultWebsite;

            Int32.TryParse(iniData["ModInfo"]["modid"], out int modmakerId);
            ModModMakerID = modmakerId;

            Int32.TryParse(iniData["ModInfo"]["updatecode"], out int modupdatecode);
            ModClassicUpdateCode = modupdatecode;
            CLog.Information($"Read modmaker update code (or used default): {ModClassicUpdateCode}", Settings.LogModStartup);
            if (ModClassicUpdateCode > 0 && ModModMakerID > 0)
            {
                Log.Error($"{ModName} has both an updater service update code and a modmaker code assigned. This is not allowed.");
                LoadFailedReason = "This mod has both an updater service update code and a modmaker code assigned. This is not allowed.";
                return; //Won't set valid
            }

            string game = iniData["ModInfo"]["game"];
            switch (game)
            {
                //case null: //will have to find a way to deal with the null case, in the event it's an ME3 mod manager mod from < 6.0.
                case "ME3":
                    Game = MEGame.ME3;
                    break;
                case "ME2":
                    Game = MEGame.ME2;
                    break;
                case "ME1":
                    Game = MEGame.ME1;
                    break;
                default:
                    //Check if this is in ME3 game directory. If it's null, it might be a legacy mod
                    if (game == null)
                    {
                        CLog.Warning("Game indicator is null. This may be mod from pre-Mod Manager 6, or developer did not specify the game. Defaulting to ME3", Settings.LogModStartup);
                        Game = MEGame.ME3;
                    }
                    else
                    {
                        Log.Error($"{ModName} has unknown game ID set for ModInfo descriptor 'game'. Valid values are ME1, ME2 or ME3. Value provided: {game}");
                        LoadFailedReason = $"This mod has an unknown game ID set for ModInfo descriptor 'game'. Valid values are ME1, ME2 or ME3. Value provided: {game}";
                        return;
                    }

                    break;
            }

            if (ModDescTargetVersion < 6 && Game != MEGame.ME3)
            {
                Log.Error($"{ModName} is designed for {game}. ModDesc versions (cmmver descriptor under ModManager section) under 6.0 do not support ME1 or ME2.");
                LoadFailedReason = $"This mod is designed for {game}. The moddesc target version is {ModDescTargetVersion}, however the first version of moddesc that supports ME1 or ME2 is 6.0.";
                return;
            }

            if (ModDescTargetVersion < 2) //Mod Manager 1 (2012)
            {
                //Ancient legacy mod that only supports ME3 basegame coalesced
                ModDescTargetVersion = 1;
                if (CheckAndCreateLegacyCoalescedJob())
                {
                    ValidMod = true;
                }

                CLog.Information($"---MOD--------END OF {ModName} STARTUP-----------", Settings.LogModStartup);
                return;
            }

            if (ModDescTargetVersion >= 2.0 && ModDescTargetVersion < 3) //Mod Manager 2 (2013)
            {
                ModDescTargetVersion = 2.0;
            }

            if (ModDescTargetVersion >= 3 && ModDescTargetVersion < 3.1) //Mod Manager 3 (2014)
            {
                ModDescTargetVersion = 3.0;
            }

            //A few mods shipped as 3.2 moddesc, however the features they targeted are officially supported in 3.1
            if (ModDescTargetVersion >= 3.1 && ModDescTargetVersion < 4.0) //Mod Manager 3.1 (2014)
            {
                ModDescTargetVersion = 3.1;
            }

            //This was in Java version - I belevie this was to ensure only tenth version of precision would be used. E.g no moddesc 4.52
            ModDescTargetVersion = Math.Round(ModDescTargetVersion * 10) / 10;
            CLog.Information("Parsing mod using moddesc target: " + ModDescTargetVersion, Settings.LogModStartup);

            #region Header Loops
            #region BASEGAME and OFFICIAL HEADERS

            if (Game == MEGame.ME3)
            {
                //We must check against official headers
                //ME1 and ME2 only supports the BASEGAME header
                var supportedOfficialHeaders = ModJob.SupportedNonCustomDLCJobHeaders;
                foreach (var header in supportedOfficialHeaders)
                {
                    if (Game != MEGame.ME3 && header != ModJob.JobHeader.BASEGAME) continue; //Skip any non-basegame offical headers for ME1/ME2
                    var headerAsString = header.ToString();
                    var jobSubdirectory = iniData[headerAsString]["moddir"];
                    if (jobSubdirectory != null)
                    {
                        CLog.Information("Found INI header with moddir specified: " + headerAsString, Settings.LogModStartup);
                        CLog.Information("Subdirectory (moddir): " + jobSubdirectory, Settings.LogModStartup);
                        //string fullSubPath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, jobSubdirectory);

                        //Replace files (ModDesc 2.0)
                        string replaceFilesSourceList = iniData[headerAsString]["newfiles"]; //Present in MM2. So this will always be read
                        string replaceFilesTargetList = iniData[headerAsString]["replacefiles"]; //Present in MM2. So this will always be read

                        //Add files (ModDesc 4.1)
                        string addFilesSourceList = ModDescTargetVersion >= 4.1 ? iniData[headerAsString]["addfiles"] : null;
                        string addFilesTargetList = ModDescTargetVersion >= 4.1 ? iniData[headerAsString]["addfilestargets"] : null;

                        //Add files Read-Only (ModDesc 4.3)
                        string addFilesTargetReadOnlyList = ModDescTargetVersion >= 4.3 ? iniData[headerAsString]["addfilesreadonlytargets"] : null;


                        //Remove files (ModDesc 4.1)
                        string removeFilesTargetList = ModDescTargetVersion >= 4.1 ? iniData[headerAsString]["removefilestargets"] : null;

                        //Check that the lists here are at least populated in one category. If none are populated then this job will do effectively nothing.
                        bool taskDoesSomething = replaceFilesSourceList != null && replaceFilesTargetList != null;
                        if (addFilesSourceList != null && addFilesTargetList != null) taskDoesSomething = true;
                        if (removeFilesTargetList != null) taskDoesSomething = true;

                        if (!taskDoesSomething)
                        {
                            Log.Error($"Mod has job header ({headerAsString}) with no tasks in add, replace, or remove lists. This header does effectively nothing. Marking mod as invalid");
                            LoadFailedReason = $"This mod has a job header ({headerAsString}) in it's moddesc.ini that no values in add, replace, or remove descriptors. This header does effectively nothing and must be removed from the mod.";
                            return;
                        }

                        List<string> replaceFilesSourceSplit = null;
                        List<string> replaceFilesTargetSplit = null;
                        if (replaceFilesSourceList != null && replaceFilesTargetList != null)
                        {
                            //Parse the newfiles and replacefiles list and ensure they have the same number of elements in them.
                            replaceFilesSourceSplit = replaceFilesSourceList.Split(';').ToList();
                            replaceFilesTargetSplit = replaceFilesTargetList.Split(';').ToList();
                            if (replaceFilesSourceSplit.Count != replaceFilesTargetSplit.Count)
                            {
                                //Mismatched source and target lists
                                Log.Error($"Mod has job header ({headerAsString}) that has mismatched newfiles and replacefiles descriptor lists. newfiles has {replaceFilesSourceSplit.Count} items, replacefiles has {replaceFilesTargetSplit.Count} items. The number of items in each list must match.");
                                LoadFailedReason = $"Job header ({headerAsString}) has mismatched newfiles and replacefiles descriptor lists. newfiles has {replaceFilesSourceSplit.Count} items, replacefiles has {replaceFilesTargetSplit.Count} items. The number of items in each list must match.";
                                return;
                            }

                            CLog.Information($"Parsing replacefiles/newfiles on {headerAsString}. Found {replaceFilesTargetSplit.Count} items in lists", Settings.LogModStartup);
                        }

                        List<string> addFilesSourceSplit = null;
                        List<string> addFilesTargetSplit = null;
                        if (addFilesSourceList != null && addFilesTargetList != null)
                        {
                            //Parse the addfiles and addfilestargets list and ensure they have the same number of elements in them.
                            addFilesSourceSplit = addFilesSourceList.Split(';').ToList();
                            addFilesTargetSplit = addFilesTargetList.Split(';').ToList();
                            if (addFilesSourceSplit.Count != addFilesTargetSplit.Count)
                            {
                                //Mismatched source and target lists
                                Log.Error($"Mod has job header ({headerAsString}) that has mismatched addfiles and addfilestargets descriptor lists. addfiles has {addFilesSourceSplit.Count} items, addfilestargets has {addFilesTargetSplit.Count} items. The number of items in each list must match.");
                                LoadFailedReason = $"Job header ({headerAsString}) has mismatched addfiles and addfilestargets descriptor lists. addfiles has {addFilesSourceSplit.Count} items, addfilestargets has {addFilesTargetSplit.Count} items. The number of items in each list must match.";
                                return;
                            }

                            CLog.Information($"Parsing addfiles/addfilestargets on {headerAsString}. Found {addFilesTargetSplit.Count} items in lists", Settings.LogModStartup);
                        }

                        //Add files read only targets
                        List<string> addFilesReadOnlySplit = null;
                        if (addFilesTargetReadOnlyList != null)
                        {
                            addFilesReadOnlySplit = addFilesTargetList.Split(';').ToList();

                            //Ensure add targets list contains this list
                            if (addFilesTargetSplit != null)
                            {
                                if (!addFilesTargetSplit.ContainsAll(addFilesReadOnlySplit, StringComparer.InvariantCultureIgnoreCase))
                                {
                                    //readonly list contains elements not contained in the targets list
                                    Log.Error($"Mod has job header ({headerAsString}) that has addfilesreadonlytargets descriptor set, however it contains items that are not part of the addfilestargets list. This is not allowed.");
                                    LoadFailedReason = $"Job header ({headerAsString}) specifies the addfilesreadonlytargets descriptor, however it contains items that are not present in the addfilestargets list. This is not allowed.";
                                    return;
                                }
                            }
                            else
                            {
                                //readonly target specified but nothing in the addfilestargets list/unspecified
                                Log.Error($"Mod has job header ({headerAsString}) that has addfilesreadonlytargets descriptor set, however there is no addfilestargets specified.");
                                LoadFailedReason = $"Job header ({headerAsString}) specifies the addfilesreadonlytargets descriptor, but the addfilestargets descriptor is not set.";
                                return;
                            }

                            CLog.Information($"Parsing addfilesreadonlytargets on {headerAsString}. Found {addFilesReadOnlySplit.Count} items in list", Settings.LogModStartup);
                        }

                        List<string> removeFilesSplit = new List<string>();

                        if (removeFilesTargetList != null)
                        {
                            removeFilesSplit = removeFilesTargetList.Split(';').ToList();
                            CLog.Information($"Parsing removefilestargets on {headerAsString}. Found {removeFilesSplit.Count} items in list", Settings.LogModStartup);

                            if (removeFilesSplit.Any(x => x.Contains("..")))
                            {
                                //Security violation: Cannot use .. in filepath
                                Log.Error($"Mod has job header ({headerAsString}) that has removefilestargets descriptor set, however at least one item in the list has a .. in it's listed file path. This is not allowed for security purposes.");
                                LoadFailedReason = $"Job header ({headerAsString}) has removefilestargets descriptor set, however at least one item in the list has a .. in it's listed file path. This is not allowed for security purposes.";
                                return;
                            }
                        }

                        //This was introduced in Mod Manager 4.1 but is considered applicable to all moddesc versions as it doesn't impact installation and is only for user convenience
                        //In Java Mod Manager, this required 4.1 moddesc
                        string jobRequirement = iniData[headerAsString]["jobdescription"];
                        CLog.Information($"Read job requirement text: {jobRequirement}", Settings.LogModStartup && jobRequirement != null);

                        //TODO: Bini support
                        //TODO: Basegame support

                        //Ensure TESTPATCH is supported by making sure we are at least on ModDesc 3 if using TESTPATCH header.
                        if (ModDescTargetVersion < 3 && header == ModJob.JobHeader.TESTPATCH)
                        {
                            Log.Error($"Mod has job header ({headerAsString}) specified, but this header is only supported when targeting ModDesc 3 or higher.");
                            LoadFailedReason = $"Job header ({headerAsString}) has been specified as part of the mod, but this header is only supported when targeting ModDesc 3 or higher.";
                            return;
                        }

                        ModJob headerJob = new ModJob(header, this);
                        headerJob.JobDirectory = jobSubdirectory;
                        headerJob.RequirementText = jobRequirement;
                        //Build replacements 
                        if (replaceFilesSourceSplit != null)
                        {
                            for (int i = 0; i < replaceFilesSourceSplit.Count; i++)
                            {
                                string destFile = replaceFilesTargetSplit[i];
                                CLog.Information($"Adding file to job installation queue: {replaceFilesSourceSplit[i]} => {destFile}", Settings.LogModStartup);
                                string failurereason = headerJob.AddFileToInstall(destFile, replaceFilesSourceSplit[i], this, ignoreLoadErrors);
                                if (failurereason != null)
                                {
                                    Log.Error($"Error occured while parsing the replace files lists for {headerAsString}: {failurereason}");
                                    LoadFailedReason = $"Error occured while parsing the replace files lists for {headerAsString}: {failurereason}";
                                    return;
                                }
                            }
                        }

                        //Build additions (vars will be null if these aren't supported by target version)
                        if (addFilesSourceSplit != null)
                        {
                            for (int i = 0; i < addFilesSourceSplit.Count; i++)
                            {
                                string destFile = addFilesTargetSplit[i];
                                CLog.Information($"Adding file to installation queue (addition): {addFilesSourceSplit[i]} => {destFile}", Settings.LogModStartup);
                                string failurereason = headerJob.AddAdditionalFileToInstall(destFile, addFilesSourceSplit[i], this, ignoreLoadErrors); //add files are layered on top
                                if (failurereason != null)
                                {
                                    Log.Error($"Error occured while parsing the add files lists for {headerAsString}: {failurereason}");
                                    LoadFailedReason = $"Error occured while parsing the add files lists for {headerAsString}: {failurereason}";
                                    return;
                                }
                            }
                        }

                        var removeFailureReason = headerJob.AddFilesToRemove(removeFilesSplit);
                        if (removeFailureReason != null)
                        {
                            Log.Error($"Error occured while parsing the remove files list for {headerAsString}: {removeFailureReason}");
                            LoadFailedReason = $"Error occured while parsing the remove files list for {headerAsString}: {removeFailureReason}";
                            return;
                        }

                        //Altfiles: Mod Manager 4.2
                        string altfilesStr = (ModDescTargetVersion >= 4.2 && headerJob.Header != ModJob.JobHeader.BALANCE_CHANGES) ? iniData[headerAsString]["altfiles"] : null;
                        if (!string.IsNullOrEmpty(altfilesStr))
                        {
                            var splits = StringStructParser.GetParenthesisSplitValues(altfilesStr);
                            if (splits.Count == 0)
                            {
                                Log.Error("Alternate files list was unable to be parsed, no items were returned from parenthesis parser.");
                                LoadFailedReason = $"Specified altfiles descriptor for header {headerAsString} did not successfully parse. Text is not empty, but no values were returned.";
                                return;
                            }
                            foreach (var split in splits)
                            {
                                AlternateFile af = new AlternateFile(split, this);
                                if (af.ValidAlternate)
                                {
                                    headerJob.AlternateFiles.Add(af);
                                }
                                else
                                {
                                    //Error is logged in constructor of AlternateFile
                                    LoadFailedReason = af.LoadFailedReason;
                                    return;
                                }
                            }
                        }

                        CLog.Information($"Successfully made mod job for {headerAsString}", Settings.LogModStartup);
                        InstallationJobs.Add(headerJob);
                    }
                }
            }

            #endregion

            #region CUSTOMDLC

            if (ModDescTargetVersion >= 3.1)
            {
                var customDLCSourceDirsStr = iniData["CUSTOMDLC"]["sourcedirs"];
                var customDLCDestDirsStr = iniData["CUSTOMDLC"]["destdirs"];

                if (customDLCSourceDirsStr != null && customDLCDestDirsStr != null)
                {
                    CLog.Information("Found CUSTOMDLC header", Settings.LogModStartup);

                    var customDLCSourceSplit = customDLCSourceDirsStr.Split(';').ToList();
                    var customDLCDestSplit = customDLCDestDirsStr.Split(';').ToList();

                    //Verify lists are the same length
                    if (customDLCSourceSplit.Count != customDLCDestSplit.Count)
                    {
                        //Mismatched source and target lists
                        Log.Error($"Mod has job header (CUSTOMDLC) that has mismatched sourcedirs and destdirs descriptor lists. sourcedirs has {customDLCSourceSplit.Count} items, destdirs has {customDLCDestSplit.Count} items. The number of items in each list must match.");
                        LoadFailedReason = $"Job header (CUSTOMDLC) has mismatched newfiles and replacefiles descriptor lists. sourcedirs has {customDLCSourceSplit.Count} items, destdirs has {customDLCDestSplit.Count} items. The number of items in each list must match.";
                        return;
                    }

                    //Security check for ..
                    if (customDLCSourceSplit.Any(x => x.Contains("..")) || customDLCDestSplit.Any(x => x.Contains("..")))
                    {
                        //Security violation: Cannot use .. in filepath
                        Log.Error($"CUSTOMDLC header sourcedirs or destdirs includes item that contains a .., which is not permitted.");
                        LoadFailedReason = $"CUSTOMDLC header sourcedirs or destdirs includes item that contains a .., which is not permitted.";
                        return;
                    }

                    //Verify folders exists
                    foreach (var f in customDLCSourceSplit)
                    {
                        if (!FilesystemInterposer.DirectoryExists(FilesystemInterposer.PathCombine(IsInArchive, ModPath, f), Archive))
                        {
                            Log.Error($"Mod has job header (CUSTOMDLC) sourcedirs descriptor specifies installation of a Custom DLC folder that does not exist in the mod folder: {f}");
                            LoadFailedReason = $"Job header (CUSTOMDLC) sourcedirs descriptor specifies installation of a Custom DLC folder that does not exist in the mod folder: {f}";
                            return;
                        }


                    }

                    //Security check: Protected folders
                    foreach (var f in customDLCDestSplit)
                    {
                        if (Utilities.IsProtectedDLCFolder(f, Game))
                        {
                            Log.Error($"Mod has job header (CUSTOMDLC) destdirs descriptor that specifies installation of a Custom DLC folder to a protected target: {f}");
                            LoadFailedReason = $"Job header (CUSTOMDLC) destdirs descriptor that specifies installation of a Custom DLC folder to a protected target: {f}. Custom DLC cannot be installed to a folder named the same as an official DLC or metadata directory.";
                            return;
                        }

                        if (!f.StartsWith("DLC_"))
                        {
                            Log.Error($"Mod has job header (CUSTOMDLC) destdirs descriptor that specifies installation of a Custom DLC folder that would install a disabled DLC: {f}. DLC folders must start with DLC_.");
                            LoadFailedReason = $"Job header (CUSTOMDLC) destdirs descriptor that specifies installation of a Custom DLC folder that would install a disabled DLC: {f}. DLC folders must start with DLC_.";
                            return;
                        }
                    }

                    ModJob customDLCjob = new ModJob(ModJob.JobHeader.CUSTOMDLC, this);
                    for (int i = 0; i < customDLCSourceSplit.Count; i++)
                    {
                        customDLCjob.CustomDLCFolderMapping[customDLCSourceSplit[i]] = customDLCDestSplit[i];
                    }

                    //Altfiles: Mod Manager 4.2
                    string altfilesStr = (ModDescTargetVersion >= 4.2) ? iniData["CUSTOMDLC"]["altfiles"] : null;
                    if (!string.IsNullOrEmpty(altfilesStr))
                    {
                        var splits = StringStructParser.GetParenthesisSplitValues(altfilesStr);
                        if (splits.Count == 0)
                        {
                            Log.Error("Alternate files list was unable to be parsed, no items were returned from parenthesis parser.");
                            LoadFailedReason = $"Specified altfiles descriptor for header CUSTOMDLC did not successfully parse. Text is not empty, but no values were returned.";
                            return;
                        }
                        foreach (var split in splits)
                        {
                            AlternateFile af = new AlternateFile(split, this);
                            if (af.ValidAlternate)
                            {
                                customDLCjob.AlternateFiles.Add(af);
                            }
                            else
                            {
                                //Error is logged in constructor of AlternateFile
                                LoadFailedReason = af.LoadFailedReason;
                                return;
                            }
                        }
                    }
                    //AltDLC: Mod Manager 4.4
                    string altdlcstr = (ModDescTargetVersion >= 4.4) ? iniData["CUSTOMDLC"]["altdlc"] : null;
                    if (!string.IsNullOrEmpty(altdlcstr))
                    {
                        var splits = StringStructParser.GetParenthesisSplitValues(altdlcstr);
                        foreach (var split in splits)
                        {
                            AlternateDLC af = new AlternateDLC(split, this);
                            if (af.ValidAlternate)
                            {
                                customDLCjob.AlternateDLCs.Add(af);
                            }
                            else
                            {
                                //Error is logged in constructor of AlternateDLC
                                LoadFailedReason = af.LoadFailedReason;
                                return;
                            }
                        }
                    }

                    CLog.Information($"Successfully made mod job for CUSTOMDLC", Settings.LogModStartup);
                    InstallationJobs.Add(customDLCjob);
                }
                else if ((customDLCSourceDirsStr != null) != (customDLCDestDirsStr != null))
                {
                    Log.Error($"{ModName} specifies only one of the two required lists for the CUSTOMDLC header. Both sourcedirs and destdirs descriptors must be set for CUSTOMDLC.");
                    LoadFailedReason = $"This mod specifies only one of the two required lists for the CUSTOMDLC header. Both sourcedirs and destdirs descriptors must be set for CUSTOMDLC.";
                    return;
                }
            }

            #endregion

            #endregion
            #region Additional Mod Items

            //Required DLC (Mod Manager 5.0)
            var requiredDLCText = ModDescTargetVersion >= 5.0 ? iniData["ModInfo"]["requireddlc"] : null;
            if (requiredDLCText != null)
            {
                var requiredDlcsSplit = requiredDLCText.Split(';').ToList();
                foreach (var reqDLC in requiredDlcsSplit)
                {
                    if (Game == Mod.MEGame.ME3)
                    {
                        if (Enum.TryParse(reqDLC, out ModJob.JobHeader header) && ModJob.HeadersToDLCNamesMap.TryGetValue(header, out var foldername))
                        {
                            RequiredDLC.Add(foldername);
                            continue;
                        }
                    } //Todo: Add support for ME1, ME2 human-readable headers. Maybe

                    if (!reqDLC.StartsWith("DLC_"))
                    {
                        Log.Error("Required DLC does not match officially supported header or start with DLC_.");
                        LoadFailedReason = $"This mod specifies required DLC but does not match a supported value or start with DLC_. The value that failed was: {reqDLC}";
                        return;
                    }
                    CLog.Information("Adding DLC requirement to mod: " + reqDLC, Settings.LogModStartup);
                    RequiredDLC.Add(reqDLC);
                }
            }

            //Outdated DLC (Mod Manager 4.4)
            var outdatedDLCText = ModDescTargetVersion >= 4.4 ? iniData["CUSTOMDLC"]["outdatedcustomdlc"] : null;
            if (!string.IsNullOrEmpty(outdatedDLCText))
            {

            }
            //Incompatible DLC (Mod Manager 6)
            //Todo: Update documentation
            var incompatibleDLCText = ModDescTargetVersion >= 6.0 ? iniData["CUSTOMDLC"]["incompatiblecustomdlc"] : null;
            if (!string.IsNullOrEmpty(incompatibleDLCText))
            {
                var incompatibleDLCSplits = incompatibleDLCText.Split(';').ToList();
                foreach (var incompat in incompatibleDLCSplits)
                {
                    //todo: check if official dlc header or official dlc name. no mod should be incompatible if official dlc is installed.
                }
                //incompatibleDLCText = incompatibleDLCSplits;
            }


            //Additional Deployment Folders (Mod Manager 5.1)
            var additonaldeploymentfoldersStr = ModDescTargetVersion >= 5.1 ? iniData["UPDATES"]["additionaldeploymentfolders"] : null;
            if (!string.IsNullOrEmpty(additonaldeploymentfoldersStr))
            {
                var addlFolderSplit = additonaldeploymentfoldersStr.Split(';').ToList();
                foreach (var addlFolder in addlFolderSplit)
                {
                    //Todo: Check to make sure this isn't contained by one of the jobs or alt files
                    if (addlFolder.Contains("..") || addlFolder.Contains("/") || addlFolder.Contains("\\"))
                    {
                        //Security violation: Cannot use .. / or \ in filepath
                        Log.Error($"UPDATES header additionaldeploymentfolders includes directory ({addlFolder}) that contains a .., \\ or /, which are not permitted.");
                        LoadFailedReason = $"UPDATES header additionaldeploymentfolders includes directory ({addlFolder}) that contains a .., \\ or /, which are not permitted.";
                        return;
                    }

                    //Check folder exists
                    if (FilesystemInterposer.DirectoryExists(FilesystemInterposer.PathCombine(IsInArchive, ModPath, addlFolder), Archive))
                    {
                        Log.Error($"UPDATES header additionaldeploymentfolders includes directory that does not exist in the mod directory: {addlFolder}");
                        LoadFailedReason = $"UPDATES header additionaldeploymentfolders includes directory that does not exist in the mod directory: {addlFolder}";
                        return;
                    }

                    AdditionalDeploymentFolders = addlFolderSplit;
                }
            }

            //Additional Root Deployment Files (Mod Manager 6.0)
            //Todo: Update documentation
            var additonaldeploymentfilesStr = ModDescTargetVersion >= 6.0 ? iniData["UPDATES"]["additionaldeploymentfiles"] : null;
            if (!string.IsNullOrEmpty(additonaldeploymentfilesStr))
            {
                var addlFileSplit = additonaldeploymentfilesStr.Split(';').ToList();
                foreach (var addlFile in addlFileSplit)
                {
                    if (addlFile.Contains("..") || addlFile.Contains("/") || addlFile.Contains("\\"))
                    {
                        //Security violation: Cannot use .. / or \ in filepath
                        Log.Error($"UPDATES header additionaldeploymentfiles includes file ({addlFile}) that contains a .., \\ or /, which are not permitted.");
                        LoadFailedReason = $"UPDATES header additionaldeploymentfiles includes file ({addlFile}) that contains a .., \\ or /, which are not permitted.";
                        return;
                    }

                    //Check file exists
                    if (!FilesystemInterposer.FileExists(FilesystemInterposer.PathCombine(IsInArchive, ModPath, addlFile), Archive))
                    {
                        Log.Error($"UPDATES header additionaldeploymentfiles includes file that does not exist in the mod directory: {addlFile}");
                        LoadFailedReason = $"UPDATES header additionaldeploymentfiles includes file that does not exist in the mod directory: {addlFile}";
                        return;
                    }

                    AdditionalDeploymentFiles = addlFileSplit;
                }
            }

            #endregion

            #region Backwards Compatibilty

            //Mod Manager 2.0 supported "modcoal" flag that would replicate Mod Manager 1.0 functionality of coalesced swap since basegame jobs at the time
            //were not yet supportedd

            string modCoalFlag = ModDescTargetVersion == 2 ? iniData["ModInfo"]["modcoal"] : null;
            //This check could be rewritten to simply check for non zero string. However, for backwards compatibility sake, we will keep the original
            //method of checking in place.
            if (modCoalFlag != null && Int32.TryParse(modCoalFlag, out int modCoalInt) && modCoalInt != 0)
            {
                CLog.Information("Mod targets ModDesc 2.0, found modcoal flag", Settings.LogModStartup);
                if (!CheckAndCreateLegacyCoalescedJob())
                {
                    CLog.Information($"---MOD--------END OF {ModName} STARTUP-----------", Settings.LogModStartup);
                    return;
                }
            }

            #endregion

            //Thread.Sleep(500);
            if (InstallationJobs.Count > 0)
            {
                CLog.Information($"Finalizing: {InstallationJobs.Count} installation job(s) were found.", Settings.LogModStartup);
                ValidMod = true;
            }
            else if (emptyModIsOK) //Empty Load OK is used by Mixins. This may be redone for MM6
            {
                CLog.Information($"Finalizing: No installation jobs were found, but empty mods are allowed in this loading session.", Settings.LogModStartup);
                ValidMod = true;
            }
            else
            {
                Log.Error("No installation jobs were specified. This mod does nothing.");
                LoadFailedReason = "No installation jobs were specified. This mod does nothing.";
            }

            CLog.Information($"---MOD--------END OF {ModName} STARTUP-----------", Settings.LogModStartup);
        }


        private bool CheckAndCreateLegacyCoalescedJob()
        {
            var legacyCoalFile = FilesystemInterposer.PathCombine(IsInArchive, ModPath, "Coalesced.bin");
            if (!ignoreLoadErrors && !FilesystemInterposer.FileExists(legacyCoalFile, Archive))
            {
                if (ModDescTargetVersion == 1.0)
                {
                    //Mod Manager 1/1.1
                    Log.Error($"{ModName} is a legacy mod (cmmver 1.0). This moddesc version requires a Coalesced.bin file in the same folder as the moddesc.ini file, but one was not found.");
                    LoadFailedReason = $"This mod is a legacy mod (cmmver 1.0). This moddesc version requires a Coalesced.bin file in the same folder as the moddesc.ini file, but one was not found.";
                }
                else
                {
                    //Mod Manager 2
                    Log.Error($"{ModName} specifies modcoal descriptor for cmmver 2.0, but the local Coalesced file doesn't exist: {legacyCoalFile}");
                    LoadFailedReason = $"This mod specifies modcoal descriptor for cmmver 2.0, but the local Coalesced file doesn't exist: {legacyCoalFile}";
                }

                return false;
            }

            ModJob basegameJob = new ModJob(ModJob.JobHeader.BASEGAME);
            string failurereason = basegameJob.AddFileToInstall(@"BIOGame\CookedPCConsole\Coalesced.bin", "Coalesced.bin", this, ignoreLoadErrors);
            if (failurereason != null)
            {
                Log.Error($"Error occured while creating basegame job for legacy 1.0 mod: {failurereason}");
                LoadFailedReason = $"Error occured while creating basegame job for legacy 1.0 mod: {failurereason}";
                return false;
            }
            InstallationJobs.Add(basegameJob);
            return true;
        }

        public void ExtractFromArchive(string archivePath, bool compressPackages, Action<string> updateTextCallback = null, Action<ProgressEventArgs> extractingCallback = null)
        {
            if (!IsInArchive) throw new Exception("Cannot extract a mod that is not part of an archive.");
            var modDirectory = Utilities.GetModDirectoryForGame(Game);
            var sanitizedPath = Path.Combine(modDirectory, Utilities.SanitizePath(ModName));
            if (Directory.Exists(sanitizedPath))
            {
                //Will delete on import
                //Todo: Delete directory/s
            }

            Directory.CreateDirectory(sanitizedPath);


            using (var archiveFile = new SevenZipExtractor(archivePath))
            {
                var fileIndicesToExtract = new List<int>();
                foreach (var info in archiveFile.ArchiveFileData)
                {
                    bool fileAdded = false;
                    //moddesc.ini
                    if (info.FileName == ModDescPath)
                    {
                        Debug.WriteLine("Add file to extraction list: " + info.FileName);
                        fileIndicesToExtract.Add(info.Index);
                        continue;
                    }

                    //Check each job
                    foreach (ModJob job in InstallationJobs)
                    {
                        //Custom DLC folders
                        #region Extract Custom DLC
                        if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                        {
                            foreach (var localCustomDLCFolder in job.CustomDLCFolderMapping.Keys)
                            {
                                if (info.FileName.StartsWith(FilesystemInterposer.PathCombine(IsInArchive, ModPath, localCustomDLCFolder)))
                                {
                                    Debug.WriteLine("Add file to extraction list: " + info.FileName);
                                    fileIndicesToExtract.Add(info.Index);
                                    fileAdded = true;
                                    break;
                                }
                            }

                            if (fileAdded) break;

                            //Alternate files
                            foreach (var alt in job.AlternateFiles)
                            {
                                if (alt.AltFile != null && info.FileName.Equals(FilesystemInterposer.PathCombine(IsInArchive, ModPath, alt.AltFile), StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Debug.WriteLine("Add alternate file to extraction list: " + info.FileName);
                                    fileIndicesToExtract.Add(info.Index);
                                    fileAdded = true;
                                    break;
                                }
                            }
                            if (fileAdded) break;

                            //Alternate DLC
                            foreach (var alt in job.AlternateDLCs)
                            {
                                if (info.FileName.StartsWith(FilesystemInterposer.PathCombine(IsInArchive, ModPath, alt.AlternateDLCFolder), StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Debug.WriteLine("Add alternate dlc file to extraction list: " + info.FileName);
                                    fileIndicesToExtract.Add(info.Index);
                                    fileAdded = true;
                                    break;
                                }
                            }
                            if (fileAdded) break;
                            #endregion
                        }
                        else
                        {
                            #region Official headers
                            foreach (var inSubDirFile in job.FilesToInstall.Values)
                            {
                                //var inArchivePath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, inSubDirFile);
                                if (info.FileName.Equals(inSubDirFile, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Debug.WriteLine("Add file to extraction list: " + info.FileName);
                                    fileIndicesToExtract.Add(info.Index);
                                    fileAdded = true;
                                    break;
                                }


                            }
                            if (fileAdded) break;
                            //Alternate files
                            foreach (var alt in job.AlternateFiles)
                            {
                                if (alt.AltFile != null && info.FileName.Equals(FilesystemInterposer.PathCombine(IsInArchive, ModPath, alt.AltFile), StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Debug.WriteLine("Add alternate file to extraction list: " + info.FileName);
                                    fileIndicesToExtract.Add(info.Index);
                                    fileAdded = true;
                                    break;
                                }
                            }
                            if (fileAdded) break;

                            #endregion
                        }
                    }
                }
                archiveFile.Extracting += (sender, args) =>
                {
                    extractingCallback?.Invoke(args);
                };

                string outputFilePathMapping(string entryPath)
                {
                    //Archive path might start with a \. Substring may return value that start with a \
                    var subModPath = entryPath/*.TrimStart('\\')*/.Substring(ModPath.Length).TrimStart('\\');
                    var path = Path.Combine(sanitizedPath, subModPath);
                    //Debug.WriteLine("remapping output: " + entryPath + " -> " + path);
                    return path;
                }
                archiveFile.ExtractFiles(sanitizedPath, outputFilePathMapping, fileIndicesToExtract.ToArray());
                ModPath = sanitizedPath;
                if (IsVirtualized)
                {
                    var parser = new IniDataParser().Parse(VirtualizedIniText);
                    parser["ModInfo"]["modver"] = ModVersionString; //In event relay service resolved this
                    File.WriteAllText(Path.Combine(ModPath, "moddesc.ini"), parser.ToString());
                }

                int packagesCompressed = 0;
                if (compressPackages)
                {
                    var packages = Utilities.GetPackagesInDirectory(ModPath, true);
                    extractingCallback?.Invoke(new ProgressEventArgs((byte)(packagesCompressed * 100.0 / packages.Count), 0));
                    foreach (var package in packages)
                    {
                        updateTextCallback?.Invoke($"Compressing {Path.GetFileName(package)}");
                        Log.Information("Compressing package: " + package);
                        var p = MEPackageHandler.OpenMEPackage(package);
                        p.save(true);

                        packagesCompressed++;
                        extractingCallback?.Invoke(new ProgressEventArgs((byte)(packagesCompressed * 100.0 / packages.Count), 0));
                    }
                }
            }
        }
    }
}