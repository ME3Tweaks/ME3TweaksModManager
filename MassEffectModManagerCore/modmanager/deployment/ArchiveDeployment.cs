using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.deployment
{
    /// <summary>
    /// Contains methods related to archive deployment
    /// </summary>
    public static class ArchiveDeployment
    {
        /// <summary>
        /// If the mod can be deployed with the current mod manager environment. This is just a basic precheck.
        /// </summary>
        /// <param name="selectedMod"></param>
        /// <returns></returns>
        public static bool CanDeployMod(Mod selectedMod)
        {
            var bup = BackupService.GetBackupStatus(selectedMod.Game);
            var backedUp = bup != null && bup.BackedUp;

            if (backedUp) return true; // We have a backup so we don't need to do further checks

            var referencedFiles = selectedMod.GetAllRelativeReferences();
            foreach (var referencedFile in referencedFiles)
            {
                if (referencedFile.RepresentsPackageFilePath())
                    return false; // We need to check references in this package. Technically we could do a prescan, but I'm too lazy to code all that up.
            }

            return true;
        }
    }
}
