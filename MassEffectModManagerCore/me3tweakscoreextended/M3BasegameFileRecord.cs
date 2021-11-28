using LegendaryExplorerCore.Gammtek.Extensions;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksCore.Targets;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.me3tweakscoreextended
{
    /// <summary>
    /// /Extension to create a BasegameFileRecord using a game target and a mod.
    /// </summary>
    public class M3BasegameFileRecord : BasegameFileRecord
    {
        public M3BasegameFileRecord(string fullfilepath, int size, GameTarget gameTarget, Mod modBeingInstalled, string md5 = null)
        {
            this.file = fullfilepath.Substring(gameTarget.TargetPath.Length + 1);
            this.hash = md5 ?? MUtilities.CalculateMD5(fullfilepath);
            this.game = gameTarget.Game.ToGameNum().ToString();
            this.size = size;
            this.source = modBeingInstalled.ModName + @" " + modBeingInstalled.ModVersionString;
        }
    }
}
