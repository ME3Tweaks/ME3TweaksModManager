using LegendaryExplorerCore.Gammtek.Extensions;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksCore.Targets;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.me3tweakscoreextended
{
    /// <summary>
    /// Extension to create a BasegameFileRecord using a game target and a mod.
    /// </summary>
    public class M3BasegameFileRecord : BasegameFileRecord
    {
        public M3BasegameFileRecord(string fullfilepath, int size, GameTargetWPF gameTarget, Mod modBeingInstalled, string md5 = null)
        {
            this.file = fullfilepath.Substring(gameTarget.TargetPath.Length + 1);
            this.hash = md5 ?? MUtilities.CalculateHash(fullfilepath);
            this.game = gameTarget.Game.ToGameNum().ToString();
            this.size = size;
            this.source = modBeingInstalled.ModName + @" " + modBeingInstalled.ModVersionString;
        }

        /// <summary>
        /// Generates a basegame file record from the file path, the given target, and the display name
        /// </summary>
        /// <param name="fullfilepath"></param>
        /// <param name="target"></param>
        /// <param name="recordedMergeName"></param>
        /// <exception cref="NotImplementedException"></exception>
        public M3BasegameFileRecord(string fullfilepath, GameTarget target, string recordedMergeName)
        {
            this.file = fullfilepath.Substring(target.TargetPath.Length + 1);
            this.hash = MUtilities.CalculateHash(fullfilepath);
            this.game = target.Game.ToGameNum().ToString();
            this.size = (int)new FileInfo(fullfilepath).Length;
            this.source = recordedMergeName;
        }
    }
}
