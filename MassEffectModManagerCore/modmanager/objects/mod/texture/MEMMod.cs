using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.localizations;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.mod.texture
{
    /// <summary>
    /// Describes a MEMMod - currently only usable in BatchQueues
    /// </summary>
    public class MEMMod
    {

        /// <summary>
        /// The header string
        /// </summary>
        public string DisplayString
        {
            get
            {
                if (!PathIsRelativeToModLibrary) return Path.GetFileName(FilePath);


                return Path.GetFileName(FilePath);
            }
        }

        /// <summary>
        /// The path to the .mem file - can be relative or absolute
        /// </summary>
        [JsonProperty("filepath")]
        public string FilePath { get; set; }

        /// <summary>
        /// If the FilePath variable is a relative or absolute path
        /// </summary>

        [JsonProperty("pathisrelativetomodlibrary")]
        public bool PathIsRelativeToModLibrary { get; set; }

        /// <summary>
        /// If this mod is part of a moddesc mod (part of standard mod library)
        /// </summary>
        [JsonProperty("partofmoddescmod")]
        public bool PartOfModdescMod { get; set; }

        /// <summary>
        /// The game this texture mod is for
        /// </summary>
        [JsonIgnore]
        public MEGame Game { get; set; }

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
            PathIsRelativeToModLibrary = other.PathIsRelativeToModLibrary;
            PartOfModdescMod = other.PartOfModdescMod;
            Game = other.Game;
            ModdedTextures = other.ModdedTextures?.ToList(); // Clone the other's object with .ToList()
            FileExists = other.FileExists; // Should we do this...?
        }

        public MEMMod(Mod mod, string memModIniStruct)
        {
            var parms = StringStructParser.GetCommaSplitValues(memModIniStruct);
            FilePath = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, @"Textures", parms[@"Filename"]);
            
            
            PartOfModdescMod = true;
        }

        public void ParseData()
        {
            var filePath = PathIsRelativeToModLibrary ? Path.Combine(M3LoadedMods.GetCurrentModLibraryDirectory(), FilePath) : FilePath;
            FileExists = File.Exists(filePath);
            if (FileExists)
            {
                Game = ModFileFormats.GetGameMEMFileIsFor(filePath);
                ModdedTextures = ModFileFormats.GetFileListForMEMFile(filePath);
            }
        }

        /// <summary>
        /// Returns the list of modified export names. 
        /// </summary>
        /// <returns></returns>
        public List<string> GetModifiedExportNames()
        {
            if (ModifiedExportNames != null) return ModifiedExportNames;
            var memPath = GetFilePathToMEM();
            if (File.Exists(memPath))
            {
                ModifiedExportNames = ModFileFormats.GetFileListForMEMFile(memPath).OrderBy(x => x).ToList(); // Alphabetize
            }
            else
            {
                ModifiedExportNames = new List<string>();
            }

            return ModifiedExportNames;
        }

        /// <summary>
        /// Gets the full path to the MEM file.
        /// </summary>
        /// <returns></returns>
        public string GetFilePathToMEM()
        {
            if (PathIsRelativeToModLibrary == false) return FilePath;
            return Path.Combine(M3LoadedMods.GetCurrentModLibraryDirectory(), FilePath);
        }
    }
}
