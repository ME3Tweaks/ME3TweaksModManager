using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.ME3Tweaks.ModManager;
using ME3TweaksCore.Objects;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.objects.alternates
{
    /// <summary>
    /// Container class for a conditional dlc
    /// </summary>
    public class ConditionalDLC : DLCRequirementBase
    {
        private ConditionalDLC() { }

        public static ConditionalDLC MakeConditionalDLC(Mod mod, string input, bool canBePlusMinus)
        {
            if (mod.ModDescTargetVersion >= ModDescConsts.MODDESC_VERSION_9_0)
            {
                // Use the base constructor
                return new ConditionalDLC(input, mod.ModDescTargetVersion, canBePlusMinus);
            }

            // Old versions did not support anything beyond folder name.
            return new ConditionalDLC()
            {
                DLCFolderName = canBePlusMinus ? new PlusMinusKey(input) : new PlusMinusKey(null, input)
            };
        }


        private ConditionalDLC(string input, double featureLevel, bool folderNameIsPlusMinus) : base(input, featureLevel, folderNameIsPlusMinus) { }
    }
}
