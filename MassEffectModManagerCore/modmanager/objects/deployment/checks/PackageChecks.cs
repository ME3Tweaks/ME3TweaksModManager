using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                Parallel.ForEach(referencedFiles,
                    new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = Math.Min(3, Environment.ProcessorCount)
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
                        var package = MEPackageHandler.OpenMEPackage(Path.Combine(item.ModToValidateAgainst.ModPath, f));
                        EntryChecker.CheckReferences(item, package, M3L.GetString, relativePath);
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
                            item.ItemText = $"Checking references in mergemods [{lnumChecked - 1}/{mergeMods.Count}]";

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
                                        item.AddBlockingError($"{assetInfo.Key}: {si.Message}", si.Entry);
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
