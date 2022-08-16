using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects.mod;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinCopies.Util;

namespace ME3TweaksModManager.modmanager.objects.batch
{
    /// <summary>
    /// Batch queue binded class
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class BatchLibraryInstallQueue
    {
        /// <summary>
        /// The name of the batch queue file. This does not include the path.
        /// </summary>
        [JsonIgnore]
        public string BackingFilename { get; set; }

        /// <summary>
        /// Game this queue is for
        /// </summary>
        [JsonProperty(@"game")]
        [JsonConverter(typeof(StringEnumConverter))]
        public MEGame Game { get; internal set; }

        /// <summary>
        /// The name of the batch queue
        /// </summary>
        [JsonProperty(@"queuename")]
        public string QueueName { get; internal set; }

        /// <summary>
        /// The description of the batch queue
        /// </summary>
        [JsonProperty(@"description")]
        public string QueueDescription { get; internal set; }

        /// <summary>
        /// Mods that are part of the queue. This does not ensure they are available for use, check the properties
        /// </summary>
        [JsonProperty(@"mods")]
        public ObservableCollectionExtended<BatchMod> ModsToInstall { get; } = new ObservableCollectionExtended<BatchMod>();

        /// <summary>
        /// USED FOR SAVING/LOADING FILE FROM DISK
        /// </summary>
        //public List<BatchMod> SerializedMods { get; internal set; }

        /// <summary>
        /// If the mod should be installed with compression. This is only used in OT ME2 and ME3
        /// </summary>
        [JsonIgnore]
        public bool InstallCompressed { get; set; }

        /// <summary>
        /// Reads a batch file from disk and parses it
        /// </summary>
        /// <param name="queueFile"></param>
        /// <param name="allLoadedMods"></param>
        /// <returns></returns>
        public static BatchLibraryInstallQueue ParseInstallQueue(string queueFile)
        {
            if (!File.Exists(queueFile)) return null;
            var queueFilename = Path.GetFileName(queueFile);

            var extension = Path.GetExtension(queueFile);
            if (extension == @".biq2")
            {
                // Mod Manager 8 format that can store options
                return ParseModernQueue(queueFilename, File.ReadAllText(queueFile));
            }

            BatchLibraryInstallQueue result = new BatchLibraryInstallQueue();
            result.BackingFilename = queueFilename;
            string[] lines = File.ReadAllLines(queueFile);
            int line = 0;
            if (extension == @".biq")
            {
                //New Mod Manager 6 format
                if (Enum.TryParse<MEGame>(lines[line], out var game))
                {
                    result.Game = game;
                    line++;
                }
            }
            else
            {
                //Old Mod Manager 5 format. This code is only used for transition purposes
                result.Game = MEGame.ME3;
            }

            ParseLegacyQueue(result, lines, line);
            return result;
        }

        /// <summary>
        /// Parses a modern queue
        /// </summary>
        /// <param name="queueJson"></param>
        /// <returns></returns>
        private static BatchLibraryInstallQueue ParseModernQueue(string queueFilename, string queueJson)
        {
            var modernQueue = JsonConvert.DeserializeObject<BatchLibraryInstallQueue>(queueJson);
            modernQueue.BackingFilename = queueFilename;
            foreach (var mod in modernQueue.ModsToInstall)
            {
                mod.Init();
            }
            return modernQueue;
        }

        /// <summary>
        /// Parses legacy .txt and .biq files into Mod Manager 8 style queues
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="lines"></param>
        /// <param name="line"></param>
        private static void ParseLegacyQueue(BatchLibraryInstallQueue queue, string[] lines, int line)
        {
            queue.QueueName = lines[line];
            line++;
            queue.QueueDescription = lines[line];
            line++;
            while (line < lines.Length)
            {
                string moddescPath = lines[line];
                var libraryRoot = M3Utilities.GetModDirectoryForGame(queue.Game);
                //workaround for 103/104 to 105: moddesc path's in biq were stored as full paths instead of relative. me3cmm is relative paths
                var fullModdescPath = File.Exists(moddescPath) ? moddescPath : Path.Combine(libraryRoot, moddescPath);

                var batchMod = new BatchMod() { ModDescPath = moddescPath };
                Mod m = M3LoadedMods.Instance.AllLoadedMods.FirstOrDefault(x => x.ModDescPath.Equals(fullModdescPath, StringComparison.InvariantCultureIgnoreCase));
                if (m != null)
                {
                    batchMod.Mod = m;
                }
                queue.ModsToInstall.Add(batchMod);
                line++;
            }

            queue.InstallCompressed = queue.Game >= MEGame.ME2 && Settings.PreferCompressingPackages;
        }

        /// <summary>
        /// Save this queue to disk
        /// </summary>
        /// <returns></returns>
        internal string Save(bool canOverwrite)
        {
            // Prepare for save
            foreach (var m in ModsToInstall)
            {
                m.PrepareForSave();
            }

            // Commit
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);

            var savePath = getSaveName(QueueName, canOverwrite);
            File.WriteAllText(savePath, json);
            return savePath;
        }

        /// <summary>
        /// Gets the filename to save as
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns></returns>
        private string getSaveName(string groupName, bool canOverwrite)
        {
            var batchfolder = M3Filesystem.GetBatchInstallGroupsFolder();
            var newFname = M3Utilities.SanitizePath(groupName);
            if (string.IsNullOrWhiteSpace(newFname))
            {
                // Has generic name. We have to make a generic name instead
                return getFirstGenericSavename(batchfolder);
            }
            var newPath = Path.Combine(batchfolder, newFname) + @".biq2";
            if (File.Exists(newPath) && !canOverwrite)
            {
                // cannot overwrite
                return getFirstGenericSavename(batchfolder);
            }

            return newPath;
        }

        private string getFirstGenericSavename(string batchfolder)
        {
            string newFname = Path.Combine(batchfolder, @"batchinstaller-");
            int i = 0;
            while (true)
            {
                i++;
                string nextGenericPath = newFname + i + @".biq2";
                if (!File.Exists(nextGenericPath))
                {
                    return nextGenericPath;
                }
            }
        }

    }

}
