using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.FileSource;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.batch;
using ME3TweaksModManager.modmanager.objects.mod.interfaces;
using Newtonsoft.Json;
using SevenZip;
using SevenZip.EventArguments;

namespace ME3TweaksModManager.modmanager.objects.mod.texture
{
    /// <summary>
    /// Describes a MEMMod, which is a containing object for a .mem file
    /// </summary>
    public class MEMMod : M3ValidateOnLoadObject, IImportableMod, INotifyPropertyChanged, IBatchQueueMod
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _displayString;

        /// <summary>
        /// The header string
        /// </summary>
        [JsonIgnore]
        public string DisplayString
        {
            get
            {
                if (_displayString != null) return _displayString;
                return Path.GetFileName(FilePath);
            }
            set
            {
                if (_displayString != value)
                {
                    _displayString = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayString)));
                }
            }
        }

        /// <summary>
        /// The full path to the .mem file
        /// </summary>
        [JsonProperty(@"filepath")]
        public string FilePath { get; set; }

        /// <summary>
        /// The game this texture mod is for
        /// </summary>
        [JsonIgnore]
        public MEGame Game { get; set; }

        /// <summary>
        /// String for showing in the UI.
        /// </summary>
        [JsonIgnore]
        public string UIDescription => GetDescription();

        /// <summary>
        /// A list of modded textures as parsed out of the mem file. This can be null if it hasn't been parsed
        /// </summary>
        [JsonIgnore]
        public List<string> ModdedTextures { get; set; }

        /// <summary>
        /// If this file exists on disk (UI binding)
        /// </summary>
        [JsonIgnore]
        public bool FileExists { get; set; }

        /// <summary>
        /// The list of texture exports this MEM mod modifies. Cached the first time the list is used. 
        /// </summary>
        [JsonIgnore]
        public List<string> ModifiedExportNames { get; set; }

        public bool IsInArchive { get; init; }

        /// <summary>
        /// Gets the full path to the MEM file. This method can be overridden.
        /// </summary>
        /// <returns></returns>
        public virtual string GetFilePathToMEM()
        {
            return FilePath;
        }

        /// <summary>
        /// Blank initialization constructor
        /// </summary>
        public MEMMod() { }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">Other MEM mod to clone</param>
        public MEMMod(MEMMod other)
        {
            FilePath = other.FilePath;
            Game = other.Game;
            ModdedTextures = other.ModdedTextures?.ToList(); // Clone the other's object with .ToList()
            FileExists = other.FileExists; // Should we do this...?
            IsInArchive = other.IsInArchive;
        }

        public MEMMod(string filePath)
        {
            FilePath = filePath;
            ModName = DisplayString;
        }

        public void ParseMEMData()
        {
            var filePath = GetFilePathToMEM();
            FileExists = File.Exists(filePath);
            if (FileExists)
            {
                Game = ModFileFormats.GetGameMEMFileIsFor(filePath);
                ModdedTextures = ModFileFormats.GetFileListForMEMFile(filePath);
            }
        }



        public virtual string GetDescription()
        {
            if (IsInArchive)
            {
                return M3L.GetString(M3L.string_textureModsMustBeImportedBeforeUse);
            }
            var modifiedExports = GetModifiedExportNames();
            var exportNotListed = M3L.GetString(M3L.string_textureExportsNotListedInThisMemFile);
            var exportList = string.Join('\n', modifiedExports.Select(x => $@" - {(string.IsNullOrWhiteSpace(x) ? exportNotListed : x)}")); // do not localize
            return M3L.GetString(M3L.string_interp_textureModModifiesExportsX, exportList);
        }

        /// <summary>
        /// Returns the list of modified export names. 
        /// </summary>
        /// <returns></returns>
        public List<string> GetModifiedExportNames()
        {
            if (ModifiedExportNames != null) return ModifiedExportNames;
            var filePath = GetFilePathToMEM();
            if (File.Exists(filePath))
            {
                ModifiedExportNames = ModFileFormats.GetFileListForMEMFile(filePath).OrderBy(x => x).ToList(); // Alphabetize
            }
            else
            {
                ModifiedExportNames = new List<string>();
            }

            return ModifiedExportNames;
        }

        #region NEWTONSOFT STUFF
        public virtual bool ShouldSerializeFilePath()
        {
            return true;
        }
        #endregion

        // IImportableMod Interface
        public bool SelectedForImport { get; set; }
        public string ModName { get; set; }
        public bool ValidMod { get; set; } = true;
        public long SizeRequiredtoExtract { get; set; }

        public void ExtractFromArchive(string archivePath,
            string unused0,
            bool unused1,
            Action<string> textUpdateCallback = null,
            Action<DetailedProgressEventArgs> extractingCallback = null,
            Action<string, int, int> unused2 = null,
            bool testMode = false,
            Stream archiveStream = null, NexusProtocolLink sourceNXMLink = null)
        {
            if (archiveStream == null && !File.Exists(archivePath))
            {
                throw new Exception(M3L.GetString(M3L.string_interp_theArchiveFileArchivePathIsNoLongerAvailable, archivePath));
            }

            SevenZipExtractor archive;
            bool closeStreamOnFinish = true;
            if (archiveStream != null)
            {
                archive = new SevenZipExtractor(archiveStream);
                closeStreamOnFinish = false;
            }
            else
            {
                archive = new SevenZipExtractor(archivePath);
            }

            var fileIndicesToExtract = archive.ArchiveFileData.Where(x => x.FileName == FilePath).Select(x => x.Index).ToArray();
            void archiveExtractionProgress(object? sender, DetailedProgressEventArgs args)
            {
                extractingCallback?.Invoke(args);
            }
            archive.Progressing += archiveExtractionProgress;
            M3Log.Information(@"Extracting files...");
            archive.ExtractFiles(M3LoadedMods.GetTextureLibraryDirectory(), fileIndicesToExtract);
            archive.Progressing -= archiveExtractionProgress;

            // Done with archive
            if (closeStreamOnFinish)
            {
                archive?.Dispose();
            }
            else
            {
                archive?.DisposeObjectOnly();
            }

            // Put into file directory
            var memPath = Path.Combine(M3LoadedMods.GetTextureLibraryDirectory(), Path.GetFileName(FilePath));

            // But first, inventory and hash the file
            if (sourceNXMLink != null)
            {
                var hash = MUtilities.CalculateHash(memPath);

                var downloadLink = sourceNXMLink.ToNexusDownloadPageLink();
                var dictionary = new CaseInsensitiveDictionary<FileSourceRecord>();
                dictionary[hash] = new FileSourceRecord()
                {
                    DownloadLink = downloadLink,
                    Hash = hash,
                    Size = new FileInfo(memPath).Length,
                    Name = Path.GetFileName(memPath)
                };
                FileSourceService.AddFileSourceEntries(dictionary, Settings.EnableTelemetry ? ServerManifest.GetInt(ServerManifest.SERVER_ALIGNMENT) : null);
            }

            var game = ModFileFormats.GetGameMEMFileIsFor(memPath);
            var outputDir = Directory.CreateDirectory(M3LoadedMods.GetTextureLibraryDirectory(game)).FullName;
            var outPath = Path.Combine(outputDir, Path.GetFileName(memPath));
            M3Log.Information($@"Moving .mem file to final destination: {memPath} -> {outPath}");
            File.Move(memPath, outPath, true);
        }

        public bool IsAvailableForInstall()
        {
            return FileExists;
        }
    }
}
    