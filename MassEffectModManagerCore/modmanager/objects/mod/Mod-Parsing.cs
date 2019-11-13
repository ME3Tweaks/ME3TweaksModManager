using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using IniParser.Parser;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using ME3Explorer.Packages;
using Serilog;
using SevenZip;

namespace MassEffectModManagerCore.modmanager
{
    public partial class Mod : INotifyPropertyChanged
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

        public int NexusModID { get; private set; }

        // Constants

        //Mod variables
        public bool ValidMod;
        public List<ModJob> InstallationJobs = new List<ModJob>();

        //private List<ModJob> jobs;

        public Dictionary<string, string> HumanReadableCustomDLCNames = new Dictionary<string, string>();
        public MEGame Game { get; set; }
        public string ModName { get; set; }
        public string ModDeveloper { get; set; }
        public string ModDescription { get; set; }
        public int ModModMakerID { get; set; }

        /// <summary>
        /// Indicates if this mod has the relevant information attached to it for updates. That is, classic update code, modmaker id, or nexusmods ID
        /// </summary>
        public bool IsUpdatable
        {
            get
            {
                if (ModClassicUpdateCode > 0) return true;
                //if (ModModMakerID > 0) return true; //Not available yet
                //Nexus update check?

                return false;
            }
        }
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
                if (ModClassicUpdateCode > 0 && Settings.DeveloperMode)
                {
                    sb.AppendLine($"Update code: {ModClassicUpdateCode}");
                }
                //else if (NexusModID > 0 && Settings.DeveloperMode)
                //{
                //    sb.AppendLine($"NexusMods ID: {NexusModID}");
                //}

                sb.AppendLine("-------Installation information--------");
                if (Settings.DeveloperMode)
                {
                    sb.AppendLine("Targets ModDesc " + ModDescTargetVersion);
                }
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

                //todo: Add potentials through AlternateDLC

                if (RequiredDLC.Count > 0)
                {
                    sb.AppendLine("Requires the following DLC to install: ");
                    foreach (var reqDLC in RequiredDLC)
                    {
                        string name = ThirdPartyServices.GetThirdPartyModInfo(reqDLC, Game)?.modname ?? reqDLC;
                        sb.AppendLine($" - {name}");
                    }
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
        public Version ParsedModVersion { get; set; }
        public string ModWebsite { get; set; } = ""; //not null default I guess.
        public double ModDescTargetVersion { get; set; }

        public List<string> OutdatedCustomDLC = new List<string>();
        public List<string> IncompatibleDLC = new List<string>();
        public int ModClassicUpdateCode { get; set; }
        public string LoadFailedReason { get; set; }
        public List<string> RequiredDLC = new List<string>();
        private List<string> AdditionalDeploymentFolders = new List<string>();
        private List<string> AdditionalDeploymentFiles = new List<string>();
        private bool emptyModIsOK;

        public string ModPath { get; private set; }

        public SevenZipExtractor Archive;

        public string ModDescPath => FilesystemInterposer.PathCombine(IsInArchive, ModPath, "moddesc.ini");


        public bool IsInArchive { get; }
        public bool IsVirtualized { get; private set; }
        public string OriginalArchiveHash { get; private set; }

        private readonly string VirtualizedIniText;
        private readonly string ArchivePath;

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
            ArchivePath = archive.FileName;
            IsInArchive = true;
            try
            {
                loadMod(iniText, MEGame.Unknown);
            }
            catch (Exception e)
            {
                LoadFailedReason = "Error occured parsing archive moddesc.ini " + moddescArchiveEntry.FileName + ": " + e.Message;
            }

            //Retain reference to archive as we might need this.
            //Archive = null; //dipose of the mod
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
            ArchivePath = archive.FileName;
            IsInArchive = true;
            IsVirtualized = true;
            VirtualizedIniText = iniText;
            Log.Information("Loading virtualized moddesc.ini");
            try
            {
                loadMod(iniText, MEGame.Unknown);
            }
            catch (Exception e)
            {
                LoadFailedReason = "Error occured parsing virtualized moddesc.ini: " + e.Message;
            }
        }

        private readonly string[] GameFileExtensions = { ".u", ".upk", ".sfm", ".pcc", ".bin", ".tlk", ".cnd", ".ini", ".afc", ".tfc", ".dlc", ".sfar", ".txt", ".bik", ".bmp" };

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
            //Check for integer value only
            if (int.TryParse(ModVersionString, out var intVersion))
            {
                ModVersionString += ".0";
            }
            Version.TryParse(ModVersionString, out var parsedValue);
            ParsedModVersion = parsedValue;

            ModWebsite = iniData["ModInfo"]["modsite"] ?? DefaultWebsite;

            //updates
            int.TryParse(iniData["ModInfo"]["modid"], out int modmakerId);
            ModModMakerID = modmakerId;

            int.TryParse(iniData["ModInfo"]["updatecode"], out int modupdatecode);
            ModClassicUpdateCode = modupdatecode;

            int.TryParse(iniData["ModInfo"]["nexuscode"], out int nexuscode);
            NexusModID = nexuscode;

            if (NexusModID == 0 && ModModMakerID == 0 && ModClassicUpdateCode == 0 && !string.IsNullOrWhiteSpace(ModWebsite) && ModWebsite.Contains("nexusmods.com/masseffect"))
            {
                try
                {
                    //try to extract nexus mods ID

                    string nexusId = ModWebsite.Substring(ModWebsite.IndexOf("nexusmods.com/masseffect")).Substring("nexusmods.com/masseffect".Length);// http:/
                    if (!nexusId.StartsWith("/"))
                    {
                        nexusId = nexusId.Substring(nexusId.IndexOf("/")).TrimStart('/'); //3/mods/NUM (removes 3/)
                    }
                    nexusId = nexusId.Substring(nexusId.IndexOf("/")).TrimStart('/'); //mods/NUM (removes mods/)

                    if (int.TryParse(nexusId, out var nid))
                    {
                        NexusModID = nid;
                    }
                    else
                    {
                        nexusId = nexusId.Substring(0, nexusId.IndexOf('/'));
                        if (int.TryParse(nexusId, out var nid2))
                        {
                            NexusModID = nid2;
                        }
                    }
                }
                catch (Exception)
                {
                    //don't bother.
                }
            }

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

            var supportedOfficialHeaders = ModJob.GetSupportedNonCustomDLCHeaders(Game);


            //We must check against official headers
            //ME2 doesn't support anything but basegame.
            foreach (var header in supportedOfficialHeaders)
            {
                //if (Game != MEGame.ME3 && header != ModJob.JobHeader.BASEGAME) continue; //Skip any non-basegame offical headers for ME1/ME2
                var headerAsString = header.ToString();
                var jobSubdirectory = iniData[headerAsString]["moddir"];
                if (jobSubdirectory != null)
                {
                    jobSubdirectory = jobSubdirectory.Replace('/', '\\').TrimStart('\\');
                    CLog.Information("Found INI header with moddir specified: " + headerAsString, Settings.LogModStartup);
                    CLog.Information("Subdirectory (moddir): " + jobSubdirectory, Settings.LogModStartup);
                    //string fullSubPath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, jobSubdirectory);

                    bool directoryMatchesGameStructure = false;
                    if (ModDescTargetVersion >= 6.0) bool.TryParse(iniData[headerAsString]["gamedirectorystructure"], out directoryMatchesGameStructure);

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

                    //Don't support add/remove files on anything except ME3, unless basegame.
                    List<string> addFilesSourceSplit = null;
                    List<string> addFilesTargetSplit = null;
                    List<string> removeFilesSplit = new List<string>();
                    if (Game == Mod.MEGame.ME3 || header == ModJob.JobHeader.BASEGAME)
                    {

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


                        //TODO: Bini support

                        //Ensure TESTPATCH is supported by making sure we are at least on ModDesc 3 if using TESTPATCH header.
                        //ME3 only
                        if (ModDescTargetVersion < 3 && header == ModJob.JobHeader.TESTPATCH)
                        {
                            Log.Error($"Mod has job header ({headerAsString}) specified, but this header is only supported when targeting ModDesc 3 or higher.");
                            LoadFailedReason = $"Job header ({headerAsString}) has been specified as part of the mod, but this header is only supported when targeting ModDesc 3 or higher.";
                            return;
                        }

                    }

                    //This was introduced in Mod Manager 4.1 but is considered applicable to all moddesc versions as it doesn't impact installation and is only for user convenience
                    //In Java Mod Manager, this required 4.1 moddesc
                    string jobRequirement = iniData[headerAsString]["jobdescription"];
                    CLog.Information($"Read job requirement text: {jobRequirement}", Settings.LogModStartup && jobRequirement != null);

                    ModJob headerJob = new ModJob(header, this);
                    headerJob.JobDirectory = jobSubdirectory.Replace('/', '\\');
                    headerJob.RequirementText = jobRequirement;



                    //Build replacements 
                    if (replaceFilesSourceSplit != null)
                    {
                        for (int i = 0; i < replaceFilesSourceSplit.Count; i++)
                        {
                            if (directoryMatchesGameStructure)
                            {
                                var sourceDirectory = FilesystemInterposer.PathCombine(IsInArchive, ModPath, jobSubdirectory, replaceFilesSourceSplit[i]);
                                var destGameDirectory = replaceFilesTargetSplit[i];
                                if (FilesystemInterposer.DirectoryExists(sourceDirectory, Archive))
                                {
                                    var files = FilesystemInterposer.DirectoryGetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories, Archive).Select(x => x.Substring((ModPath.Length > 0 ? (ModPath.Length + 1) : 0) + jobSubdirectory.Length).TrimStart('\\')).ToList();
                                    foreach (var file in files)
                                    {
                                        if (GameFileExtensions.Contains(Path.GetExtension(file), StringComparer.InvariantCultureIgnoreCase))
                                        {
                                            var destFile = destGameDirectory + file.Substring(replaceFilesSourceSplit[i].Length);
                                            CLog.Information($"Adding file to job installation queue: {file} => {destFile}", Settings.LogModStartup);
                                            string failurereason = headerJob.AddPreparsedFileToInstall(destFile, file, this);
                                            if (failurereason != null)
                                            {
                                                Log.Error($"Error occured while automapping the replace files lists for {headerAsString}: {failurereason}. This is likely a bug in M3, please report it to Mgamerz");
                                                LoadFailedReason = $"Error occured while automapping the replace files lists for {headerAsString}: {failurereason}. This is likely a bug in M3, please report it to Mgamerz.";
                                                return;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Log.Error($"Error occured while parsing the replace files lists for {headerAsString}:  source directory {sourceDirectory} was not found and the gamedirectorystructure flag was used on this job.");
                                    LoadFailedReason = $"Error occured while parsing the replace files lists for {headerAsString}: source directory {sourceDirectory} was not found and the gamedirectorystructure flag was used on this job.";
                                    return;
                                }
                            }
                            else
                            {
                                string destFile = replaceFilesTargetSplit[i];
                                CLog.Information($"Adding file to job installation queue: {replaceFilesSourceSplit[i]} => {destFile}", Settings.LogModStartup);
                                string failurereason = headerJob.AddFileToInstall(destFile, replaceFilesSourceSplit[i], this);
                                if (failurereason != null)
                                {
                                    Log.Error($"Error occured while parsing the replace files lists for {headerAsString}: {failurereason}");
                                    LoadFailedReason = $"Error occured while parsing the replace files lists for {headerAsString}: {failurereason}";
                                    return;
                                }
                            }
                        }
                    }

                    //Build additions (vars will be null if these aren't supported by target version)
                    if (addFilesSourceSplit != null && !directoryMatchesGameStructure)
                    {
                        for (int i = 0; i < addFilesSourceSplit.Count; i++)
                        {
                            string destFile = addFilesTargetSplit[i];
                            CLog.Information($"Adding file to installation queue (addition): {addFilesSourceSplit[i]} => {destFile}", Settings.LogModStartup);
                            string failurereason = headerJob.AddAdditionalFileToInstall(destFile, addFilesSourceSplit[i], this); //add files are layered on top
                            if (failurereason != null)
                            {
                                Log.Error($"Error occured while parsing the add files lists for {headerAsString}: {failurereason}");
                                LoadFailedReason = $"Error occured while parsing the add files lists for {headerAsString}: {failurereason}";
                                return;
                            }
                        }
                    }

                    var removeFailureReason = headerJob.AddFilesToRemove(removeFilesSplit);
                    if (removeFailureReason != null && !directoryMatchesGameStructure)
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

            #endregion

            #region CUSTOMDLC

            if (ModDescTargetVersion >= 3.1)
            {
                var customDLCSourceDirsStr = iniData["CUSTOMDLC"]["sourcedirs"];
                var customDLCDestDirsStr = iniData["CUSTOMDLC"]["destdirs"];
                //ALT DLC: Mod Manager 4.4
                //This behavior changed in Mod Manager 6 to allow no sourcedirs/destdirs if a custom dlc will only be added on a condition
                string altdlcstr = (ModDescTargetVersion >= 4.4) ? iniData["CUSTOMDLC"]["altdlc"] : null;


                if ((customDLCSourceDirsStr != null && customDLCDestDirsStr != null) || !string.IsNullOrEmpty(altdlcstr))
                {
                    CLog.Information("Found CUSTOMDLC header", Settings.LogModStartup);
                    ModJob customDLCjob = new ModJob(ModJob.JobHeader.CUSTOMDLC, this);

                    if (customDLCSourceDirsStr != null && customDLCDestDirsStr != null)
                    {
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
                        for (int i = 0; i < customDLCSourceSplit.Count; i++)
                        {
                            customDLCjob.CustomDLCFolderMapping[customDLCSourceSplit[i]] = customDLCDestSplit[i];
                        }
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

                    //Custom DLC names: Mod Manager 6 (but can be part of any spec as it's only cosmetic)
                    HumanReadableCustomDLCNames = iniData["CUSTOMDLC"].Where(x => x.KeyName.StartsWith("DLC_")).ToDictionary(mc => mc.KeyName, mc => mc.Value);

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

            #region BALANCE_CHANGES (ME3 ONLY)

            var balanceChangesDirectory = (Game == MEGame.ME3 && ModDescTargetVersion >= 4.3) ? iniData[ModJob.JobHeader.BALANCE_CHANGES.ToString()]["moddir"] : null;
            if (balanceChangesDirectory != null)
            {
                CLog.Information("Found BALANCE_CHANGES header", Settings.LogModStartup);
                CLog.Information("Subdirectory (moddir): " + balanceChangesDirectory, Settings.LogModStartup);
                //string fullSubPath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, jobSubdirectory);

                //In MM5.1 or lower you would have to specify the target. In MM6 you can only specify a single source and it must be a .bin file.
                string replaceFilesSourceList = iniData[ModJob.JobHeader.BALANCE_CHANGES.ToString()]["newfiles"];
                if (replaceFilesSourceList != null)
                {
                    //Parse the newfiles and replacefiles list and ensure they have the same number of elements in them.
                    var replaceFilesSourceSplit = replaceFilesSourceList.Split(';').ToList();
                    if (replaceFilesSourceSplit.Count == 1)
                    {
                        //Only 1 file is allowed here.
                        string balanceFile = replaceFilesSourceSplit[0];
                        if (!balanceFile.EndsWith(".bin"))
                        {
                            //Invalid file
                            Log.Error("Balance changes file must be a .bin file.");
                            LoadFailedReason = $"This mod specifies a balance changes file, but the provided file is not a .bin file.";
                            return;
                        }
                        ModJob balanceJob = new ModJob(ModJob.JobHeader.BALANCE_CHANGES);
                        balanceJob.AddFileToInstall(@"Binaries\win32\asi\ServerCoalesced.bin", balanceChangesDirectory + '\'' + balanceFile, this);
                        CLog.Information($"Successfully made mod job for {balanceJob.Header}", Settings.LogModStartup);
                        InstallationJobs.Add(balanceJob);
                    }
                    else
                    {
                        Log.Error($"Balance changes newfile descriptor only allows 1 entry in the list, but {replaceFilesSourceSplit.Count} were parsed.");
                        LoadFailedReason = $"This mod specifies that balance changes will be modified, but provides an invalid amount of files to install. BALANCE_CHANGES only supports 1 item in it's newfiles descriptor, however {replaceFilesSourceSplit.Count} were parsed.";
                        return;
                    }

                }
            }


            #endregion

            #region CONFIG FILES FOR ME1 AND ME2

            if (ModDescTargetVersion >= 6 && Game < MEGame.ME3)
            {

                if (Game == MEGame.ME1)
                {
                    var jobSubdirectory = iniData[ModJob.JobHeader.ME1_CONFIG.ToString()]["moddir"];
                    if (!string.IsNullOrWhiteSpace(jobSubdirectory))
                    {
                        var configfilesStr = iniData["ME1_CONFIG"]["configfiles"];
                        if (string.IsNullOrWhiteSpace(configfilesStr))
                        {
                            Log.Error("ME1_CONFIG job was specified but configfiles descriptor is empty or missing. Remove this header if you are not using this task.");
                            LoadFailedReason = $"Mod specifies ME1_CONFIG job, but the configfiles descriptor is empty or missing. Remove this header if you are not using this task.";
                            return;
                        }
                        var configFilesSplit = configfilesStr.Split(';').ToList();
                        ModJob me1ConfigJob = new ModJob(ModJob.JobHeader.ME1_CONFIG, this);
                        me1ConfigJob.JobDirectory = jobSubdirectory;
                        foreach (var configFilename in configFilesSplit)
                        {
                            if (allowedConfigFilesME1.Contains(configFilename, StringComparer.InvariantCultureIgnoreCase))
                            {
                                me1ConfigJob.AddFileToInstall(configFilename, configFilename, this);
                            }
                            else
                            {
                                Log.Error("ME1_CONFIG job's configfiles descriptor contains an unsupported config file: " + configFilename);
                                LoadFailedReason = "ME1_CONFIG job's configfiles descriptor contains an unsupported config file: " + configFilename;
                                return;
                            }
                        }
                        CLog.Information($"Successfully made mod job for {ModJob.JobHeader.ME1_CONFIG}", Settings.LogModStartup);
                        InstallationJobs.Add(me1ConfigJob);
                    }
                }
            }


            #endregion

            #endregion
            #region Additional Mod Items

            //Required DLC (Mod Manager 5.0)
            var requiredDLCText = ModDescTargetVersion >= 5.0 ? iniData["ModInfo"]["requireddlc"] : null;
            if (!string.IsNullOrWhiteSpace(requiredDLCText))
            {
                var requiredDlcsSplit = requiredDLCText.Split(';').ToList();
                foreach (var reqDLC in requiredDlcsSplit)
                {
                    switch (Game)
                    {
                        case MEGame.ME1:
                            if (Enum.TryParse(reqDLC, out ModJob.JobHeader header1) && ModJob.GetHeadersToDLCNamesMap(MEGame.ME1).TryGetValue(header1, out var foldername1))
                            {
                                RequiredDLC.Add(foldername1);
                                CLog.Information("Adding DLC requirement to mod: " + foldername1, Settings.LogModStartup);
                                continue;
                            }
                            break;
                        case MEGame.ME2:
                            if (Enum.TryParse(reqDLC, out ModJob.JobHeader header2) && ModJob.GetHeadersToDLCNamesMap(MEGame.ME2).TryGetValue(header2, out var foldername2))
                            {
                                RequiredDLC.Add(foldername2);
                                CLog.Information("Adding DLC requirement to mod: " + foldername2, Settings.LogModStartup);
                                continue;
                            }
                            break;
                        case MEGame.ME3:
                            if (Enum.TryParse(reqDLC, out ModJob.JobHeader header3) && ModJob.GetHeadersToDLCNamesMap(MEGame.ME3).TryGetValue(header3, out var foldername3))
                            {
                                RequiredDLC.Add(foldername3);
                                CLog.Information("Adding DLC requirement to mod: " + foldername3, Settings.LogModStartup);
                                continue;
                            }
                            break;
                    }

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
                var outdatedCustomDLCDLCSplits = outdatedDLCText.Split(';').Select(x => x.Trim()).ToList();
                foreach (var outdated in outdatedCustomDLCDLCSplits)
                {
                    if (MEDirectories.OfficialDLC(Game).Contains(outdated, StringComparer.InvariantCultureIgnoreCase))
                    {
                        Log.Error($"Outdated Custom DLC cannot contain an official DLC: " + outdated);
                        LoadFailedReason = $"CustomDLC header contains an item in outdatedcustomdlc list that is an official DLC: {outdated}. This is not allowed.";
                        return;
                    }
                }
                OutdatedCustomDLC.ReplaceAll(outdatedCustomDLCDLCSplits);

            }

            //Incompatible DLC (Mod Manager 6)
            //Todo: Update documentation
            var incompatibleDLCText = ModDescTargetVersion >= 6.0 ? iniData["CUSTOMDLC"]["incompatiblecustomdlc"] : null;
            if (!string.IsNullOrEmpty(incompatibleDLCText))
            {
                var incompatibleDLCSplits = incompatibleDLCText.Split(';').Select(x => x.Trim()).ToList();
                foreach (var incompat in incompatibleDLCSplits)
                {
                    if (MEDirectories.OfficialDLC(Game).Contains(incompat, StringComparer.InvariantCultureIgnoreCase))
                    {
                        Log.Error($"Incompatible Custom DLC cannot contain an official DLC: " + incompat);
                        LoadFailedReason = $"CustomDLC header contains an item in incompatibledlc list that is an official DLC: {incompat}. This is not allowed.";
                        return;
                    }
                }
                IncompatibleDLC.ReplaceAll(incompatibleDLCSplits);
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
                    if (!FilesystemInterposer.DirectoryExists(FilesystemInterposer.PathCombine(IsInArchive, ModPath, addlFolder), Archive))
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

            //Archive file hash for update checks
            OriginalArchiveHash = iniData["UPDATES"]["originalarchivehash"];

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

        private static readonly string[] allowedConfigFilesME1 = { "BIOCredits.ini", "BioEditor.ini", "BIOEngine.ini", "BIOGame.ini", "BIOGuiResources.ini", "BIOInput.ini", "BIOParty.in", "BIOQA.ini" };
        private bool CheckAndCreateLegacyCoalescedJob()
        {
            var legacyCoalFile = FilesystemInterposer.PathCombine(IsInArchive, ModPath, "Coalesced.bin");
            if (!FilesystemInterposer.FileExists(legacyCoalFile, Archive))
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
            string failurereason = basegameJob.AddFileToInstall(@"BIOGame\CookedPCConsole\Coalesced.bin", "Coalesced.bin", this);
            if (failurereason != null)
            {
                Log.Error($"Error occured while creating basegame job for legacy 1.0 mod: {failurereason}");
                LoadFailedReason = $"Error occured while creating basegame job for legacy 1.0 mod: {failurereason}";
                return false;
            }
            InstallationJobs.Add(basegameJob);
            return true;
        }
    }
}