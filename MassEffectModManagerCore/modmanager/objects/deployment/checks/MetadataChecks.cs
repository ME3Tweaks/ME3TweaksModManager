using ME3TweaksCore.ME3Tweaks.ModManager;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.modmanager.objects.mod;


namespace ME3TweaksModManager.modmanager.objects.deployment.checks
{
    internal static class MetadataChecks
    {
        public static void AddMetadataChecks(EncompassingModDeploymentCheck check)
        {
            string versionString = check.ModBeingDeployed.ParsedModVersion != null ? check.ModBeingDeployed.ParsedModVersion.ToString(M3Utilities.GetDisplayableVersionFieldCount(check.ModBeingDeployed.ParsedModVersion)) : check.ModBeingDeployed.ModVersionString;
            string versionFormat = check.ModBeingDeployed.ModDescTargetVersion < ModDescConsts.MODDESC_VERSION_6_0 ? @"X.X" : @"X.X[.X[.X]]";
            string checklistItemText = check.ModBeingDeployed.ParsedModVersion != null ? M3L.GetString(M3L.string_verifyModVersion) : M3L.GetString(M3L.string_recommendedVersionFormatNotFollowed, versionFormat);
            check.DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = $@"{checklistItemText}: {versionString}",
                ValidationFunction = ManualValidationFunction.ManualValidation
            });
            check.DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = $@"{M3L.GetString(M3L.string_verifyURLIsCorrect)} {check.ModBeingDeployed.ModWebsite}",
                ValidationFunction = URLValidation,
                DialogMessage = M3L.GetString(M3L.string_validation_badUrl),
                DialogTitle = M3L.GetString(M3L.string_modUrlErrorsWereFound),
                ModToValidateAgainst = check.ModBeingDeployed
            });
            check.DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = M3L.GetString(M3L.string_verifyModDescription),
                ModToValidateAgainst = check.ModBeingDeployed,
                ValidationFunction = ManualValidationFunction.DescriptionValidation,
            });
            if (check.ModBeingDeployed.Game == MEGame.ME3) // SFAR Check (ME3 only)
            {
                check.DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                {
                    ItemText = M3L.GetString(M3L.string_sfarFilesCheck),
                    ModToValidateAgainst = check.ModBeingDeployed,
                    DialogMessage = M3L.GetString(M3L.string_invalidSfarSize),
                    DialogTitle = M3L.GetString(M3L.string_wrongSfarSizesFound),
                    ValidationFunction = CheckModSFARs,
                });
            }
        }


        /// <summary>
        /// Validates the URL of the mod
        /// </summary>
        /// <param name="item"></param>
        private static void URLValidation(DeploymentChecklistItem item)
        {
            bool OK = Uri.TryCreate(item.ModToValidateAgainst.ModWebsite, UriKind.Absolute, out var uriResult)
                      && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            if (!OK)
            {
                string url = item.ModToValidateAgainst.ModWebsite ?? @"null";
                item.AddBlockingError(M3L.GetString(M3L.string_interp_urlIsNotValid, url));
                item.ItemText = M3L.GetString(M3L.string_emptyOrInvalidModUrl);
                item.ToolTip = M3L.GetString(M3L.string_validationFailed);
            }
            else
            {
                if (item.ModToValidateAgainst.ModWebsite == Mod.DefaultWebsite)
                {
                    item.AddSignificantIssue(M3L.GetString(M3L.string_noModWebsiteSet));
                    item.ItemText = M3L.GetString(M3L.string_moddescMissingModsite);
                }
                else
                {
                    // 06/27/2024 - try to get info from Nexus page
                    if (!string.IsNullOrWhiteSpace(item.ModToValidateAgainst.ModWebsite))
                    {
                        item.Hyperlink = item.ModToValidateAgainst.ModWebsite;
                        if (NexusModsUtilities.HasAPIKey)
                        {
                            var nfi = NexusFileInfo.FromModSite(item.ModToValidateAgainst.Game,
                                item.ModToValidateAgainst.ModWebsite);
                            if (nfi != null)
                            {
                                // its a nexus link
                                try
                                {
                                    var modInfo = NexusModsUtilities.GetClient().Mods.GetMod(nfi.Domain, nfi.ModId)
                                        .Result;
                                    if (modInfo != null)
                                    {
                                        item.ItemText = M3L.GetString(M3L.string_interp_nexusModsPageX, modInfo.Name);
                                        item.ToolTip = M3L.GetString(M3L.string_validationOK);
                                        item.Hyperlink = item.ModToValidateAgainst.ModWebsite;
                                        return;
                                    }
                                }
                                catch (Exception e)
                                {
                                    M3Log.Error(
                                        $@"Error getting mod information from NexusMods: {e.Message}. This may be expected if mod page is not created yet.");
                                }
                            }
                        }
                    }

                    item.ItemText = M3L.GetString(M3L.string_interp_modURLOK, item.ModToValidateAgainst.ModWebsite);
                    item.ToolTip = M3L.GetString(M3L.string_validationOK);
                }
            }
        }
        /// <summary>
        /// Checks SFAR sizes
        /// </summary>
        /// <param name="item"></param>
        private static void CheckModSFARs(DeploymentChecklistItem item)
        {
            item.ItemText = M3L.GetString(M3L.string_checkingSFARFilesSizes);
            var referencedFiles = item.ModToValidateAgainst.GetAllRelativeReferences().Select(x => Path.Combine(item.ModToValidateAgainst.ModPath, x)).ToList();
            bool hasSFARs = false;
            foreach (var f in referencedFiles)
            {
                if (item.CheckDone) return;
                if (Path.GetExtension(f) == @".sfar")
                {
                    hasSFARs = true;
                    if (new FileInfo(f).Length != 32)
                    {
                        {
                            item.AddBlockingError(f.Substring(item.ModToValidateAgainst.ModPath.Length + 1));
                        }
                    }
                }
            }

            if (!hasSFARs)
            {
                item.ItemText = M3L.GetString(M3L.string_modDoesNotUseSFARs);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                if (item.HasAnyMessages())
                {
                    item.ItemText = M3L.GetString(M3L.string_someSFARSizesAreTheIncorrectSize);
                    item.ToolTip = M3L.GetString(M3L.string_validationFailed);
                }
                else
                {
                    item.ItemText = M3L.GetString(M3L.string_noSFARSizeIssuesWereDetected);
                    item.ToolTip = M3L.GetString(M3L.string_validationOK);
                }
            }
        }
    }
}
