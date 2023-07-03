using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.objects.deployment.checks
{
    internal static class PackageChecks
    {

        /// <summary>
        /// Adds package checks to the encompassing mod deployment checks.
        /// </summary>
        /// <param name="check"></param>
        public static void AddPackageChecks(EncompassingModDeploymentCheck check)
        {
            check.DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = M3L.GetString(M3L.string_referencesCheck),
                ModToValidateAgainst = check.ModBeingDeployed,
                DialogMessage = M3L.GetString(M3L.string_interp_dialog_invalidReferencesFound),
                DialogTitle = M3L.GetString(M3L.string_invalidNameAndObjectReferences),
                ValidationFunction = CheckReferences
            });
        }

        /// <summary>
        /// Checks object and name references for invalid values and if values are of the incorrect typing.
        /// </summary>
        /// <param name="item"></param>
        private static void CheckReferences(DeploymentChecklistItem item)
        {
            item.ItemText = M3L.GetString(M3L.string_checkingNameAndObjectReferences);
            var referencedFiles = item.ModToValidateAgainst.GetAllRelativeReferences().Where(x => x.RepresentsPackageFilePath()).Select(x => Path.Combine(item.ModToValidateAgainst.ModPath, x)).ToList();
            int numChecked = 0;

            try
            {
                // Load heavy-lookups into cache
                // Use postload since most shipped files will check these
                var game = item.ModToValidateAgainst.Game;

                // Max 100 blank packages
                using PackageCache localCache = new PackageCache() { CacheMaxSize = 100 };

                Parallel.ForEach(referencedFiles,
                    new ParallelOptions()
                    {
#if DEBUG
                        MaxDegreeOfParallelism = 1
#else
                        MaxDegreeOfParallelism = Math.Max(3, Environment.ProcessorCount)
#endif
                    },
                    f =>
                    //foreach (var f in referencedFiles)
                    {
                        if (item.CheckDone) return;
                        // Mostly ported from ME3Explorer
                        var lnumChecked = Interlocked.Increment(ref numChecked);
                        item.ItemText = M3L.GetString(M3L.string_checkingNameAndObjectReferences) + $@" [{lnumChecked - 1}/{referencedFiles.Count}]";

                        var relativePath = f.Substring(item.ModToValidateAgainst.ModPath.Length + 1);
                        M3Log.Information($@"Checking package and name references in {relativePath}");
                        using var package = MEPackageHandler.OpenMEPackage(Path.Combine(item.ModToValidateAgainst.ModPath, f));
                        EntryChecker.CheckReferences(item, package, M3L.GetString, relativePath);

                        // Todo: Check for bad properties?

#if DEBUG
                        // Maybe enable in 8.1 - import resolution needs improvement
                        var localDirFiles = Directory.GetFiles(Directory.GetParent(package.FilePath).FullName);
                        foreach (var import in package.Imports)
                        {
                            if (GlobalUnrealObjectInfo.IsAKnownNativeClass(import.InstancedFullPath, game))
                                continue; // Don't bother looking up since it'll never be found

                            var resolved = EntryImporter.CanResolveImport(import, null, localCache, @"INT", localDirFiles);
                            if (!resolved)
                            {
                                if (game.IsGame2())
                                {
                                    // Check for the known bad missing ones
                                    if (import.InstancedFullPath is
                                            @"BioVFX_Z_TEXTURES.Generic.Glass_Shards_Norm" or
                                            // The following two have incorrectly spelled names
                                            // But they aren't in game files anyways
                                            @"BIOG_Humanoid_MASTER_MTR_R.Skin_HumanHED_SpecMulitplier_Mask" or 
                                            @"BIOG_Humanoid_MASTER_MTR_R.Skin_HumanScalp_SpecMulitplier_Mask")
                                        continue; // These are not present in Game 2 files for some reason. According to BioWare they were also never committed to P4
                                }
                                item.AddInfoWarning($"Legendary Explorer Core could not resolve import: {import.UIndex} {import.InstancedFullPath} in {relativePath}", new LEXOpenable(import));
                            }
                        }
#endif
                    });

                // Check mergemod references

                numChecked = 0;
                var mergeMods = item.ModToValidateAgainst.GetJob(ModJob.JobHeader.BASEGAME)?.MergeMods;
                if (mergeMods != null && mergeMods.Any())
                {

                    Parallel.ForEach(mergeMods,
                        new ParallelOptions()
                        {
                            MaxDegreeOfParallelism = Math.Min(3, Environment.ProcessorCount)
                        },
                        mergeMod =>
                        //foreach (var f in referencedFiles)
                        {
                            if (item.CheckDone) return;
                            // Mostly ported from ME3Explorer
                            var lnumChecked = Interlocked.Increment(ref numChecked);
                            item.ItemText = M3L.GetString(M3L.string_deployment_mergemodPackageCheckingReferences, lnumChecked - 1, mergeMods.Count);

                            var relativePath = $@"{Mod.MergeModFolderName}\{mergeMod.MergeModFilename}";
                            M3Log.Information($@"Checking package and name references in mergemod {relativePath}");

                            var mergeModPath = Path.Combine(item.ModToValidateAgainst.ModPath, Mod.MergeModFolderName, mergeMod.MergeModFilename);
                            using var mergeStream = File.OpenRead(mergeModPath);

                            foreach (var assetInfo in mergeMod.Assets)
                            {
                                if (assetInfo.Key.RepresentsPackageFilePath())
                                {
                                    if (assetInfo.Value.AssetBinary == null)
                                    {
                                        assetInfo.Value.ReadAssetBinary(mergeStream);
                                    }

                                    var package = MEPackageHandler.OpenMEPackageFromStream(new MemoryStream(assetInfo.Value.AssetBinary), $@"{assetInfo.Key}");

                                    // Warnings in Merge assets are treated as blocking errors.
                                    ReferenceCheckPackage rcp = new ReferenceCheckPackage();
                                    EntryChecker.CheckReferences(rcp, package, M3L.GetString, relativePath);

                                    foreach (var si in rcp.GetSignificantIssues().Concat(rcp.GetBlockingErrors()))
                                    {
                                        // Entry checker uses relative path. Our relative path is set to the m3m though.
                                        item.AddBlockingError($@"{assetInfo.Key}: {si.Message}", si.Entry);
                                    }
                                }
                            }
                        });
                }
            }
            catch (Exception e)
            {
                Crashes.TrackError(new Exception(M3L.GetString(M3L.string_errorOccurredCheckingReferences), e));
                M3Log.Error($@"An error occurred checking references for deployment: {e.Message}.");
                item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningExceptionOccurredDuringRefChecks, e.Message));
            }

            if (!item.HasAnyMessages())
            {
                item.ItemText = M3L.GetString(M3L.string_noReferenceIssuesWereDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.ItemText = M3L.GetString(M3L.string_referencesCheckDetectedIssues);
                item.ToolTip = M3L.GetString(M3L.string_validationFailed);
            }
        }
    }
}
