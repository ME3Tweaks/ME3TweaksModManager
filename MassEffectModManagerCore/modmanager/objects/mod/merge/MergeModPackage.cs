using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.Shared.BasegameFileIdentification;

namespace ME3TweaksModManager.modmanager.objects.mod.merge
{
    public class MergeFileTransition
    {
        public string FileName { get; set; }
        public string OriginalMD5 { get; set; }
        public string FinalMD5 { get; set; }
        public bool WasSavedOnce { get; set; }

        /// <summary>
        /// List of applied m3m filenames
        /// </summary>
        public List<string> AppliedMergeMods { get; } = new();

        public MergeFileTransition(string filename, string originalMD5)
        {
            FileName = FileName;
            OriginalMD5 = originalMD5;
        }
    }

    /// <summary>
    /// Contains variables that should be passed through to various parts of merge mod during install
    /// </summary>
    public class MergeModPackage
    {
        /// <summary>
        /// Target being installed to
        /// </summary>
        public GameTarget Target { get; init; }

        /// <summary>
        /// Cache of basegame packages that have been opened to improve performance
        /// </summary>
        public PackageCache OpenedBasegameCache { get; init; }

        /// <summary>
        /// The mod this merge mod is associated with
        /// </summary>
        public Mod AssociatedMod { get; init; }

        /// <summary>
        /// Map of each file as it transitions through a merge for the basegame identification service
        /// </summary>
        public CaseInsensitiveConcurrentDictionary<MergeFileTransition> FileTransitionMap = new();

        /// <summary>
        /// Calculates the final hashes
        /// </summary>
        public void FinalizeFileTransitionMap()
        {
            foreach (var key in FileTransitionMap.Keys)
            {
                FileTransitionMap[key].FinalMD5 = MUtilities.CalculateHash(key);
            }
        }

        /// <summary>
        /// Maps a filename to which merge mod object should perform the save (the last one). This can save a lot of time with huge startup files saving multiple times.
        /// Only can be used with higher memory systems as it uses the cache - if it is not uncapped this is unused
        /// </summary>
        public CaseInsensitiveDictionary<IMergeMod> MergeModToSavePackageWith; 

        private CaseInsensitiveDictionary<string> _loadedFiles;

        /// <summary>
        /// List of files that will be used in the target game. Only populates on first access
        /// </summary>
        public CaseInsensitiveDictionary<string> LoadedFiles
        {
            get
            {
                if (_loadedFiles == null)
                {
                    _loadedFiles = MELoadedFiles.GetFilesLoadedInGame(Target.Game, true, gameRootOverride: Target.TargetPath);
                    if (Target.Game == MEGame.LE2)
                    {
                        // SPECIAL CASE: LE2 EntryMenu is loaded before DLC version so first load of the file
                        // will be basegame one. The majority of time this is likely the desirable
                        // file so we only target this one instead.
                        _loadedFiles[@"EntryMenu.pcc"] = Path.Combine(Target.GetCookedPath(), @"EntryMenu.pcc");
                    }
                }
                return _loadedFiles;
            }
        }
    }
}
