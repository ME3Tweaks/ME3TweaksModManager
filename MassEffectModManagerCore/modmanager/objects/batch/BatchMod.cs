using ME3TweaksModManager.modmanager.objects.mod;
using Newtonsoft.Json;
using PropertyChanged;
using WinCopies.Util;

namespace ME3TweaksModManager.modmanager.objects.batch
{
    [AddINotifyPropertyChangedInterface]
    public class BatchMod
    {
        public BatchMod() { }

        /// <summary>
        /// Creates a batch mod wrapper based on this mod with no chosen options yet
        /// </summary>
        /// <param name="m"></param>
        public BatchMod(Mod m)
        {
            Mod = m;
            ModDescPath = m.ModDescPath.Substring(M3LoadedMods.GetCurrentModLibraryDirectory().Length + 1); // Make relative to root of library (global)
        }

        /// <summary>
        /// The matching mod in the library
        /// </summary>
        [JsonIgnore]
        public Mod Mod { get; set; }

        /// <summary>
        /// The path on disk for the associated mod
        /// </summary>
        [JsonProperty(@"moddescpath")]
        public string ModDescPath { get; set; }

        /// <summary>
        /// The hash of the moddesc.ini file - if the one on disk does not match this, options must (should?) be rechosen
        /// </summary>
        [JsonProperty(@"moddeschash")]
        public string ModDescHash { get; set; }

        /// <summary>
        /// If the moddesc hash does not match the one on disk
        /// </summary>
        [JsonIgnore]
        public bool ChosenOptionsDesync { get; set; }

        /// <summary>
        /// If the mod is not available for install, e.g. it was removed
        /// </summary>
        [JsonIgnore]
        public bool ModMissing => Mod == null;

        /// <summary>
        /// List of chosen option keys
        /// </summary>
        [JsonProperty(@"chosenoptions")]
        public List<string> ChosenOptions { get; set; }

        /// <summary>
        /// If options have been chosen for this mod - even if there are none.
        /// </summary>
        [JsonProperty(@"haschosenoptions")]
        public bool HasChosenOptions { get; set; }

        /// <summary>
        /// Initializes and associates a mod with this object
        /// </summary>
        public void Init()
        {
            var libraryRoot = M3LoadedMods.GetCurrentModLibraryDirectory(); // biq2 stores relative to library root. biq stores to library root FOR GAME

            var fullModdescPath = Path.Combine(libraryRoot, ModDescPath);
            if (File.Exists(fullModdescPath))
            {
                Mod m = M3LoadedMods.Instance.AllLoadedMods.FirstOrDefault(x => x.ModDescPath.Equals(fullModdescPath, StringComparison.InvariantCultureIgnoreCase));
                if (m != null)
                {
                    Mod = m;
                    var localHash = M3Utilities.CalculateMD5(Mod.ModDescPath);
                    ChosenOptionsDesync = ModDescHash != null && localHash != ModDescHash;
                }
                else
                {
                    M3Log.Warning($@"Batch queue mod has moddesc but is not a loaded mod in library: {fullModdescPath}");
                }
            }
            else
            {
                M3Log.Warning($@"Batch queue mod not available in library: {fullModdescPath}");
            }
        }

        /// <summary>
        /// Populates variables that must be available when serializing to disk
        /// </summary>
        public void PrepareForSave()
        {
            if (Mod != null)
            {
                ModDescHash = M3Utilities.CalculateMD5(Mod.ModDescPath);
            }
        }
    }
}
