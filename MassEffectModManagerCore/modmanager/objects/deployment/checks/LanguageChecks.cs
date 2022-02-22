using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using ME3TweaksCore.Objects;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.deployment.checks
{
    internal static class LanguageChecks
    {
        /// <summary>
        /// Adds language checks to the encompassing mod deployment checks.
        /// </summary>
        /// <param name="check"></param>
        public static void AddLanguageChecks(EncompassingModDeploymentCheck check)
        {
            var customDLCJob = check.ModBeingDeployed.GetJob(ModJob.JobHeader.CUSTOMDLC);
            if (customDLCJob != null)
            {
                var customDLCFolders = customDLCJob.CustomDLCFolderMapping.Keys.ToList();
                customDLCFolders.AddRange(customDLCJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC).Select(x => x.AlternateDLCFolder));

                if (customDLCFolders.Count > 0)
                {
                    check.DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                    {
                        ItemText = M3L.GetString(M3L.string_languageSupportCheck),
                        ModToValidateAgainst = check.ModBeingDeployed,
                        ValidationFunction = CheckLocalizations,
                        DialogMessage = M3L.GetString(M3L.string_languageSupportCheckDetectedFollowingIssues),
                        DialogTitle = M3L.GetString(M3L.string_languageSupportIssuesDetectedInMod)
                    });
                }
            }
        }


        /// <summary>
        /// Checks localizations and proper DLC mod setup for TLK
        /// </summary>
        /// <param name="item"></param>
        private static void CheckLocalizations(DeploymentChecklistItem item)
        {
            var customDLCJob = item.ModToValidateAgainst.GetJob(ModJob.JobHeader.CUSTOMDLC);
            var customDLCFolders = customDLCJob.CustomDLCFolderMapping.Keys.ToList();
            customDLCFolders.AddRange(customDLCJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC).Select(x => x.AlternateDLCFolder));
            var languages = GameLanguage.GetLanguagesForGame(item.ModToValidateAgainst.Game);
            item.ItemText = M3L.GetString(M3L.string_languageCheckInProgress);
            foreach (var customDLC in customDLCFolders)
            {
                if (item.CheckDone) return;
                if (item.ModToValidateAgainst.Game.IsGame2() || item.ModToValidateAgainst.Game.IsGame3())
                {
                    var modCookedDir = Path.Combine(item.ModToValidateAgainst.ModPath, customDLC, item.ModToValidateAgainst.Game.CookedDirName());
                    var mountFile = Path.Combine(modCookedDir, @"mount.dlc");
                    if (!File.Exists(mountFile))
                    {
                        item.AddBlockingError(M3L.GetString(M3L.string_interp_noMountDlcFile, customDLC));
                        continue;
                    }

                    var mount = new MountFile(mountFile);

                    int moduleNum = -1;
                    if (item.ModToValidateAgainst.Game.IsGame2())
                    {
                        // Look up the module number
                        var bioengine = Path.Combine(modCookedDir, @"BioEngine.ini");
                        if (!File.Exists(bioengine))
                        {
                            item.AddBlockingError(M3L.GetString(M3L.string_interp_me2NoBioEngineFile, customDLC));
                            continue;
                        }
                        else
                        {
                            var ini = DuplicatingIni.LoadIni(Path.Combine(bioengine));
                            if (!int.TryParse(ini[@"Engine.DLCModules"][customDLC]?.Value, out moduleNum) || moduleNum < 1)
                            {
                                item.AddBlockingError(M3L.GetString(M3L.string_interp_me2MissingInvalidModuleNum, customDLC));
                                continue;
                            }
                        }
                    }

                    var tlkBasePath = item.ModToValidateAgainst.Game.IsGame2() ? $@"DLC_{moduleNum}" : customDLC;
                    Dictionary<string, List<TLKStringRef>> tlkMappings = new Dictionary<string, List<TLKStringRef>>();
                    foreach (var language in languages)
                    {
                        if (item.CheckDone) return;
                        var tlkLangPath = Path.Combine(modCookedDir, tlkBasePath + @"_" + language.FileCode + @".tlk");
                        if (File.Exists(tlkLangPath))
                        {
                            //inspect
                            var tf = new ME2ME3TalkFile();
                            tf.LoadTlkData(tlkLangPath);
                            tlkMappings[language.FileCode] = tf.StringRefs;

                            //Check string order
                            var malestringRefsInRightOrder = tf.StringRefs.Take(tf.MaleEntryCount).IsAscending((x, y) => x.CalculatedID.CompareTo(y.CalculatedID)); //male strings
                            var femalestringRefsInRightOrder = tf.StringRefs.Skip(tf.MaleEntryCount).Take(tf.FemaleEntryCount).IsAscending((x, y) => x.CalculatedID.CompareTo(y.CalculatedID)); //male strings
                            string gender = M3L.GetString(M3L.string_male);
                            if (!malestringRefsInRightOrder)
                            {
                                //Some TLK strings will not work
                                item.AddSignificantIssue(M3L.GetString(M3L.string_interp_error_outOfOrderTLK, gender, language.FileCode));
                            }

                            if (!femalestringRefsInRightOrder)
                            {
                                gender = M3L.GetString(M3L.string_female);
                                //Some TLK strings will not work
                                item.AddSignificantIssue(M3L.GetString(M3L.string_interp_error_outOfOrderTLK, gender, language.FileCode));
                            }

                            // Check to make sure TLK contains the mount file TLK ID
                            var referencedStr = tf.FindDataById(mount.TLKID);
                            if (referencedStr == null || referencedStr == @"No Data")
                            {
                                // TLK STRING REF NOT FOUND
                                item.AddBlockingError(M3L.GetString(M3L.string_interp_missingReferencedTlkStrInMod, customDLC, Path.GetFileName(tlkLangPath), mount.TLKID));
                                continue;
                            }
                        }
                        else
                        {
                            item.AddSignificantIssue(M3L.GetString(M3L.string_interp_customDLCMissingLocalizedTLK, customDLC, language.FileCode));
                        }
                    }

                    if (tlkMappings.Any())
                    {
                        CheckTLKMappings(tlkMappings, item);
                    }
                    else if (tlkMappings.Count == 0)
                    {
                        item.AddBlockingError(M3L.GetString(M3L.string_interp_dlcModHasNoTlkFiles, customDLC));
                    }
                }
                else if (item.ModToValidateAgainst.Game.IsGame1())
                {
                    // Game 1
                    var parsedIni = DuplicatingIni.LoadIni(Path.Combine(item.ModToValidateAgainst.ModPath, customDLC, @"AutoLoad.ini"));

                    // We only check TalkTable1 since... who's gonna use TalkTable2,3,4...?
                    var tlkFileBaseName = parsedIni[@"Packages"][@"GlobalTalkTable1"]?.Value;
                    if (tlkFileBaseName == null)
                    {
                        item.AddBlockingError(M3L.GetString(M3L.string_interp_dlcModMissingTlkInAutoloadIni, customDLC));
                    }
                    else
                    {
                        // Check TLK exists.
                        var tlkExportObjName = tlkFileBaseName.Split('.').Last();
                        tlkFileBaseName = tlkFileBaseName.Substring(0, tlkFileBaseName.IndexOf(@".")); //They end with _tlk
                        var tlkExtension = item.ModToValidateAgainst.Game == MEGame.ME1 ? @".upk" : @".pcc";
                        var tlkPackagePath = Directory.GetFiles(Path.Combine(item.ModToValidateAgainst.ModPath, customDLC), $@"{tlkFileBaseName}{tlkExtension}", SearchOption.AllDirectories).FirstOrDefault();
                        if (tlkPackagePath == null)
                        {
                            item.AddBlockingError(M3L.GetString(M3L.string_interp_dlcModMissingAutoLoadTlkFile, customDLC, tlkFileBaseName, tlkExtension));
                        }
                        else
                        {
                            var tlkMappings = new Dictionary<string, List<TLKStringRef>>();
                            Game1CheckTLK(item, tlkFileBaseName, tlkExtension, tlkExportObjName, customDLC, languages, tlkMappings);
                        }
                    }
                }
            }

            if (item.HasAnyMessages())
            {
                item.ItemText = M3L.GetString(M3L.string_languageCheckDetectedIssues);
                item.ToolTip = M3L.GetString(M3L.string_validationFailed);
            }
            else
            {
                item.ItemText = M3L.GetString(M3L.string_noLanguageIssuesDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
        }

        private static void Game1CheckTLK(DeploymentChecklistItem item, string tlkPackageBaseName, string tlkExtension, string tlkExportObjName, string customDLC, GameLanguage[] languages, Dictionary<string, List<TLKStringRef>> tlkMappings)
        {
            var tlkPackages = Directory.GetFiles(Path.Combine(item.ModToValidateAgainst.ModPath, customDLC), $@"{tlkPackageBaseName}*{tlkExtension}", SearchOption.AllDirectories);

            foreach (var language in languages)
            {
                if (item.CheckDone) return;
                // Open and inspect TLK package.
                var langExt = language.FileCode == @"INT" ? "" : $@"_{language.FileCode}";
                var tlkFilename = tlkPackageBaseName + langExt + tlkExtension;
                var tlkPackagePath = tlkPackages.FirstOrDefault(x => Path.GetFileName(x) == tlkFilename);

                if (tlkPackagePath == null)
                {
                    item.AddSignificantIssue($"Localized TLK package {tlkFilename} was not found. Users who play the game with their language/voiceover set to {language.FileCode} ({language.HumanDescription}) will have blank strings from this mod. See the Mod Manager documentation for more information.");
                }
                else
                {
                    var tlkPackage = MEPackageHandler.OpenMEPackage(tlkPackagePath);
                    var tfExports = tlkPackage.Exports.Where(x => x.ObjectName.Name.StartsWith(tlkExportObjName) && x.ClassName == @"BioTlkFile").ToList();
                    if (tfExports.Count == 0)
                    {
                        item.AddBlockingError(M3L.GetString(M3L.string_interp_dlcModTlkPackageHasNoUsableTlk, customDLC));
                    }
                    else if (tfExports.Count == 1)
                    {
                        item.AddBlockingError($"TLK package {tlkFilename} only has 1 BioTalkFile export, it should have 2: one for male, one for female.");
                    }
                    else
                    {
                        var maleTLK = new ME1TalkFile(tfExports[0]);
                        var femaleTLK = new ME1TalkFile(tfExports[1]);

                        if (maleTLK.StringRefs.Count != femaleTLK.StringRefs.Count)
                        {
                            item.AddSignificantIssue($"TLK package {tlkFilename} has different numbers of strings in the male/female TLK exports. The count should be identical.");
                        }


                        tlkMappings[language.FileCode] = maleTLK.StringRefs;
                    }
                }
            }


            // Check mappings.
        }

        /// <summary>
        /// Generates messages based on if all TLKs have the same string IDs.
        /// </summary>
        /// <param name="tlkMappings">Mapping of language -> stringrefs in that language.</param>
        /// <param name="item"></param>
        private static void CheckTLKMappings(Dictionary<string, List<TLKStringRef>> tlkMappings, DeploymentChecklistItem item)
        {
            double numLoops = Math.Pow(tlkMappings.Count, 2);
            int numDone = 0;
            foreach (var mapping1 in tlkMappings)
            {
                foreach (var mapping2 in tlkMappings)
                {
                    var percent = (int)((numDone * 100.0) / numLoops);
                    item.ItemText = $@"{M3L.GetString(M3L.string_languageCheckInProgress)} {percent}%";
                    numDone++;

                    if (mapping1.Equals(mapping2))
                    {
                        continue;
                    }

                    var differences = mapping1.Value.Select(x => x.StringID).Except(mapping2.Value.Select(x => x.StringID));
                    foreach (var difference in differences)
                    {
                        var str = mapping1.Value.FirstOrDefault(x => x.StringID == difference)?.Data ?? M3L.GetString(M3L.string_errorFindingString);
                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_tlkDifference, difference.ToString(), mapping1.Key, mapping2.Key, str));
                    }
                }
            }
        }
    }
}
