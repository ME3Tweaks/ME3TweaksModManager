using System;
using System.Collections.Generic;
using System.IO;
using MassEffectModManagerCore.modmanager.objects;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects.mod;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    public class BasegameFileIdentificationService
    {

        private static Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>> LocalBasegameFileIdentificationService;

        private static void LoadLocalBasegameIdentificationService()
        {
            if (LocalBasegameFileIdentificationService != null) return;

            var file = Utilities.GetLocalBasegameIdentificationServiceFile();
            if (File.Exists(file))
            {
                try
                {
                    LocalBasegameFileIdentificationService =
                        JsonConvert
                            .DeserializeObject<
                                Dictionary<string, CaseInsensitiveDictionary<
                                    List<BasegameFileIdentificationService.BasegameCloudDBFile>>>>(
                                File.ReadAllText(file));
                    Log.Information(@"Loaded Local Basegame File Identification Service");
                }
                catch (Exception e)
                {
                    Log.Error($@"Error loading local BGFIS: {e.Message}");
                    LocalBasegameFileIdentificationService = OnlineContent.getBlankBGFIDB();
                }
            }
            else
            {
                Log.Information(@"Loaded blank Local Basegame File Identification Service");
                LocalBasegameFileIdentificationService = OnlineContent.getBlankBGFIDB();
            }
        }

        public static void AddLocalBasegameIdentificationEntries(List<BasegameCloudDBFile> entries)
        {
            LoadLocalBasegameIdentificationService();

            bool updated = false;
            // Update the DB
            foreach (var entry in entries)
            {
                string gameKey = entry.game == @"0" ? @"LELAUNCHER" : Utilities.GetGameFromNumber(entry.game).ToString();
                if (LocalBasegameFileIdentificationService.TryGetValue(gameKey, out var gameDB))
                {
                    List<BasegameCloudDBFile> existingInfos;
                    if (!gameDB.TryGetValue(entry.file, out existingInfos))
                    {
                        existingInfos = new List<BasegameCloudDBFile>();
                        gameDB[entry.file] = existingInfos;
                    }

                    if (existingInfos.All(x => x.hash != entry.hash))
                    {
                        // new info
                        existingInfos.Add(entry);
                        updated = true;
                    }
                }
            }

            // Serialize it back to disk
            if (updated)
            {
                var outText = JsonConvert.SerializeObject(LocalBasegameFileIdentificationService);
                try
                {
                    File.WriteAllText(Utilities.GetLocalBasegameIdentificationServiceFile(), outText);
                    Log.Information(@"Updated Local Basegame File Identification Service");

                }
                catch (Exception e)
                {
                    // bwomp bwomp
                    Log.Error($@"Error saving local BGFIS: {e.Message}");
                }
            }
            else
            {
                Log.Information(@"Local Basegame File Identification Service did not need updating");

            }
        }


        /// <summary>
        /// Looks up information about a basegame file using the Basegame File Identification Service
        /// </summary>
        /// <param name="target"></param>
        /// <param name="fullfilepath"></param>
        /// <returns></returns>
        public static BasegameCloudDBFile GetBasegameFileSource(GameTarget target, string fullfilepath, string md5 = null)
        {
            // Check local first
            LoadLocalBasegameIdentificationService();
            if (LocalBasegameFileIdentificationService.TryGetValue(target.Game.ToString(), out var infosForGameL))
            {
                var relativeFilename = fullfilepath.Substring(target.TargetPath.Length + 1).ToUpper();

                if (infosForGameL.TryGetValue(relativeFilename, out var items))
                {
                    md5 ??= Utilities.CalculateMD5(fullfilepath);
                    var match = items.FirstOrDefault(x => x.hash == md5);
                    if (match != null)
                    {
                        return match;
                    }
                }
            }

            if (App.BasegameFileIdentificationService == null) return null; // ME3Tweaks DB not loaded
            if (App.BasegameFileIdentificationService.TryGetValue(target.Game.ToString(), out var infosForGame))
            {
                var relativeFilename = fullfilepath.Substring(target.TargetPath.Length + 1).ToUpper();

                if (infosForGame.TryGetValue(relativeFilename, out var items))
                {
                    md5 ??= Utilities.CalculateMD5(fullfilepath);
                    return items.FirstOrDefault(x => x.hash == md5);
                }
            }

            return null;
        }

        public class BasegameCloudDBFile
        {
            public string file { get; set; }
            public string hash { get; set; }
            public string source { get; set; }
            public string game { get; set; }
            public BasegameCloudDBFile() { }
            public BasegameCloudDBFile(string file, GameTarget gameTarget, Mod modBeingInstalled, string md5 = null)
            {
                this.file = file.Substring(gameTarget.TargetPath.Length + 1);
                this.hash = md5 ?? Utilities.CalculateMD5(file);
                this.game = gameTarget.Game.ToGameNum().ToString();
                this.source = modBeingInstalled.ModName + @" " + modBeingInstalled.ModVersionString;
            }
        }
    }
}