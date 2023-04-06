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
using LegendaryExplorerCore.Helpers;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using Microsoft.AppCenter.Crashes;
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

        private const int QUEUE_VERSION_TXT = 1;
        private const int QUEUE_VERSION_BIQ = 2;
        private const int QUEUE_VERSION_BIQ2 = 3;

        /// <summary>
        /// The version of the queue that was used when serializing it from disk. If not specified this will default to the latest
        /// </summary>
        [JsonIgnore]
        public int QueueFormatVersion { get; set; } = QUEUE_VERSION_BIQ2;

        /// <summary>
        /// Mods that are part of the queue. This does not ensure they are available for use, check the properties
        /// </summary>
        [JsonProperty(@"mods")]
        public ObservableCollectionExtended<BatchMod> ModsToInstall { get; } = new ObservableCollectionExtended<BatchMod>();

        /// <summary>
        /// ASI mods that are part of the queue.
        /// </summary>
        [JsonProperty(@"asimods")]
        public ObservableCollectionExtended<BatchASIMod> ASIModsToInstall { get; } = new ObservableCollectionExtended<BatchASIMod>();

        /// <summary>
        /// Texture mods that will install at the end of the installation.
        /// </summary>
        [JsonIgnore] // This is built after deserialization
        public ObservableCollectionExtended<MEMMod> TextureModsToInstall { get; } = new ObservableCollectionExtended<MEMMod>();

        /// <summary>
        /// SERIALIZATION ONLY - Stores the list of MEM file paths befoer they are parsed into TextureModsToInstall
        /// </summary>
        [JsonProperty("texturemodfiles")]
        public List<string> SerializeOnly_MEMFilePaths { get; set; }

        /// <summary>
        /// Only used for UI binding!
        /// </summary>
        [JsonIgnore]
        public ObservableCollectionExtended<object> AllModsToInstall { get; } = new ObservableCollectionExtended<object>();

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
        /// If the installer should use the saved options or ignore them
        /// </summary>
        [JsonIgnore]
        public bool UseSavedOptions { get; set; }

        /// <summary>
        /// Reads a batch file from disk and parses it
        /// </summary>
        /// <param name="queueFile"></param>
        /// <param name="allLoadedMods"></param>
        /// <returns></returns>
        public static BatchLibraryInstallQueue ParseInstallQueue(string queueFile)
        {
            // Check for size is commented out while we try to debug this problem
            if (!File.Exists(queueFile) /*|| new FileInfo(queueFile).Length == 0*/) return null;
            M3Log.Information($@"Parsing batch queue file {queueFile}");
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
                result.QueueFormatVersion = QUEUE_VERSION_BIQ;
            }
            else
            {
                //Old Mod Manager 5 format. This code is only used for transition purposes
                result.Game = MEGame.ME3;
                result.QueueFormatVersion = QUEUE_VERSION_TXT;
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
            try
            {
                var modernQueue = JsonConvert.DeserializeObject<BatchLibraryInstallQueue>(queueJson);
                modernQueue.BackingFilename = queueFilename;
                modernQueue.QueueFormatVersion = QUEUE_VERSION_BIQ2;
                foreach (var mod in modernQueue.ModsToInstall)
                {
                    mod.Init();
                }
                foreach (var mod in modernQueue.ASIModsToInstall)
                {
                    mod.AssociateASIObject(modernQueue.Game);
                }

                // Associate any M3-managed texture mods, otherwise use a basic MEMMod object.
                if (modernQueue.SerializeOnly_MEMFilePaths != null)
                {
                    foreach (var texModPath in modernQueue.SerializeOnly_MEMFilePaths)
                    {
                        var matchingM3Entry = M3LoadedMods.GetAllM3ManagedMEMs()
                            .FirstOrDefault(x =>
                                x.GetFilePathToMEM().CaseInsensitiveEquals(texModPath)); // Filepath the same!
                        if (matchingM3Entry == null)
                        {
                            MEMMod m = new MEMMod(texModPath);
                            modernQueue.TextureModsToInstall.Add(m);
                        }
                        else
                        {
                            modernQueue.TextureModsToInstall.Add(matchingM3Entry);
                        }
                    }
                }

                modernQueue.SerializeOnly_MEMFilePaths = null; // Remove this data as it's only used during serialization

                // Populate the full list of mods for UI binding
                modernQueue.AllModsToInstall.AddRange(modernQueue.ModsToInstall);
                modernQueue.AllModsToInstall.AddRange(modernQueue.ASIModsToInstall);
                modernQueue.AllModsToInstall.AddRange(modernQueue.TextureModsToInstall);

                return modernQueue;
            }
            catch (Exception e)
            {
                M3Log.Exception(e, @"Failure reading modern batch queue:");
                Crashes.TrackError(new Exception(@"Failed to read modern batch queue", e), new Dictionary<string, string>()
                {
                    {@"Filename", queueFilename},
                    {@"Queue Text", queueJson}
                });
                return null;
            }
        }

        /// <summary>
        /// Parses legacy .txt and .biq files into Mod Manager 8 style queues
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="lines"></param>
        /// <param name="line"></param>
        private static void ParseLegacyQueue(BatchLibraryInstallQueue queue, string[] lines, int line)
        {
            M3Log.Information(@"Deserializing legacy queue");
            queue.QueueName = lines[line];
            line++;

            if (lines.Length >= line)
            {
                // Observed crash when deserializing this in telemetry
                queue.QueueDescription = lines[line];
                line++;
            }

            while (line < lines.Length)
            {
                string moddescPath = lines[line];
                var libraryRoot = M3LoadedMods.GetModDirectoryForGame(queue.Game);
                //workaround for 103/104 to 105: moddesc path's in biq were stored as full paths instead of relative. me3cmm is relative paths
                var fullModdescPath = File.Exists(moddescPath) ? moddescPath : Path.Combine(libraryRoot, moddescPath);

                var batchMod = new BatchMod()
                {
                    ModDescPath = $@"{queue.Game}\{moddescPath}" // 08/25/2022 -> This is for conversion from biq to biq2 which uses library root instead
                };
                Mod m = M3LoadedMods.Instance.AllLoadedMods.FirstOrDefault(x => x.ModDescPath.Equals(fullModdescPath, StringComparison.InvariantCultureIgnoreCase));
                if (m != null)
                {
                    batchMod.Mod = m;
                }
                queue.ModsToInstall.Add(batchMod);
                line++;
            }

            queue.AllModsToInstall.ReplaceAll(queue.ModsToInstall); // AllModsToInstall is what determines the UI list as it contains multiple object types.
            queue.InstallCompressed = queue.Game >= MEGame.ME2 && Settings.PreferCompressingPackages;
        }

        /// <summary>
        /// Save this queue to disk
        /// </summary>
        /// <returns></returns>
        internal string Save(bool canOverwrite, string newName = null)
        {
            // Prepare for save
            foreach (var m in ModsToInstall)
            {
                m.PrepareForSave();
            }

            SerializeOnly_MEMFilePaths = TextureModsToInstall.Select(x => x.GetFilePathToMEM()).ToList(); // Serialize the list

            // Commit
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);

            var savePath = getSaveName(newName ?? QueueName, canOverwrite);
            File.WriteAllText(savePath, json);

            SerializeOnly_MEMFilePaths = null; // Clear
            return savePath;
        }

        /// <summary>
        /// Gets the filename to save as
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns></returns>
        private string getSaveName(string groupName, bool canOverwrite)
        {
            var batchfolder = M3LoadedMods.GetBatchInstallGroupsDirectory();
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
