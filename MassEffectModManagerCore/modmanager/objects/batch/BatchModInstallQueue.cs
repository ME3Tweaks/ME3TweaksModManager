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
using ME3TweaksCore.Helpers;
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
        /// SERIALIZATION ONLY - Stores the list of MEM file paths before they are parsed into TextureModsToInstall
        /// </summary>
        [JsonProperty(@"texturemodfiles")]
        public List<SerializedTextureMod> SerializeOnly_MEMFilePaths { get; set; }

        /// <summary>
        /// Only used for UI binding!
        /// </summary>
        [JsonIgnore]
        public ObservableCollectionExtended<IBatchQueueMod> AllModsToInstall { get; } = new ObservableCollectionExtended<IBatchQueueMod>();

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
                var modernQueue = JsonConvert.DeserializeObject<BatchLibraryInstallQueue>(queueJson, new JsonSerializerSettings()
                {
                    Error = (sender, args) =>
                    {
                        var currentError = args.ErrorContext.Error.Message;
                        M3Log.Warning($@"Error deserializing {queueFilename}: {currentError}. Part of this queue will not be deserialized.");
                        args.ErrorContext.Handled = true; // ignore errors to try to bring up as much as possible
                    }
                });
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
                    var modLibraryPath = M3LoadedMods.GetCurrentModLibraryDirectory();
                    var textureLibraryPath = M3LoadedMods.GetTextureLibraryDirectory();
                    var registeredTextureMods = M3LoadedMods.GetAllM3ManagedMEMs(game: modernQueue.Game);
                    foreach (var textureModEntry in modernQueue.SerializeOnly_MEMFilePaths)
                    {
                        MEMMod matchingM3Entry = null;
                        if (textureModEntry.AttachedToModdescMod)
                        {
                            var onDiskPath = Path.Combine(modLibraryPath, textureModEntry.TextureModPath);
                            matchingM3Entry = registeredTextureMods.FirstOrDefault(x => x.GetFilePathToMEM().CaseInsensitiveEquals(onDiskPath)); // Find registered M3MM with same filepath
                        }
                        else if (textureModEntry.InTextureLibrary)
                        {
                            var onDiskPath = Path.Combine(M3LoadedMods.GetTextureLibraryDirectory(), textureModEntry.TextureModPath);
                            matchingM3Entry = registeredTextureMods.FirstOrDefault(x => x.GetFilePathToMEM().CaseInsensitiveEquals(onDiskPath)); // Find registered MEMMod with same filepath
                        }
                        // Other case is stored entirely on disk

                        if (matchingM3Entry == null)
                        {
                            // Will store full path
                            // Preload with our known data to enable FSS lookups
                            var expectedPath = textureModEntry.TextureModPath;

                            if (textureModEntry.AttachedToModdescMod)
                            {
                                expectedPath = Path.Combine(modLibraryPath, textureModEntry.TextureModPath);
                            }
                            else if (textureModEntry.InTextureLibrary)
                            {
                                expectedPath = Path.Combine(textureLibraryPath, textureModEntry.TextureModPath);
                            }
                            MEMMod m = new MEMMod(expectedPath)
                            {
                                Hash = textureModEntry.TextureModHash,
                                Size = textureModEntry.TextureModSize,
                            };

                            m.ParseMEMData();
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

            SerializeOnly_MEMFilePaths = TextureModsToInstall.Select(x =>
            {
                var s = new SerializedTextureMod()
                {
                    TextureModPath = x.GetFilePathToMEM(),
                    TextureModSize = x.Size, // Use the existing saved (in case we can't compute it)
                    TextureModHash = x.Hash, // Use the existing saved (in case we can't compute it)
                    AttachedToModdescMod = x is M3MEMMod, // M3MEMMod files are content-mod based texture mods (part of moddesc.ini bundle)
                    InTextureLibrary = x.GetFilePathToMEM().StartsWith(M3LoadedMods.GetTextureLibraryDirectory(), StringComparison.InvariantCultureIgnoreCase) // If this is part of loose imported texture files
                };

                if (File.Exists(x.GetFilePathToMEM()))
                {
                    s.TextureModSize = new FileInfo(x.GetFilePathToMEM()).Length;
                    if (x.Hash == null || x.InitialLoadedSize != x.Size || s.TextureModSize < (FileSize.MebiByte * 128))
                    {
                        // If we don't have a hash listed we MUST hash this into the object
                        // If the initial loaded size has changed vs what we stored we must also change it (because the underlying file has changed)
                        // Hash it. If it's big, we don't hash it, we just trust the size, to save on time.
                        // I am not fully sure how reliable this system is...
                        s.TextureModHash = MUtilities.CalculateHash(x.GetFilePathToMEM());
                    }
                    else
                    {
                        s.TextureModHash = @"0".PadLeft(32); // Hash 0...0
                    }

                    if (x is M3MEMMod mm && mm.ModdescMod != null)
                    {
                        // Store library relative path 
                        s.TextureModPath = mm.ModdescMod.ModPath.Substring(M3LoadedMods.GetCurrentModLibraryDirectory().Length + 1) + Path.DirectorySeparatorChar + mm.GetRelativePathToMEM();
                    }
                    else if (s.InTextureLibrary)
                    {
                        // Store library relative path (after import)
                        s.TextureModPath = x.GetFilePathToMEM().Substring(M3LoadedMods.GetTextureLibraryDirectory().Length + 1);
                    }
                }

                return s;
            }).ToList(); // Serialize the list

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

        /// <summary>
        /// If this queue has recorded any options in the chosen values for validation
        /// </summary>
        /// <returns></returns>
        public bool HasAnyRecordedOptions()
        {
            foreach (var mod in ModsToInstall)
            {
                if (mod.AllChosenOptionsForValidation.Any())
                    return true;
            }

            return false;
        }
    }

}
