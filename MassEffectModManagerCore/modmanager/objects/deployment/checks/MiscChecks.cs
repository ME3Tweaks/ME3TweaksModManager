using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.localizations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using ME3TweaksModManager.modmanager.objects.merge.squadmate;
using ME3TweaksModManager.modmanager.squadmates;
using Newtonsoft.Json;
using ME3TweaksModManager.modmanager.objects.mod.merge.v1;
using ME3TweaksModManager.modmanager.objects.mod.merge;
using ME3TweaksModManager.modmanager.objects.alternates;

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

            // Remove this if check if this ever works in ME2/LE2.
            if (check.ModBeingDeployed.Game.IsGame3())
            {
                var installableFiles = check.ModBeingDeployed.GetAllInstallableFiles();
                if (installableFiles.Any(x => Path.GetFileName(x).Equals(SQMOutfitMerge.SQUADMATE_MERGE_MANIFEST_FILE)))
                {
                    // This mod can install potentially a SquadmateOutfitMerge file. We must ensure all localizations are here.
                    check.DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                    {
                        ItemText = "Squadmate Outfit Merge",
                        ModToValidateAgainst = check.ModBeingDeployed,
                        DialogMessage = "The following issues were found in relation to the Squadmate Outfit Merge feature, which your mod appears to use due to inclusion of a SquadmateMergeInfo.sqm file.",
                        DialogTitle = "Squadmate Outfit Merge issues",
                        ValidationFunction = CheckModForSquadmateOutfitMergeIssues
                    });
                }
            }
        }

        #region Squadmate Outfit Merge
        private static void CheckModForSquadmateOutfitMergeIssues(DeploymentChecklistItem item)
        {
            item.ItemText = "Checking for Squadmate Outfit Merge issues";

            // Validate files
            var installableFiles = item.ModToValidateAgainst.GetAllRelativeReferences();
            var mergeManifests = installableFiles.Where(x => Path.GetFileName(x).Equals(SQMOutfitMerge.SQUADMATE_MERGE_MANIFEST_FILE) && x.Contains(item.ModToValidateAgainst.Game.CookedDirName())).Select(x => Path.Combine(item.ModToValidateAgainst.ModPath, x)).ToList();
            var dlcNames = mergeManifests.Select(x => Directory.GetParent(x).Parent.Name).Distinct().ToArray();

            foreach (var mergeManifest in mergeManifests)
            {
                SQMOutfitMerge.SquadmateMergeInfo mergeInfo = JsonConvert.DeserializeObject<SQMOutfitMerge.SquadmateMergeInfo>(File.ReadAllText(mergeManifest));

                foreach (var henchOutfit in mergeInfo.Outfits)
                {
                    sqmMergeCheckHenchName(item, henchOutfit, item.ModToValidateAgainst.Game);
                    sqmMergeCheckHenchPackages(item, henchOutfit, installableFiles);
                    sqmMergeCheckHenchImages(item, henchOutfit, dlcNames, installableFiles);
                }
            }

            //end setup
            if (!item.HasAnyMessages())
            {
                item.ItemText = "No Squadmate Outfit Merge issues detected";
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.ItemText = "Detected Squadmate Outfit Merge issues";
                item.ToolTip = "Deployment found issues with Squadmate Outfit Merge files";
            }
        }

        private static void sqmMergeCheckHenchPackages(DeploymentChecklistItem item, SquadmateInfoSingle henchOutfit, List<string> installableFiles)
        {
            // Localizations that must be included.
            var packageBases = new[]
            {
                henchOutfit.HenchPackage,
                $@"{henchOutfit.HenchPackage}_Explore",
            };

            // Make sure base package exist
            foreach (var packageBase in packageBases)
            {
                // Check package exists.
                var fullPath = installableFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == packageBase && Path.GetExtension(x) == @".pcc");
                if (fullPath == null)
                {
                    item.AddBlockingError($"Squadmate outfit package '{packageBase}' does not appear to install along with the mod.");
                }
            }


            // Localizations check
            var locs = new[] { @"INT", @"FRA", @"ITA", @"DEU" };
            foreach (var packageBase in packageBases)
            {
                foreach (var loc in locs)
                {
                    var locName = $@"{packageBase}_LOC_{loc}";
                    // Check package exists.
                    var fullPath = installableFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == locName && Path.GetExtension(x) == @".pcc");
                    if (fullPath == null)
                    {
                        item.AddBlockingError($"Localization '{loc}' is missing for hench package {packageBase}.");
                    }
                }
            }
        }

        private static void sqmMergeCheckHenchImages(DeploymentChecklistItem item, SquadmateInfoSingle henchOutfit, string[] dlcNames, List<string> installableFiles)
        {
            string[] images = new[] { henchOutfit.AvailableImage, henchOutfit.HighlightImage }; // silhouetteimage is unlikely to be modified so don't bother checking it.
            foreach (var imageExportPath in images)
            {
                bool found = false;
                bool foundDlcNamePackage = false;
                foreach (var dlcName in dlcNames)
                {
                    var packageFile = $@"SFXHenchImages_{dlcName}";

                    // Check package exists.
                    var paths = installableFiles.Where(x => Path.GetFileNameWithoutExtension(x) == packageFile && Path.GetExtension(x) == @".pcc").Select(x => Path.Combine(item.ModToValidateAgainst.ModPath, x)).ToList();
                    if (paths.Count == 0)
                    {
                        // We will check at end of loop
                        continue;
                    }

                    foundDlcNamePackage = true;

                    foreach (var fullpath in paths)
                    {

                        // Check image exists in package and is of class Texture2D.
                        using var package = MEPackageHandler.OpenMEPackage(fullpath);
                        var export = package.FindExport(imageExportPath);
                        if (export == null)
                        {
                            // We won't check this again until all have been checked due to alternates.
                            continue;
                        }

                        found = true;
                        if (export.ClassName != @"Texture2D")
                        {
                            item.AddBlockingError($"Invalid image value '{imageExportPath}': Export must be of type 'Texture2D', the referenced one is of type '{export.ClassName}'.");
                        }

                        break;
                    }

                    if (!found)
                    {
                        item.AddBlockingError($"Invalid image value '{imageExportPath}': No installable version of '{packageFile}' contains an export with instanced full path of '{imageExportPath}'.");
                    }
                }

                if (!foundDlcNamePackage)
                {
                    item.AddBlockingError($"Hench images package file not found. The package should be named SFXHenchImages_[DLC_MOD_YourModName].");
                }
            }
        }

        private static void sqmMergeCheckHenchName(DeploymentChecklistItem item, SquadmateInfoSingle henchOutfit, MEGame game)
        {
            if (game.IsGame3())
            {
                switch (henchOutfit.HenchName)
                {
                    case "Prothean":
                    case "Marine":
                    case "Tali":
                    case "Liara":
                    case "EDI":
                    case "Garrus":
                    case "Kaidan":
                    case "Ashley":
                        break;
                    default:
                        item.AddBlockingError($"Unknown 'henchname': {henchOutfit.HenchName}. Values are case sensitive.");
                        break;
                }
            }
        }

        #endregion


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

            #region Check for full-file mergemod targets
            // Check if our mod contains any basegame only files that are hot merge mod targets.
            var basegameJob = item.ModToValidateAgainst.GetJob(ModJob.JobHeader.BASEGAME);
            if (basegameJob != null)
            {
                // Get files installed into CookedPC of basegame (without extension)
                var basegameCookedPrefix = $@"BioGame/{item.ModToValidateAgainst.Game.CookedDirName()}/";

                // Job files.
                var cookedDirTargets = basegameJob.FilesToInstall.Keys.Where(x => x.Replace("\\", "/").TrimStart('/').StartsWith(basegameCookedPrefix, StringComparison.InvariantCultureIgnoreCase) && x.RepresentsPackageFilePath()).Select(x => Path.GetFileNameWithoutExtension(x)).ToList();

                // Find alternates that may target this directory.
                var alts = basegameJob.AlternateFiles.Where(
                    x => x.Operation
                is AlternateFile.AltFileOperation.OP_SUBSTITUTE
                or AlternateFile.AltFileOperation.OP_INSTALL
                or AlternateFile.AltFileOperation.OP_APPLY_MULTILISTFILES
                ).ToList();

                foreach (var alt in alts)
                {
                    switch (alt.Operation)
                    {
                        case AlternateFile.AltFileOperation.OP_SUBSTITUTE:
                        case AlternateFile.AltFileOperation.OP_INSTALL:
                            var testPath = alt.ModFile.Replace("\\", "/").TrimStart('/');
                            if (testPath.StartsWith(basegameCookedPrefix, StringComparison.InvariantCultureIgnoreCase) && testPath.RepresentsPackageFilePath())
                            {
                                cookedDirTargets.Add(Path.GetFileNameWithoutExtension(alt.ModFile));
                            }
                            break;
                        case AlternateFile.AltFileOperation.OP_APPLY_MULTILISTFILES:
                            foreach (var mlFile in alt.MultiListSourceFiles)
                            {
                                string destPath;
                                if (alt.FlattenMultilistOutput)
                                {
                                    destPath = alt.MultiListTargetPath + @"\" + Path.GetFileName(mlFile);
                                }
                                else
                                {
                                    destPath = alt.MultiListTargetPath + @"\" + mlFile;
                                }

                                destPath = destPath.Replace(@"\", @"//").TrimStart('/');

                                if (destPath.StartsWith(basegameCookedPrefix, StringComparison.InvariantCultureIgnoreCase) && destPath.RepresentsPackageFilePath())
                                {
                                    cookedDirTargets.Add(Path.GetFileNameWithoutExtension(destPath));
                                }
                            }
                            break;
                    }
                }

                // Get list of files that our merge mod supports.
                var mergeTargets = MergeModLoader.GetAllowedMergeTargetFilenames(item.ModToValidateAgainst.Game).Select(x => Path.GetFileNameWithoutExtension(x).StripUnrealLocalization()).ToList();

                // 
                foreach (var mt in mergeTargets)
                {
                    if (cookedDirTargets.Contains(mt, StringComparer.InvariantCultureIgnoreCase))
                    {
                        item.AddSignificantIssue($"Mod potentially installs file targetable by mergemods: {mt}. Consider changing this mod to use mergemods for this file to be more compatible with other mods.");
                    }
                }
            }
            #endregion

            #region Check for misc development files such as decompiled folders and other .bin files.

            var installableFiles = item.ModToValidateAgainst.GetAllInstallableFiles();

            var developmentOnlyFiles = new[] { @".xml", @".dds", @".png" }; // Should maybe check for duplicate coalesceds...
            var devFiles = installableFiles.Where(x => developmentOnlyFiles.Contains(Path.GetExtension(x))).ToList();
            foreach (var file in devFiles)
            {
                var ext = Path.GetExtension(file);

                item.AddSignificantIssue($"{ext} file may be installed with the mod: {file}. This file should be removed as the game will never use it. If you wish to include extra files such as this, use 'additionaldeploymentfolders' from the [UPDATES] header.");
            }
            #endregion


            // End of check
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
