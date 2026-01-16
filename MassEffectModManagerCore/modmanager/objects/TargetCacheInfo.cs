using LegendaryExplorerCore.Packages;
using ME3TweaksCoreWPF.Targets;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// Information about a cached target, including its validity state
    /// </summary>
    public class TargetCacheInfo
    {
        /// <summary>
        /// The game for this cached target
        /// </summary>
        public MEGame Game { get; set; }
        
        /// <summary>
        /// The path to the target directory
        /// </summary>
        public string TargetPath { get; set; }
        
        /// <summary>
        /// Whether the target is valid
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// The reason the target failed to load (null if valid)
        /// </summary>
        public string FailureReason { get; set; }
        
        /// <summary>
        /// The loaded target object (null if failed to load)
        /// </summary>
        public GameTargetWPF Target { get; set; }

        public TargetCacheInfo(MEGame game, string targetPath, bool isValid, string failureReason, GameTargetWPF target = null)
        {
            Game = game;
            TargetPath = targetPath;
            IsValid = isValid;
            FailureReason = failureReason;
            Target = target;
        }
    }
}
