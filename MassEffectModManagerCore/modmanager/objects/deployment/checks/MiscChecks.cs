using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.localizations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.deployment.checks
{
    internal static class MiscChecks
    {
        /// <summary>
        /// Adds miscellaneous checks to the encompassing mod deployment checks.
        /// </summary>
        /// <param name="check"></param>
        public static void AddMiscChecks(EncompassingModDeploymentCheck check)
        {
            check.DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = M3L.GetString(M3L.string_miscellaneousChecks),
                ModToValidateAgainst = check.ModBeingDeployed,
                DialogMessage = M3L.GetString(M3L.string_atLeastOneMiscellaneousCheckFailed),
                DialogTitle = M3L.GetString(M3L.string_detectedMiscellaneousIssues),
                ValidationFunction = CheckModForMiscellaneousIssues
            });
        }


        /// <summary>
        /// Checks for ALOT markers
        /// </summary>
        /// <param name="item"></param>
        private static void CheckModForMiscellaneousIssues(DeploymentChecklistItem item)
        {
            item.ItemText = M3L.GetString(M3L.string_checkingForMiscellaneousIssues);
            var referencedFiles = item.ModToValidateAgainst.GetAllRelativeReferences(false);

            var metacmms = referencedFiles.Where(x => Path.GetFileName(x) == @"_metacmm.txt").ToList();

            if (metacmms.Any())
            {
                foreach (var m in metacmms)
                {
                    //Mods cannot include metacmm files
                    item.AddBlockingError(M3L.GetString(M3L.string_interp_modReferencesMetaCmm, m));
                }
            }

            // Check for ALOT markers
            var packageFiles = referencedFiles.Where(x => x.RepresentsPackageFilePath());
            foreach (var p in packageFiles)
            {
                var fullPath = Path.Combine(item.ModToValidateAgainst.ModPath, p);

                if (M3Utilities.HasALOTMarker(fullPath))
                {
                    item.AddBlockingError(M3L.GetString(M3L.string_interp_error_textureTaggedFileFound, p));
                }

                var package = MEPackageHandler.QuickOpenMEPackage(fullPath);
                {
                    if (package.NameCount == 0)
                    {
                        item.AddBlockingError(M3L.GetString(M3L.string_interp_packageFileNoNames, p));
                    }

                    if (package.ImportCount == 0)
                    {
                        // Is there always an import? I assume from native classes...?
                        item.AddBlockingError(M3L.GetString(M3L.string_interp_packageFileNoImports, p));
                    }

                    if (package.ExportCount == 0)
                    {
                        item.AddBlockingError(M3L.GetString(M3L.string_interp_packageFileNoExports, p));
                    }

                    if (package.Game != item.ModToValidateAgainst.Game)
                    {
                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningPackageForOtherGameFound, package.FilePath.Substring(item.ModToValidateAgainst.ModPath.Length + 1), package.Game, item.ModToValidateAgainst.Game));
                    }
                }
            }

            //Check moddesc.ini for things that shouldn't be present - unofficial
            if (item.ModToValidateAgainst.IsUnofficial)
            {
                item.AddBlockingError(M3L.GetString(M3L.string_error_foundUnofficialDescriptor));
            }

            //Check moddesc.ini for things that shouldn't be present - importedby
            if (item.ModToValidateAgainst.ImportedByBuild > 0)
            {
                item.AddBlockingError(M3L.GetString(M3L.string_error_foundImportedByDesriptor));
            }

            // Check mod name length
            if (item.ModToValidateAgainst.ModName.Length > 40)
            {
                item.AddInfoWarning(M3L.GetString(M3L.string_interp_infoModNameTooLong, item.ModToValidateAgainst.ModName, item.ModToValidateAgainst.ModName.Length));
            }

            //end setup
            if (!item.HasAnyMessages())
            {
                item.ItemText = M3L.GetString(M3L.string_noMiscellaneousIssuesDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.ItemText = M3L.GetString(M3L.string_detectedMiscellaneousIssues);
                item.ToolTip = M3L.GetString(M3L.string_tooltip_deploymentChecksFoundMiscIssues);
            }
        }

    }
}
