using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IniParser.Parser;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.gameini;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using ME3Explorer.Packages;
using Serilog;
using SevenZip;

namespace MassEffectModManagerCore.modmanager
{
    [DebuggerDisplay("Mod - {ModName}")] //do not localize
    public partial class Mod : INotifyPropertyChanged
    {
        public enum MEGame
        {
            Unknown = 0,
            ME1,
            ME2,
            ME3
        }

        /// <summary>
        /// The default website value, to indicate one was not set. This value must be set to a valid url or navigation request in UI binding may not work.
        /// </summary>
        public const string DefaultWebsite = @"http://example.com"; //this is required to prevent exceptions when binding the navigateuri
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The numerical ID for a mod on the respective game's NexusMods page. This is automatically parsed from the ModWebsite if this is not explicitly set and the ModWebsite attribute is a nexusmods url.
        /// </summary>
        public int NexusModID { get; set; }
        /// <summary>
        /// Indicates if this is a valid mod or not.
        /// </summary>
        public bool ValidMod { get; private set; }
        /// <summary>
        /// List of installation jobs (aka Task Headers)
        /// </summary>
        public List<ModJob> InstallationJobs = new List<ModJob>();
        /// <summary>
        /// Mapping of Custom DLC names to their human-readable versions.
        /// </summary>
        public Dictionary<string, string> HumanReadableCustomDLCNames = new Dictionary<string, string>();
        /// <summary>
        /// What game this mod can install to.
        /// </summary>
        public MEGame Game { get; set; }
        /// <summary>
        /// The mod's name.
        /// </summary>
        public string ModName { get; set; }
        /// <summary>
        /// Developer of the mod
        /// </summary>
        public string ModDeveloper { get; set; }
        /// <summary>
        /// The description for the mod, as written in moddesc.ini
        /// </summary>
        public string ModDescription { get; set; }
        /// <summary>
        /// The ID for updating on ModMaker. This value will be 0 if none is set.
        /// </summary>
        public int ModModMakerID { get; set; }
        /// <summary>
        /// Indicates this is an 'unofficial' mod that was imported from the game's DLC directory.
        /// </summary>
        public bool IsUnofficial { get; set; }
        /// <summary>
        /// This variable is only set if IsInArchive is true
        /// </summary>
        public long SizeRequiredtoExtract { get; set; }
        /// <summary>
        /// Used with DLC imported from the game directory, this is used to track what version of MM imported the DLC, which may be used in the future check for things like texture tags.
        /// </summary>
        public int ImportedByBuild { get; set; }
        /// <summary>
        /// List of files that will always be deleted locally when servicing an update on a client. This has mostly been deprecated for new mods.
        /// </summary>
        public ObservableCollectionExtended<string> UpdaterServiceBlacklistedFiles { get; } = new ObservableCollectionExtended<string>();
        /// <summary>
        /// The server folder that this mod will be published to when using the ME3Tweaks Updater Service
        /// </summary>
        public string UpdaterServiceServerFolder { get; set; }
        public string UpdaterServiceServerFolderShortname
        {
            get
            {
                if (UpdaterServiceServerFolder == null) return null;
                if (UpdaterServiceServerFolder.Contains('/'))
                {
                    return UpdaterServiceServerFolder.Substring(UpdaterServiceServerFolder.LastIndexOf('/') + 1);
                }
                return UpdaterServiceServerFolder;
            }
        }

        /// <summary>
        /// Indicates if this mod has the relevant information attached to it for updates. That is, classic update code, modmaker id, or nexusmods ID
        /// </summary>
        public bool IsUpdatable
        {
            get
            {
                if (ModClassicUpdateCode > 0) return true;
                if (ModModMakerID > 0) return true;
                if (NexusModID > 0) return true;
                return false;
            }
        }

        /// <summary>
        /// Indicates if this mod has the relevant information attached to it for updates from ME3Tweaks That is, classic update code or modmaker id
        /// </summary>
        public bool IsME3TweaksUpdatable
        {
            get
            {
                if (ModClassicUpdateCode > 0) return true;
                if (ModModMakerID > 0) return true;
                return false;
            }
        }

        /// <summary>
        /// The actual description string shown on the right hand panel of Mod Manager's main window
        /// </summary>
        public string DisplayedModDescription
        {
            get
            {
                if (LoadFailedReason != null) return LoadFailedReason;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(ModDescription);
                sb.AppendLine(@"=============================");
                //Todo: Mod Deltas

                //Todo: Automatic configuration

                //Todo: Optional manuals

                sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_modVersion, ModVersionString ?? @"1.0"));
                sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_modDeveloper, ModDeveloper));
                if (ModModMakerID > 0)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_modMakerCode, ModModMakerID.ToString()));
                }
                if (ModClassicUpdateCode > 0 && Settings.DeveloperMode)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_updateCode, ModClassicUpdateCode.ToString()));
                }
                //else if (NexusModID > 0 && Settings.DeveloperMode)
                //{
                //    sb.AppendLine($"NexusMods ID: {NexusModID}");
                //}

                sb.AppendLine(M3L.GetString(M3L.string_modparsing_installationInformationSplitter));
                if (Settings.DeveloperMode)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_targetsModDesc, ModDescTargetVersion.ToString(CultureInfo.InvariantCulture)));
                }
                var modifiesList = InstallationJobs.Where(x => x.Header != ModJob.JobHeader.CUSTOMDLC).Select(x => x.Header == ModJob.JobHeader.ME2_RCWMOD ? @"ME2 Coalesced.ini" : x.Header.ToString()).ToList();
                if (modifiesList.Count > 0)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_modifies, string.Join(@", ", modifiesList)));
                }

                var customDLCJob = InstallationJobs.FirstOrDefault(x => x.Header == ModJob.JobHeader.CUSTOMDLC);
                if (customDLCJob != null)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_interp_modparsing_addCustomDLCs, string.Join(@", ", customDLCJob.CustomDLCFolderMapping.Values)));
                }

                SortedSet<string> autoConfigs = new SortedSet<string>();
                foreach (var InstallationJob in InstallationJobs)
                {
                    foreach (var altdlc in InstallationJob.AlternateDLCs)
                    {
                        foreach (var conditionaldlc in altdlc.ConditionalDLC)
                        {
                            autoConfigs.Add(conditionaldlc.TrimStart('-', '+'));
                        }
                    }
                    foreach (var altfile in InstallationJob.AlternateFiles)
                    {
                        foreach (var conditionaldlc in altfile.ConditionalDLC)
                        {
                            autoConfigs.Add(conditionaldlc.TrimStart('-', '+'));
                        }
                    }
                }

                if (autoConfigs.Count > 0)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_modparsing_configCanChangeIfOtherDLCFound));
                    foreach (var autoConfigDLC in autoConfigs)
                    {
                        string name = ThirdPartyServices.GetThirdPartyModInfo(autoConfigDLC, Game)?.modname ?? autoConfigDLC;
                        sb.AppendLine($@" - {name}");
                    }
                }


                if (RequiredDLC.Count > 0)
                {
                    sb.AppendLine(M3L.GetString(M3L.string_modparsing_requiresTheFollowingDLCToInstall));
                    foreach (var reqDLC in RequiredDLC)
                    {
                        string name = ThirdPartyServices.GetThirdPartyModInfo(reqDLC, Game)?.modname ?? reqDLC;
                        sb.AppendLine($@" - {name}");
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
        public string ModPath { get; private set; }
        public SevenZipExtractor Archive;
        public string ModDescPath => FilesystemInterposer.PathCombine(IsInArchive, ModPath, @"moddesc.ini");
        public bool IsInArchive { get; }
        public int MinimumSupportedBuild { get; set; }
        public bool IsVirtualized { get; private set; }
        public string OriginalArchiveHash { get; private set; }
        public string PostInstallToolLaunch { get; private set; }

        private readonly string VirtualizedIniText;
        private readonly string ArchivePath;

        public Mod(RCWMod rcw)
        {
            Log.Information(@"Converting an RCW mod to an M3 mod.");
            Game = MEGame.ME2;
            ModDescTargetVersion = 6.0;
            ModDeveloper = rcw.Author;
            ModName = rcw.ModName;
            ModDescription = M3L.GetString(M3L.string_modparsing_defaultRCWDescription);
            ModJob rcwJob = new ModJob(ModJob.JobHeader.ME2_RCWMOD);
            rcwJob.RCW = rcw;
            InstallationJobs.Add(rcwJob);
            ValidMod = true;
            MemoryAnalyzer.AddTrackedMemoryItem(@"Mod (RCW) - " + ModName, new WeakReference(this));
        }

        /// <summary>
        /// Loads a moddesc from a stream. Used when reading data from an archive. 
        /// </summary>
        /// <param name="moddescArchiveEntry">File entry in archive for this moddesc.ini</param>
        /// <param name="archive">Archive to inspect for</param>
        public Mod(ArchiveFileInfo moddescArchiveEntry, SevenZipExtractor archive)
        {
            Log.Information($@"Loading moddesc.ini from archive: {Path.GetFileName(archive.FileName)} => {moddescArchiveEntry.FileName}");
            MemoryStream ms = new MemoryStream();
            try
            {
                archive.ExtractFile(moddescArchiveEntry.FileName, ms);
            }
            catch (Exception e)
            {
                ModName = M3L.GetString(M3L.string_loadFailed);
                Log.Error(@"Error loading moddesc.ini from archive! Error: " + e.Message);
                LoadFailedReason = M3L.GetString(M3L.string_interp_errorReadingArchiveModdesc, e.Message);
                return;
            }

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
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_errorOccuredParsingArchiveModdescini, moddescArchiveEntry.FileName, e.Message);
            }
            SizeRequiredtoExtract = GetRequiredSpaceForExtraction();
            MemoryAnalyzer.AddTrackedMemoryItem(@"Mod (Archive) - " + ModName, new WeakReference(this));

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
            Log.Information(@"Loading moddesc: " + filePath);
            try
            {
                loadMod(File.ReadAllText(filePath), expectedGame);
            }
            catch (Exception e)
            {
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_errorOccuredParsingModdescini, filePath, e.Message);
            }
            MemoryAnalyzer.AddTrackedMemoryItem(@"Mod (Disk) - " + ModName, new WeakReference(this));

        }

        /// <summary>
        /// Loads a mod from a virtual moddesc.ini file, forcing the ini path. If archive is specified, the archive will be used, otherwise it will load as if on disk.
        /// </summary>
        /// <param name="iniText">Virtual Ini text</param>
        /// <param name="forcedModPath">Directory where this moddesc.ini would reside be if it existed in the archive or on disk</param>
        /// <param name="archive">Archive file to parse against. If null, this mod will be parsed as if on-disk</param>
        public Mod(string iniText, string forcedModPath, SevenZipExtractor archive)
        {
            ModPath = forcedModPath;
            if (archive != null)
            {
                Archive = archive;
                ArchivePath = archive.FileName;
                IsInArchive = true;
                IsVirtualized = true;
            }

            VirtualizedIniText = iniText;
            Log.Information(@"Loading virtualized moddesc.ini");
            try
            {
                loadMod(iniText, MEGame.Unknown);
            }
            catch (Exception e)
            {
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_errorOccuredParsingVirtualizedModdescini, e.Message);
            }

            if (archive != null)
            {
                SizeRequiredtoExtract = GetRequiredSpaceForExtraction();
            }

            MemoryAnalyzer.AddTrackedMemoryItem(@"Mod (Virtualized) - " + ModName, new WeakReference(this));
        }

        private readonly string[] GameFileExtensions = { @".u", @".upk", @".sfm", @".pcc", @".bin", @".tlk", @".cnd", @".ini", @".afc", @".tfc", @".dlc", @".sfar", @".txt", @".bik", @".bmp", @".usf" };

        /// <summary>
        /// Main moddesc.ini parser
        /// </summary>
        /// <param name="iniText"></param>
        /// <param name="expectedGame"></param>
        private void loadMod(string iniText, MEGame expectedGame)
        {
            Game = expectedGame; //we will assign this later. This is for startup errors only
            var parser = new IniDataParser();
            var iniData = parser.Parse(iniText);

            if (int.TryParse(iniData[@"ModManager"][@"minbuild"], out int minBuild))
            {
                MinimumSupportedBuild = minBuild;
                if (App.BuildNumber < minBuild)
                {
                    ModName = (ModPath == "" && IsInArchive)
                        ? Path.GetFileNameWithoutExtension(Archive.FileName)
                        : Path.GetFileName(ModPath);
                    Log.Error(
                        $@"This mod specifies it can only load on M3 builds {minBuild} or higher. The current build number is {App.BuildNumber}.");
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_cannotLoadBuildTooOld,
                        minBuild, App.BuildNumber);
                    return; //Won't set valid
                }
            }

            int.TryParse(iniData[@"ModManager"][@"importedby"], out int importedByBuild);
            ImportedByBuild = importedByBuild;

            if (double.TryParse(iniData[@"ModManager"][@"cmmver"], out double parsedModCmmVer))
            {
                ModDescTargetVersion = parsedModCmmVer;
            }
            else
            {
                //Run in legacy mode (ME3CMM 1.0)
                ModDescTargetVersion = 1.0;
            }

            ModName = iniData[@"ModInfo"][@"modname"];
            if (string.IsNullOrEmpty(ModName))
            {
                ModName = (ModPath == "" && IsInArchive)
                    ? Path.GetFileNameWithoutExtension(Archive.FileName)
                    : Path.GetFileName(ModPath);
                Log.Error($@"moddesc.ini in {ModPath} does not set the modname descriptor.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_nomodname, ModPath);
                return; //Won't set valid
            }

            // This is loaded early so the website value can be parsed if something else fails. 
            // This is used in failedmodspanel
            ModWebsite = iniData[@"ModInfo"][@"modsite"] ?? DefaultWebsite;
            if (string.IsNullOrWhiteSpace(ModWebsite)) ModWebsite = DefaultWebsite;

            //test url scheme
            Uri uriResult;
            if (!(Uri.TryCreate(ModWebsite, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)))
            {
                Log.Error($@"Invalid url for mod {ModName}: URL must be of type http:// or https:// and be a valid formed url. Invalid value: {ModWebsite}");
                LoadFailedReason = $"Invalid url for mod {ModName}: URL must be of type http:// or https:// and be a valid formed url. Invalid value: {ModWebsite}";
                ModWebsite = DefaultWebsite; //Reset so we don't try to open invalid url
                return; //Won't set valid
            }

            ModDescription = Utilities.ConvertBrToNewline(iniData[@"ModInfo"][@"moddesc"]);
            if (string.IsNullOrWhiteSpace(ModDescription))
            {
                Log.Error($@"moddesc.ini in {ModPath} does not set the moddesc descriptor.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_nomoddesc, ModPath);
                return; //Won't set valid
            }

            ModDeveloper = iniData[@"ModInfo"][@"moddev"];
            if (string.IsNullOrWhiteSpace(ModDeveloper))
            {
                Log.Error($@"moddesc.ini in {ModPath} does not set the moddev descriptor.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_nomoddev, ModPath);
                return; //Won't set valid
            }

            ModVersionString = iniData[@"ModInfo"][@"modver"];
            //Check for integer value only
            if (int.TryParse(ModVersionString, out var intVersion))
            {
                ModVersionString += @".0";
            }

            Version.TryParse(ModVersionString, out var parsedValue);
            ParsedModVersion = parsedValue;

            //updates
            int.TryParse(iniData[@"ModInfo"][@"modid"], out int modmakerId);
            ModModMakerID = modmakerId;

            int.TryParse(iniData[@"UPDATES"][@"updatecode"], out int modupdatecode);
            ModClassicUpdateCode = modupdatecode;

            if (ModClassicUpdateCode == 0)
            {
                //try in old location
                int.TryParse(iniData[@"ModInfo"][@"updatecode"], out int modupdatecode2);
                ModClassicUpdateCode = modupdatecode2;
            }

            int.TryParse(iniData[@"ModInfo"][@"nexuscode"], out int nexuscode);
            NexusModID = nexuscode;

            #region NexusMods ID from URL

            if (NexusModID == 0 && ModModMakerID == 0 /*&& ModClassicUpdateCode == 0 */ &&
                !string.IsNullOrWhiteSpace(ModWebsite) && ModWebsite.Contains(@"nexusmods.com/masseffect"))
            {
                try
                {
                    //try to extract nexus mods ID
                    var nexusIndex = ModWebsite.IndexOf(@"nexusmods.com/", StringComparison.InvariantCultureIgnoreCase);
                    if (nexusIndex > 0)
                    {
                        string nexusId = ModWebsite.Substring(nexusIndex + @"nexusmods.com/".Length); // http:/

                        nexusId = nexusId.Substring(@"masseffect".Length);
                        if (Game == MEGame.ME2 || Game == MEGame.ME3)
                        {
                            nexusId = nexusId.Substring(1); //number
                        }

                        nexusId = nexusId.Substring(6)
                            .TrimEnd('/'); // /mods/ and any / after number in the event url has that in it.

                        int questionMark = nexusId.IndexOf(@"?", StringComparison.InvariantCultureIgnoreCase);
                        if (questionMark > 0)
                        {
                            nexusId = nexusId.Substring(0, questionMark);
                        }

                        if (int.TryParse(nexusId, out var nid))
                        {
                            NexusModID = nid;
                        }
                    }
                }
                catch (Exception)
                {
                    //don't bother.
                }
            }

            #endregion

            CLog.Information($@"Read modmaker update code (or used default): {ModClassicUpdateCode}",
                Settings.LogModStartup);
            if (ModClassicUpdateCode > 0 && ModModMakerID > 0)
            {
                Log.Error(
                    $@"{ModName} has both an updater service update code and a modmaker code assigned. This is not allowed.");
                LoadFailedReason =
                    M3L.GetString(M3L.string_validation_modparsing_loadfailed_cantSetBothUpdaterAndModMaker);
                return; //Won't set valid
            }

            var unofficialStr = iniData[@"ModInfo"][@"unofficial"];
            if (!string.IsNullOrWhiteSpace(unofficialStr))
            {
                IsUnofficial = true;
                CLog.Information($@"Found unofficial descriptor. Marking mod as unofficial. This will block deployment of the mod until it is removed.",
                    Settings.LogModStartup);

            }

            string game = iniData[@"ModInfo"][@"game"];
            if (parsedModCmmVer >= 6 && game == null && importedByBuild > 0)
            {
                //Not allowed. You MUST specify game on cmmver 6 or higher
                Log.Error($@"{ModName} does not set the ModInfo 'game' descriptor, which is required for all mods targeting ModDesc 6 or higher.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_missingModInfoGameDescriptor, ModName);
                return;
            }
            switch (game)
            {
                //case null: //will have to find a way to deal with the null case, in the event it's an ME3 mod manager mod from < 6.0.
                case @"ME3":
                    Game = MEGame.ME3;
                    break;
                case @"ME2":
                    Game = MEGame.ME2;
                    break;
                case @"ME1":
                    Game = MEGame.ME1;
                    break;
                default:
                    //Check if this is in ME3 game directory. If it's null, it might be a legacy mod
                    if (game == null)
                    {
                        CLog.Warning(@"Game indicator is null. This may be mod from pre-Mod Manager 6, or developer did not specify the game. Defaulting to ME3", Settings.LogModStartup);
                        Game = MEGame.ME3;
                    }
                    else
                    {
                        Log.Error($@"{ModName} has unknown game ID set for ModInfo descriptor 'game'. Valid values are ME1, ME2 or ME3. Value provided: {game}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_unknownGameId, game);
                        return;
                    }

                    break;
            }

            if (ModDescTargetVersion < 6 && Game != MEGame.ME3)
            {
                Log.Error($@"{ModName} is designed for {game}. ModDesc versions (cmmver descriptor under ModManager section) under 6.0 do not support ME1 or ME2.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_cmm6RequiredForME12, game, ModDescTargetVersion.ToString(CultureInfo.InvariantCulture));
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

                CLog.Information($@"---MOD--------END OF {ModName} STARTUP-----------", Settings.LogModStartup);
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
            CLog.Information(@"Parsing mod using moddesc target: " + ModDescTargetVersion, Settings.LogModStartup);

            #region Header Loops
            #region BASEGAME and OFFICIAL HEADERS

            var supportedOfficialHeaders = ModJob.GetSupportedNonCustomDLCHeaders(Game);


            //We must check against official headers
            //ME2 doesn't support anything but basegame.
            foreach (var header in supportedOfficialHeaders)
            {
                //if (Game != MEGame.ME3 && header != ModJob.JobHeader.BASEGAME) continue; //Skip any non-basegame offical headers for ME1/ME2
                var headerAsString = header.ToString();
                var jobSubdirectory = iniData[headerAsString][@"moddir"];
                if (jobSubdirectory != null)
                {
                    jobSubdirectory = jobSubdirectory.Replace('/', '\\').TrimStart('\\');
                    CLog.Information(@"Found INI header with moddir specified: " + headerAsString, Settings.LogModStartup);
                    CLog.Information(@"Subdirectory (moddir): " + jobSubdirectory, Settings.LogModStartup);
                    //string fullSubPath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, jobSubdirectory);

                    bool directoryMatchesGameStructure = false;
                    if (ModDescTargetVersion >= 6.0) bool.TryParse(iniData[headerAsString][@"gamedirectorystructure"], out directoryMatchesGameStructure);

                    //Replace files (ModDesc 2.0)
                    string replaceFilesSourceList = iniData[headerAsString][@"newfiles"]; //Present in MM2. So this will always be read
                    string replaceFilesTargetList = iniData[headerAsString][@"replacefiles"]; //Present in MM2. So this will always be read

                    //Add files (ModDesc 4.1)
                    string addFilesSourceList = ModDescTargetVersion >= 4.1 ? iniData[headerAsString][@"addfiles"] : null;
                    string addFilesTargetList = ModDescTargetVersion >= 4.1 ? iniData[headerAsString][@"addfilestargets"] : null;

                    //Add files Read-Only (ModDesc 4.3)
                    string addFilesTargetReadOnlyList = ModDescTargetVersion >= 4.3 ? iniData[headerAsString][@"addfilesreadonlytargets"] : null;


                    //Remove files (ModDesc 4.1) - REMOVE IN MODDESC 6


                    //Check that the lists here are at least populated in one category. If none are populated then this job will do effectively nothing.
                    bool taskDoesSomething = (replaceFilesSourceList != null && replaceFilesTargetList != null) || (addFilesSourceList != null && addFilesTargetList != null);

                    if (!taskDoesSomething)
                    {
                        Log.Error($@"Mod has job header ({headerAsString}) with no tasks in add, replace, or remove lists. This header does effectively nothing. Marking mod as invalid");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_headerDoesNothing, headerAsString);
                        return;
                    }

                    List<string> replaceFilesSourceSplit = null;
                    List<string> replaceFilesTargetSplit = null;
                    if (replaceFilesSourceList != null && replaceFilesTargetList != null)
                    {
                        //Parse the newfiles and replacefiles list and ensure they have the same number of elements in them.
                        replaceFilesSourceSplit = replaceFilesSourceList.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        replaceFilesTargetSplit = replaceFilesTargetList.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        if (replaceFilesSourceSplit.Count != replaceFilesTargetSplit.Count)
                        {
                            //Mismatched source and target lists
                            Log.Error($@"Mod has job header ({headerAsString}) that has mismatched newfiles and replacefiles descriptor lists. newfiles has {replaceFilesSourceSplit.Count} items, replacefiles has {replaceFilesTargetSplit.Count} items. The number of items in each list must match.");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_headerHasMismatchedNewFilesReplaceFiles, headerAsString, replaceFilesSourceSplit.Count.ToString(), replaceFilesTargetSplit.Count.ToString());
                            return;
                        }

                        CLog.Information($@"Parsing replacefiles/newfiles on {headerAsString}. Found {replaceFilesTargetSplit.Count} items in lists", Settings.LogModStartup);
                    }

                    //Don't support add/remove files on anything except ME3, unless basegame.
                    List<string> addFilesSourceSplit = null;
                    List<string> addFilesTargetSplit = null;
                    List<string> addFilesReadOnlySplit = null;
                    if (Game == Mod.MEGame.ME3 || header == ModJob.JobHeader.BASEGAME)
                    {

                        if (addFilesSourceList != null && addFilesTargetList != null)
                        {
                            //Parse the addfiles and addfilestargets list and ensure they have the same number of elements in them.
                            addFilesSourceSplit = addFilesSourceList.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            addFilesTargetSplit = addFilesTargetList.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            if (addFilesSourceSplit.Count != addFilesTargetSplit.Count)
                            {
                                //Mismatched source and target lists
                                Log.Error($@"Mod has job header ({headerAsString}) that has mismatched addfiles and addfilestargets descriptor lists. addfiles has {addFilesSourceSplit.Count} items, addfilestargets has {addFilesTargetSplit.Count} items. The number of items in each list must match.");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_headerHasMismatchedAddFilesAddFilesTargets, headerAsString, addFilesSourceSplit.Count.ToString(), addFilesTargetSplit.Count.ToString());
                                return;
                            }

                            CLog.Information($@"Parsing addfiles/addfilestargets on {headerAsString}. Found {addFilesTargetSplit.Count} items in lists", Settings.LogModStartup);
                        }

                        //Add files read only targets
                        if (addFilesTargetReadOnlyList != null)
                        {
                            addFilesReadOnlySplit = addFilesTargetList.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                            //Ensure add targets list contains this list
                            if (addFilesTargetSplit != null)
                            {
                                if (!addFilesTargetSplit.ContainsAll(addFilesReadOnlySplit, StringComparer.InvariantCultureIgnoreCase))
                                {
                                    //readonly list contains elements not contained in the targets list
                                    Log.Error($@"Mod has job header ({headerAsString}) that has addfilesreadonlytargets descriptor set, however it contains items that are not part of the addfilestargets list. This is not allowed.");
                                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_headerSpecifiesNotAddedReadOnlyTarget, headerAsString);
                                    return;
                                }
                            }
                            else
                            {
                                //readonly target specified but nothing in the addfilestargets list/unspecified
                                Log.Error($@"Mod has job header ({headerAsString}) that has addfilesreadonlytargets descriptor set, however there is no addfilestargets specified.");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_headerCantSetReadOnlyWithoutAddFilesList, headerAsString);
                                return;
                            }
                            //TODO: IMPLEMENT INSTALLER LOGIC FOR THIS.
                            CLog.Information($@"Parsing addfilesreadonlytargets on {headerAsString}. Found {addFilesReadOnlySplit.Count} items in list", Settings.LogModStartup);
                        }

                        //TODO: Bini support

                        //Ensure TESTPATCH is supported by making sure we are at least on ModDesc 3 if using TESTPATCH header.
                        //ME3 only
                        if (ModDescTargetVersion < 3 && header == ModJob.JobHeader.TESTPATCH)
                        {
                            Log.Error($@"Mod has job header ({headerAsString}) specified, but this header is only supported when targeting ModDesc 3 or higher.");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_headerUnsupportedOnLTModdesc3, headerAsString);
                            return;
                        }

                    }

                    //This was introduced in Mod Manager 4.1 but is considered applicable to all moddesc versions as it doesn't impact installation and is only for user convenience
                    //In Java Mod Manager, this required 4.1 moddesc
                    string jobRequirement = iniData[headerAsString][@"jobdescription"];
                    CLog.Information($@"Read job requirement text: {jobRequirement}", Settings.LogModStartup && jobRequirement != null);

                    ModJob headerJob = new ModJob(header, this);
                    headerJob.JobDirectory = jobSubdirectory.Replace('/', '\\');
                    headerJob.RequirementText = jobRequirement;



                    //Build replacements 
                    int jobDirLength = jobSubdirectory == @"." ? 0 : jobSubdirectory.Length;
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
                                    var files = FilesystemInterposer.DirectoryGetFiles(sourceDirectory, @"*.*", SearchOption.AllDirectories, Archive).Select(x => x.Substring((ModPath.Length > 0 ? (ModPath.Length + 1) : 0) + jobDirLength).TrimStart('\\')).ToList();
                                    foreach (var file in files)
                                    {
                                        if (GameFileExtensions.Contains(Path.GetExtension(file), StringComparer.InvariantCultureIgnoreCase))
                                        {
                                            var destFile = destGameDirectory + file.Substring(replaceFilesSourceSplit[i].Length);
                                            CLog.Information($@"Adding file to job installation queue: {file} => {destFile}", Settings.LogModStartup);
                                            string failurereason = headerJob.AddPreparsedFileToInstall(destFile, file, this);
                                            if (failurereason != null)
                                            {
                                                Log.Error($@"Error occured while automapping the replace files lists for {headerAsString}: {failurereason}. This is likely a bug in M3, please report it to Mgamerz");
                                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_errorAutomappingPleaseReport, headerAsString, failurereason);
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            Log.Warning(@"File type/extension not supported by gamedirectorystructure scan: " + file);
                                        }
                                    }
                                }
                                else
                                {
                                    Log.Error($@"Error occured while parsing the replace files lists for {headerAsString}: source directory {sourceDirectory} was not found and the gamedirectorystructure flag was used on this job.");
                                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_sourceDirectoryForJobNotFound, headerAsString, sourceDirectory);
                                    return;
                                }
                                if (!headerJob.FilesToInstall.Any())
                                {
                                    Log.Error($@"Error using gamedirectorystructure option: No files were found to install in the specified path for job: {headerAsString}, Path: {sourceDirectory}");
                                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_gamedirectoryStructureFoundNoFiles, headerAsString, sourceDirectory);
                                    return;
                                }
                            }
                            else
                            {
                                string destFile = replaceFilesTargetSplit[i];
                                CLog.Information($@"Adding file to job installation queue: {replaceFilesSourceSplit[i]} => {destFile}", Settings.LogModStartup);
                                string failurereason = headerJob.AddFileToInstall(destFile, replaceFilesSourceSplit[i], this);
                                if (failurereason != null)
                                {
                                    Log.Error($@"Error occured while parsing the replace files lists for {headerAsString}: {failurereason}");
                                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_genericFailureParsingLists, headerAsString, failurereason);
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
                            CLog.Information($@"Adding file to installation queue (addition): {addFilesSourceSplit[i]} => {destFile}", Settings.LogModStartup);
                            string failurereason = headerJob.AddAdditionalFileToInstall(destFile, addFilesSourceSplit[i], this); //add files are layered on top
                            if (failurereason != null)
                            {
                                Log.Error($@"Error occured while parsing the add files lists for {headerAsString}: {failurereason}");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_genericFailedToParseAddFilesLists, headerAsString, failurereason);
                                return;
                            }
                        }
                    }

                    //Build additions (vars will be null if these aren't supported by target version)
                    if (addFilesSourceSplit != null && !directoryMatchesGameStructure)
                    {
                        for (int i = 0; i < addFilesSourceSplit.Count; i++)
                        {
                            string destFile = addFilesTargetSplit[i];
                            CLog.Information($@"Adding file to installation queue (addition): {addFilesSourceSplit[i]} => {destFile}", Settings.LogModStartup);
                            string failurereason = headerJob.AddAdditionalFileToInstall(destFile, addFilesSourceSplit[i], this); //add files are layered on top
                            if (failurereason != null)
                            {
                                Log.Error($@"Error occured while parsing the add files lists for {headerAsString}: {failurereason}");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_genericFailedToParseAddFilesLists, headerAsString, failurereason);
                                return;
                            }
                        }
                    }

                    if (addFilesReadOnlySplit != null && !directoryMatchesGameStructure)
                    {
                        for (int i = 0; i < addFilesReadOnlySplit.Count; i++)
                        {
                            CLog.Information($@"Adding read-only item to post-installation step): {addFilesSourceSplit[i]}", Settings.LogModStartup);

                            string failurereason = headerJob.AddReadOnlyIndicatorForFile(addFilesSourceSplit[i], this);
                            if (failurereason != null)
                            {
                                Log.Error($@"Error occured while parsing the addfilesreadonlytargtes list for {headerAsString}: {failurereason}");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_genericFailedToParseAddFilesReadOnlyTargets, headerAsString, failurereason);
                                return;
                            }
                        }
                    }

                    //MultiLists: Mod Manager 6.0
                    //Must be parsed before AltFile as AltFiles can access these lists.
                    if (ModDescTargetVersion >= 6.0)
                    {
                        int i = 1;
                        while (true)
                        {
                            var multilist = iniData[headerAsString][@"multilist" + i];
                            if (multilist == null) break; //no more to parse
                            headerJob.MultiLists[i] = multilist.Split(';');
                            i++;
                        }
                    }

                    //Altfiles: Mod Manager 4.2
                    string altfilesStr = (ModDescTargetVersion >= 4.2 && headerJob.Header != ModJob.JobHeader.BALANCE_CHANGES) ? iniData[headerAsString][@"altfiles"] : null;
                    if (!string.IsNullOrEmpty(altfilesStr))
                    {
                        var splits = StringStructParser.GetParenthesisSplitValues(altfilesStr);
                        if (splits.Count == 0)
                        {
                            Log.Error($@"Alternate files list was unable to be parsed for header {headerAsString}, no items were returned from parenthesis parser.");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_failedToParseAltfiles, headerAsString);
                            return;
                        }
                        foreach (var split in splits)
                        {
                            AlternateFile af = new AlternateFile(split, headerJob, this);
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

                    if (!headerJob.ValidateAlternates(out string failureReason))
                    {
                        LoadFailedReason = failureReason;
                        return;
                    }

                    CLog.Information($@"Successfully made mod job for {headerAsString}", Settings.LogModStartup);
                    InstallationJobs.Add(headerJob);
                }
            }

            #endregion

            #region CUSTOMDLC

            if (ModDescTargetVersion >= 3.1)
            {
                var customDLCSourceDirsStr = iniData[@"CUSTOMDLC"][@"sourcedirs"];
                var customDLCDestDirsStr = iniData[@"CUSTOMDLC"][@"destdirs"];
                //ALT DLC: Mod Manager 4.4
                //This behavior changed in Mod Manager 6 to allow no sourcedirs/destdirs if a custom dlc will only be added on a condition
                string altdlcstr = (ModDescTargetVersion >= 4.4) ? iniData[@"CUSTOMDLC"][@"altdlc"] : null;


                if ((customDLCSourceDirsStr != null && customDLCDestDirsStr != null) || !string.IsNullOrEmpty(altdlcstr))
                {
                    CLog.Information(@"Found CUSTOMDLC header", Settings.LogModStartup);
                    ModJob customDLCjob = new ModJob(ModJob.JobHeader.CUSTOMDLC, this);

                    if (customDLCSourceDirsStr != null && customDLCDestDirsStr != null)
                    {
                        var customDLCSourceSplit = customDLCSourceDirsStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        var customDLCDestSplit = customDLCDestDirsStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                        //Verify lists are the same length
                        if (customDLCSourceSplit.Count != customDLCDestSplit.Count)
                        {
                            //Mismatched source and target lists
                            Log.Error($@"Mod has job header (CUSTOMDLC) that has mismatched sourcedirs and destdirs descriptor lists. sourcedirs has {customDLCSourceSplit.Count} items, destdirs has {customDLCDestSplit.Count} items. The number of items in each list must match.");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_customDLCMismatchedSourceDirsDestDirs, customDLCSourceSplit.Count.ToString(), customDLCDestSplit.Count.ToString());
                            return;
                        }

                        //Security check for ..
                        if (customDLCSourceSplit.Any(x => x.Contains(@"..")) || customDLCDestSplit.Any(x => x.Contains(@"..")))
                        {
                            //Security violation: Cannot use .. in filepath
                            Log.Error(@"CUSTOMDLC header sourcedirs or destdirs includes item that contains a .., which is not permitted.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_customDLCItemHasIllegalCharacters);
                            return;
                        }

                        //Verify folders exists
                        foreach (var f in customDLCSourceSplit)
                        {
                            if (!FilesystemInterposer.DirectoryExists(FilesystemInterposer.PathCombine(IsInArchive, ModPath, f), Archive))
                            {
                                Log.Error($@"Mod has job header (CUSTOMDLC) sourcedirs descriptor specifies installation of a Custom DLC folder that does not exist in the mod folder: {f}");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_customDLCSourceDirMissing, f);
                                return;
                            }
                        }

                        //Security check: Protected folders
                        foreach (var f in customDLCDestSplit)
                        {
                            if (Utilities.IsProtectedDLCFolder(f, Game))
                            {
                                Log.Error($@"Mod has job header (CUSTOMDLC) destdirs descriptor that specifies installation of a Custom DLC folder to a protected target: {f}");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_destDirCannotBeMetadataOrOfficialDLC, f);
                                return;
                            }

                            if (!f.StartsWith(@"DLC_"))
                            {
                                Log.Error($@"Mod has job header (CUSTOMDLC) destdirs descriptor that specifies installation of a Custom DLC folder that would install a disabled DLC: {f}. DLC folders must start with DLC_.");
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_destDirFoldernamesMustStartWithDLC, f);
                                return;
                            }
                        }
                        for (int i = 0; i < customDLCSourceSplit.Count; i++)
                        {
                            customDLCjob.CustomDLCFolderMapping[customDLCSourceSplit[i]] = customDLCDestSplit[i];
                        }
                    }

                    //Altfiles: Mod Manager 4.2
                    string altfilesStr = (ModDescTargetVersion >= 4.2) ? iniData[@"CUSTOMDLC"][@"altfiles"] : null;
                    if (!string.IsNullOrEmpty(altfilesStr))
                    {
                        var splits = StringStructParser.GetParenthesisSplitValues(altfilesStr);
                        if (splits.Count == 0)
                        {
                            Log.Error(@"Alternate files list was unable to be parsed, no items were returned from parenthesis parser.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_altFilesListFailedToParse);
                            return;
                        }
                        foreach (var split in splits)
                        {
                            AlternateFile af = new AlternateFile(split, customDLCjob, this);
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

                    //MultiLists: Mod Manager 6.0
                    //Must be parsed before AltDLC as AltDLC can access these lists.
                    if (ModDescTargetVersion >= 6.0)
                    {
                        int i = 1;
                        while (true)
                        {
                            var multilist = iniData[@"CUSTOMDLC"][@"multilist" + i];
                            if (multilist == null) break; //no more to parse
                            customDLCjob.MultiLists[i] = multilist.Split(';');
                            i++;
                        }
                    }


                    //AltDLC: Mod Manager 4.4
                    if (!string.IsNullOrEmpty(altdlcstr))
                    {
                        var splits = StringStructParser.GetParenthesisSplitValues(altdlcstr);
                        foreach (var split in splits)
                        {
                            AlternateDLC af = new AlternateDLC(split, this, customDLCjob);
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
                    HumanReadableCustomDLCNames = iniData[@"CUSTOMDLC"].Where(x => x.KeyName.StartsWith(@"DLC_")).ToDictionary(mc => mc.KeyName, mc => mc.Value);

                    if (!customDLCjob.ValidateAlternates(out string failureReason))
                    {
                        LoadFailedReason = failureReason;
                        return;
                    }

                    CLog.Information($@"Successfully made mod job for CUSTOMDLC", Settings.LogModStartup);
                    InstallationJobs.Add(customDLCjob);
                }
                else if ((customDLCSourceDirsStr != null) != (customDLCDestDirsStr != null))
                {
                    Log.Error($@"{ModName} specifies only one of the two required lists for the CUSTOMDLC header. Both sourcedirs and destdirs descriptors must be set for CUSTOMDLC.");
                    LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_mustHaveBothSourceAndDestDirs);
                    return;
                }
            }

            #endregion

            #region BALANCE_CHANGES (ME3 ONLY)

            var balanceChangesDirectory = (Game == MEGame.ME3 && ModDescTargetVersion >= 4.3) ? iniData[ModJob.JobHeader.BALANCE_CHANGES.ToString()][@"moddir"] : null;
            if (balanceChangesDirectory != null)
            {
                CLog.Information(@"Found BALANCE_CHANGES header", Settings.LogModStartup);
                CLog.Information(@"Subdirectory (moddir): " + balanceChangesDirectory, Settings.LogModStartup);
                //string fullSubPath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, jobSubdirectory);

                //In MM5.1 or lower you would have to specify the target. In MM6 you can only specify a single source and it must be a .bin file.
                string replaceFilesSourceList = iniData[ModJob.JobHeader.BALANCE_CHANGES.ToString()][@"newfiles"];
                if (replaceFilesSourceList != null)
                {
                    //Parse the newfiles and replacefiles list and ensure they have the same number of elements in them.
                    var replaceFilesSourceSplit = replaceFilesSourceList.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    if (replaceFilesSourceSplit.Count == 1)
                    {
                        //Only 1 file is allowed here.
                        string balanceFile = replaceFilesSourceSplit[0];
                        if (!balanceFile.EndsWith(@".bin"))
                        {
                            //Invalid file
                            Log.Error(@"Balance changes file must be a .bin file.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_balanceChangesFileNotBinFile);
                            return;
                        }
                        ModJob balanceJob = new ModJob(ModJob.JobHeader.BALANCE_CHANGES);
                        balanceJob.JobDirectory = balanceChangesDirectory;
                        CLog.Information($@"Adding file to job installation queue: {balanceFile} => Binaries\win32\asi\ServerCoalesced.bin", Settings.LogModStartup);

                        string failurereason = balanceJob.AddFileToInstall(@"Binaries\win32\asi\ServerCoalesced.bin", balanceFile, this);
                        if (failurereason != null)
                        {
                            Log.Error($@"Error occured while creating BALANCE_CHANGE job: {failurereason}");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_genericErrorCreatingBalanceChangeJob, failurereason);
                            return;
                        }
                        CLog.Information($@"Successfully made mod job for {balanceJob.Header}", Settings.LogModStartup);
                        InstallationJobs.Add(balanceJob);
                    }
                    else
                    {
                        Log.Error($@"Balance changes newfile descriptor only allows 1 entry in the list, but {replaceFilesSourceSplit.Count} were parsed.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_canOnlyHaveOneBalanceChangesFile, replaceFilesSourceSplit.Count.ToString());
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
                    var jobSubdirectory = iniData[ModJob.JobHeader.ME1_CONFIG.ToString()][@"moddir"];
                    if (!string.IsNullOrWhiteSpace(jobSubdirectory))
                    {
                        var configfilesStr = iniData[@"ME1_CONFIG"][@"configfiles"];
                        if (string.IsNullOrWhiteSpace(configfilesStr))
                        {
                            Log.Error(@"ME1_CONFIG job was specified but configfiles descriptor is empty or missing. Remove this header if you are not using this task.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_cannotHaveEmptyME1ConfigJob);
                            return;
                        }
                        var configFilesSplit = configfilesStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        ModJob me1ConfigJob = new ModJob(ModJob.JobHeader.ME1_CONFIG, this);
                        me1ConfigJob.JobDirectory = jobSubdirectory;
                        foreach (var configFilename in configFilesSplit)
                        {
                            if (allowedConfigFilesME1.Contains(configFilename, StringComparer.InvariantCultureIgnoreCase))
                            {
                                var failurereason = me1ConfigJob.AddFileToInstall(configFilename, configFilename, this);
                                if (failurereason != null)
                                {
                                    Log.Error($@"Error occured while creating ME1_CONFIG job: {failurereason}");
                                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_genericErrorReadingME1ConfigJob, failurereason);
                                    return;
                                }
                            }
                            else
                            {
                                Log.Error(@"ME1_CONFIG job's configfiles descriptor contains an unsupported config file: " + configFilename);
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_unsupportedME1ConfigFileSpecified, configFilename);
                                return;
                            }
                        }
                        CLog.Information($@"Successfully made mod job for {ModJob.JobHeader.ME1_CONFIG}", Settings.LogModStartup);
                        InstallationJobs.Add(me1ConfigJob);
                    }
                }

                if (Game == MEGame.ME2)
                {
                    var rcwfile = iniData[ModJob.JobHeader.ME2_RCWMOD.ToString()][@"modfile"];
                    if (!string.IsNullOrWhiteSpace(rcwfile))
                    {
                        var path = FilesystemInterposer.PathCombine(IsInArchive, ModPath, rcwfile);
                        if (!FilesystemInterposer.FileExists(path, Archive))
                        {
                            Log.Error(@"ME2_RCWMOD job was specified, but the specified file doesn't exist: " + rcwfile);
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_specifiedRCWFileDoesntExist, rcwfile);
                            return;
                        }

                        if (IsInArchive)
                        {
                            Log.Error(@"Cannot load compressed RCW through main mod loader.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_cannotLoadCompressedRCWModThroughMainLoaderLikelyBug);
                            return;
                        }
                        var rcwMods = RCWMod.LoadRCWMods(path);
                        if (rcwMods.Count != 1)
                        {
                            Log.Error(@"M3-mod based RCW mods may only contain 1 RCW mod each. Importing should split these into multiple single mods.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_rcwModsMayOnlyContainOneRCWMod);
                            return;
                        }

                        ModJob rcwJob = new ModJob(ModJob.JobHeader.ME2_RCWMOD);
                        rcwJob.RCW = rcwMods[0];
                        InstallationJobs.Add(rcwJob);
                        CLog.Information(@"Successfully made RCW mod job for " + rcwJob.RCW.ModName, Settings.LogModStartup);
                    }
                }
            }


            #endregion

            #endregion
            #region Additional Mod Items

            //Required DLC (Mod Manager 5.0)
            var requiredDLCText = ModDescTargetVersion >= 5.0 ? iniData[@"ModInfo"][@"requireddlc"] : null;
            if (!string.IsNullOrWhiteSpace(requiredDLCText))
            {
                var requiredDlcsSplit = requiredDLCText.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (var reqDLC in requiredDlcsSplit)
                {
                    switch (Game)
                    {
                        case MEGame.ME1:
                            if (Enum.TryParse(reqDLC, out ModJob.JobHeader header1) && ModJob.GetHeadersToDLCNamesMap(MEGame.ME1).TryGetValue(header1, out var foldername1))
                            {
                                RequiredDLC.Add(foldername1);
                                CLog.Information(@"Adding DLC requirement to mod: " + foldername1, Settings.LogModStartup);
                                continue;
                            }
                            break;
                        case MEGame.ME2:
                            if (Enum.TryParse(reqDLC, out ModJob.JobHeader header2) && ModJob.GetHeadersToDLCNamesMap(MEGame.ME2).TryGetValue(header2, out var foldername2))
                            {
                                RequiredDLC.Add(foldername2);
                                CLog.Information(@"Adding DLC requirement to mod: " + foldername2, Settings.LogModStartup);
                                continue;
                            }
                            break;
                        case MEGame.ME3:
                            if (Enum.TryParse(reqDLC, out ModJob.JobHeader header3) && ModJob.GetHeadersToDLCNamesMap(MEGame.ME3).TryGetValue(header3, out var foldername3))
                            {
                                RequiredDLC.Add(foldername3);
                                CLog.Information(@"Adding DLC requirement to mod: " + foldername3, Settings.LogModStartup);
                                continue;
                            }
                            break;
                    }

                    if (!reqDLC.StartsWith(@"DLC_"))
                    {
                        Log.Error(@"Required DLC does not match officially supported header or start with DLC_.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_invalidRequiredDLCSpecified, reqDLC);
                        return;
                    }
                    CLog.Information(@"Adding DLC requirement to mod: " + reqDLC, Settings.LogModStartup);
                    RequiredDLC.Add(reqDLC);
                }
            }

            //Outdated DLC (Mod Manager 4.4)
            var outdatedDLCText = ModDescTargetVersion >= 4.4 ? iniData[@"CUSTOMDLC"][@"outdatedcustomdlc"] : null;
            if (!string.IsNullOrEmpty(outdatedDLCText))
            {
                var outdatedCustomDLCDLCSplits = outdatedDLCText.Split(';').Select(x => x.Trim()).ToList();
                foreach (var outdated in outdatedCustomDLCDLCSplits)
                {
                    if (MEDirectories.OfficialDLC(Game).Contains(outdated, StringComparer.InvariantCultureIgnoreCase))
                    {
                        Log.Error($@"Outdated Custom DLC cannot contain an official DLC: " + outdated);
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_customDlcCAnnotListOfficialDLCAsOutdated, outdated);
                        return;
                    }
                }
                OutdatedCustomDLC.ReplaceAll(outdatedCustomDLCDLCSplits);

            }

            //Incompatible DLC (Mod Manager 6)
            //Todo: Update documentation
            var incompatibleDLCText = ModDescTargetVersion >= 6.0 ? iniData[@"CUSTOMDLC"][@"incompatiblecustomdlc"] : null;
            if (!string.IsNullOrEmpty(incompatibleDLCText))
            {
                var incompatibleDLCSplits = incompatibleDLCText.Split(';').Select(x => x.Trim()).ToList();
                foreach (var incompat in incompatibleDLCSplits)
                {
                    if (MEDirectories.OfficialDLC(Game).Contains(incompat, StringComparer.InvariantCultureIgnoreCase))
                    {
                        Log.Error($@"Incompatible Custom DLC cannot contain an official DLC: " + incompat);
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_customDlcCannotListOfficialDLCAsIncompatible, incompat);
                        return;
                    }
                }
                IncompatibleDLC.ReplaceAll(incompatibleDLCSplits);
            }


            //Additional Deployment Folders (Mod Manager 5.1)
            var additonaldeploymentfoldersStr = ModDescTargetVersion >= 5.1 ? iniData[@"UPDATES"][@"additionaldeploymentfolders"] : null;
            if (!string.IsNullOrEmpty(additonaldeploymentfoldersStr))
            {
                var addlFolderSplit = additonaldeploymentfoldersStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (var addlFolder in addlFolderSplit)
                {
                    //Todo: Check to make sure this isn't contained by one of the jobs or alt files
                    if (addlFolder.Contains(@"..") || addlFolder.Contains(@"/") || addlFolder.Contains(@"\"))
                    {
                        //Security violation: Cannot use .. / or \ in filepath
                        Log.Error($@"UPDATES header additionaldeploymentfolders includes directory ({addlFolder}) that contains a .., \\ or /, which are not permitted.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_illegalAdditionalDeploymentFoldersValue, addlFolder);
                        return;
                    }

                    //Check folder exists
                    if (!FilesystemInterposer.DirectoryExists(FilesystemInterposer.PathCombine(IsInArchive, ModPath, addlFolder), Archive))
                    {
                        Log.Error($@"UPDATES header additionaldeploymentfolders includes directory that does not exist in the mod directory: {addlFolder}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_updatesSpecifiesMissingAdditionalDeploymentFolder, addlFolder);
                        return;
                    }

                    AdditionalDeploymentFolders = addlFolderSplit;
                }
            }

            //Additional Root Deployment Files (Mod Manager 6.0)
            //Todo: Update documentation
            var additonaldeploymentfilesStr = ModDescTargetVersion >= 6.0 ? iniData[@"UPDATES"][@"additionaldeploymentfiles"] : null;
            if (!string.IsNullOrEmpty(additonaldeploymentfilesStr))
            {
                var addlFileSplit = additonaldeploymentfilesStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (var addlFile in addlFileSplit)
                {
                    if (addlFile.Contains(@"..") || addlFile.Contains(@"/") || addlFile.Contains(@"\"))
                    {
                        //Security violation: Cannot use .. / or \ in filepath
                        Log.Error($@"UPDATES header additionaldeploymentfiles includes file ({addlFile}) that contains a .., \\ or /, which are not permitted.");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_illegalAdditionalDeploymentFilesValue, addlFile);
                        return;
                    }

                    //Check file exists
                    if (!FilesystemInterposer.FileExists(FilesystemInterposer.PathCombine(IsInArchive, ModPath, addlFile), Archive))
                    {
                        Log.Error($@"UPDATES header additionaldeploymentfiles includes file that does not exist in the mod directory: {addlFile}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_updatesSpecifiesMissingAdditionalDeploymentFiles, addlFile);
                        return;
                    }

                    AdditionalDeploymentFiles = addlFileSplit;
                }
            }

            //Archive file hash for update checks
            OriginalArchiveHash = iniData[@"UPDATES"][@"originalarchivehash"];

            #endregion

            #region Backwards Compatibilty

            //Mod Manager 2.0 supported "modcoal" flag that would replicate Mod Manager 1.0 functionality of coalesced swap since basegame jobs at the time
            //were not yet supportedd

            string modCoalFlag = ModDescTargetVersion == 2 ? iniData[@"ModInfo"][@"modcoal"] : null;
            //This check could be rewritten to simply check for non zero string. However, for backwards compatibility sake, we will keep the original
            //method of checking in place.
            if (modCoalFlag != null && Int32.TryParse(modCoalFlag, out int modCoalInt) && modCoalInt != 0)
            {
                CLog.Information(@"Mod targets ModDesc 2.0, found modcoal flag", Settings.LogModStartup);
                if (!CheckAndCreateLegacyCoalescedJob())
                {
                    CLog.Information($@"---MOD--------END OF {ModName} STARTUP-----------", Settings.LogModStartup);
                    return;
                }
            }
            #endregion

            #region Updater Service (Devs only)
            UpdaterServiceServerFolder = iniData[@"UPDATES"][@"serverfolder"];
            var blacklistedFilesStr = iniData[@"UPDATES"][@"blacklistedfiles"];
            if (blacklistedFilesStr != null)
            {
                var blacklistedFiles = blacklistedFilesStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (var blf in blacklistedFiles)
                {
                    var fullpath = Path.Combine(ModPath, blf);
                    if (File.Exists(fullpath))
                    {

                        Log.Error(@"Mod folder contains file that moddesc.ini blacklists: " + fullpath);
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_blacklistedfileFound, fullpath);
                        return;
                    }
                }
                UpdaterServiceBlacklistedFiles.ReplaceAll(blacklistedFiles);
            }
            #endregion

            #region Updater Service (Devs only)
            //For 104
            //UpdaterServiceServerFolder = iniData[@"UPDATES"][@"serverfolder"];
            //var blacklistedFilesStr = iniData[@"UPDATES"][@"blacklistedfiles"];
            //if (blacklistedFilesStr != null)
            //{
            //    var blacklistedFiles = blacklistedFilesStr.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            //    foreach (var blf in blacklistedFiles)
            //    {
            //        var fullpath = Path.Combine(ModPath, blf);
            //        if (File.Exists(fullpath))
            //        {

            //            Log.Error(@"Mod folder contains file that moddesc.ini blacklists: " + fullpath);
            //            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_blacklistedfileFound, fullpath);
            //            return;
            //        }
            //    }
            //    UpdaterServiceBlacklistedFiles = blacklistedFiles;
            //}
            #endregion

            //What tool to launch post-install
            PostInstallToolLaunch = iniData[@"ModInfo"][@"postinstalltool"];


            //Thread.Sleep(500);
            if (InstallationJobs.Count > 0)
            {
                CLog.Information($@"Finalizing: {InstallationJobs.Count} installation job(s) were found.", Settings.LogModStartup);
                ValidMod = true;
            }
            //else if (emptyModIsOK) //Empty Load OK is used by Mixins. This may be redone for MM6
            //{
            //    CLog.Information($@"Finalizing: No installation jobs were found, but empty mods are allowed in this loading session.", Settings.LogModStartup);
            //    ValidMod = true;
            //}
            else
            {
                Log.Error(@"No installation jobs were specified. This mod does nothing.");
                LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_modDoesNothing);
                return;
            }

            CLog.Information($@"---MOD--------END OF {ModName} STARTUP-----------", Settings.LogModStartup);
        }

        private static readonly string[] allowedConfigFilesME1 = { @"BIOCredits.ini", @"BioEditor.ini", @"BIOEngine.ini", @"BIOGame.ini", @"BIOGuiResources.ini", @"BIOInput.ini", @"BIOParty.in", @"BIOQA.ini" };
        private bool CheckAndCreateLegacyCoalescedJob()
        {
            var legacyCoalFile = FilesystemInterposer.PathCombine(IsInArchive, ModPath, @"Coalesced.bin");
            if (!FilesystemInterposer.FileExists(legacyCoalFile, Archive))
            {
                if (ModDescTargetVersion == 1.0)
                {
                    //Mod Manager 1/1.1
                    Log.Error($@"{ModName} is a legacy mod (cmmver 1.0). This moddesc version requires a Coalesced.bin file in the same folder as the moddesc.ini file, but one was not found.");
                    LoadFailedReason = M3L.GetString(M3L.string_validation_modparsing_loadfailed_cmm1CoalFileMissing);
                }
                else
                {
                    //Mod Manager 2
                    Log.Error($@"{ModName} specifies modcoal descriptor for cmmver 2.0, but the local Coalesced file doesn't exist: {legacyCoalFile}");
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_cmm2CoalFileMissing, legacyCoalFile);
                }

                return false;
            }

            ModJob basegameJob = new ModJob(ModJob.JobHeader.BASEGAME);
            string failurereason = basegameJob.AddFileToInstall(@"BIOGame\CookedPCConsole\Coalesced.bin", @"Coalesced.bin", this);
            if (failurereason != null)
            {
                Log.Error($@"Error occured while creating basegame job for legacy 1.0 mod: {failurereason}");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_loadfailed_errorCreatingLegacyMod, failurereason);
                return false;
            }
            InstallationJobs.Add(basegameJob);
            return true;
        }
    }
}
