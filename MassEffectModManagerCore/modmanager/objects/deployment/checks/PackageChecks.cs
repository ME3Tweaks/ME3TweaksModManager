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
