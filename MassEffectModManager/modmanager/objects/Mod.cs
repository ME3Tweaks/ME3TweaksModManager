using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IniParser.Parser;
using Serilog;

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
        private List<ModJob> InstallationJobs = new List<ModJob>();

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

                sb.AppendLine("Modifies: " + string.Join(", ", InstallationJobs.Select(x => x.jobHeader.ToString()).ToList()));
                return sb.ToString();
            }
        }
        public string ModVersionString { get; set; }
        public double ParsedModVersion { get; set; }
        public string ModWebsite { get; set; } = ""; //not null default I guess.
        public double ModDescTargetVersion { get; set; }
        public int ModClassicUpdateCode { get; set; }
        public string LoadFailedReason { get; set; }

        public string ModPath { get; }
        public string ModDescPath => Path.Combine(ModPath, "moddesc.ini");
        public Mod(MemoryStream iniStream)
        {

        }

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

        private void loadMod(string iniText, MEGame expectedGame)
        {
            Game = expectedGame; //we will assign this later. This is for startup errors only
            var outputStartup = Properties.Settings.Default.LogModStartup;
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
            if (ModName == null || ModName == "")
            {
                ModName = Path.GetFileName(ModPath);
                Log.Error($"Moddesc.ini in {ModPath} does not set the modname descriptor.");
                LoadFailedReason = $"The moddesc.ini file located at {ModPath} does not have a value set for modname. This value is required.";
                return; //Won't set valid
            }
            ModDescription = Utilities.ConvertBrToNewline(iniData["ModInfo"]["moddesc"]);
            ModDeveloper = iniData["ModInfo"]["moddev"];
            ModVersionString = iniData["ModInfo"]["modver"];
            double.TryParse(ModVersionString, out double parsedValue);
            ParsedModVersion = parsedValue;

            ModWebsite = iniData["ModInfo"]["modsite"] ?? DefaultWebsite;

            int.TryParse(iniData["ModInfo"]["modid"], out int modmakerId);
            ModModMakerID = modmakerId;

            int.TryParse(iniData["ModInfo"]["updatecode"], out int modupdatecode);
            ModClassicUpdateCode = modupdatecode;
            CLog.Information($"Read modmaker update code (or used default): {ModClassicUpdateCode}", outputStartup);
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
                    if (game == null && Game != MEGame.ME3)
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
                var legacyCoalFile = Path.Combine(ModPath, "Coalesced.bin");
                if (!ignoreLoadErrors && !File.Exists(legacyCoalFile))
                {
                    Log.Error($"{ModName} is a legacy mod (cmmver 1.0). This moddesc version requires a Coalesced.bin file in the same folder as the moddesc.ini file, but one was not found.");
                    LoadFailedReason = $"This mod is a legacy mod (cmmver 1.0). This moddesc version requires a Coalesced.bin file in the same folder as the moddesc.ini file, but one was not found.";
                    return;
                }
                ModJob basegameJob = new ModJob(ModJob.JobHeader.BASEGAME);
                basegameJob.AddFileToInstall(@"BIOGame\CookedPCConsole\Coalesced.bin", legacyCoalFile, ignoreLoadErrors);
                InstallationJobs.Add(basegameJob);
                ValidMod = true;
                CLog.Information($"---MOD--------END OF {ModName} STARTUP-----------", Properties.Settings.Default.LogModStartup);
            }
            if (ModDescTargetVersion >= 2.0 && ModDescTargetVersion < 3) //Mod Manager 2 (2013)
            {
                ModDescTargetVersion = 2.0;
            }
            if (ModDescTargetVersion >= 3 || ModDescTargetVersion <= 3.1) //Mod Manager 3 (2014)
            {
                ModDescTargetVersion = 3.0;
            }
            //A few mods shipped as 3.2 moddesc, however the features they targeted are officially supported in 3.1
            if (ModDescTargetVersion > 3.1 && ModDescTargetVersion < 4.0) //Mod Manager 3.1 (2014)
            {
                ModDescTargetVersion = 3.1;
            }

            //Thread.Sleep(500);
            ValidMod = true;
        }
    }
}
