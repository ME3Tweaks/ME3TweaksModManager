using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.Helpers;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.mod.texture
{
    /// <summary>
    /// Describes a MEMMod - currently only usable in BatchQueues
    /// </summary>
    public class MEMMod
    {
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
    }
}
