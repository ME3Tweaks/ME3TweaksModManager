using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using FontAwesome.WPF;
using MassEffectModManagerCore.modmanager.windows;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using SevenZip;
using Brushes = System.Windows.Media.Brushes;
using Microsoft.Win32;
using MassEffectModManagerCore.modmanager.gameini;
using MassEffectModManagerCore.modmanager.localizations;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.TLK.ME1;
using ME3ExplorerCore.TLK.ME2ME3;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.Classes;
using Serilog;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ArchiveDeployment.xaml
    /// </summary>
    public partial class ArchiveDeployment : MMBusyPanelBase
    {
        public objects.mod.Mod ModBeingDeployed { get; }
        public string Header { get; set; } = M3L.GetString(M3L.string_prepareModForDistribution);
        public bool MultithreadedCompression { get; set; } = true;
        public string DeployButtonText { get; set; } = M3L.GetString(M3L.string_pleaseWait);
        public ArchiveDeployment(GameTarget validationTarget, objects.mod.Mod mod)
        {
            ValidationTarget = validationTarget;
            Analytics.TrackEvent(@"Started deployment panel for mod", new Dictionary<string, string>()
            {
                { @"Mod name" , $@"{mod.ModName} {mod.ParsedModVersion}"}
            });
            DataContext = this;
            ModBeingDeployed = mod;
            string versionString = mod.ParsedModVersion != null ? mod.ParsedModVersion.ToString(Utilities.GetDisplayableVersionFieldCount(mod.ParsedModVersion)) : mod.ModVersionString;
            string versionFormat = mod.ModDescTargetVersion < 6 ? @"X.X" : @"X.X[.X[.X]]";
            string checklistItemText = mod.ParsedModVersion != null ? M3L.GetString(M3L.string_verifyModVersion) : M3L.GetString(M3L.string_recommendedVersionFormatNotFollowed, versionFormat);
            DeploymentChecklistItems.Add(new DeploymentChecklistItem() { ItemText = $@"{checklistItemText}: {versionString}", ValidationFunction = ManualValidation });
            DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = $@"{M3L.GetString(M3L.string_verifyURLIsCorrect)} {mod.ModWebsite}",
                ValidationFunction = URLValidation,
                ErrorsMessage = M3L.GetString(M3L.string_validation_badUrl),
                ErrorsTitle = M3L.GetString(M3L.string_modUrlErrorsWereFound)
            });
            DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = M3L.GetString(M3L.string_verifyModDescription),
                ValidationFunction = ManualValidation

            });
            if (mod.Game == MEGame.ME3)
            {
                DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                {
                    ItemText = M3L.GetString(M3L.string_sfarFilesCheck),
                    ModToValidateAgainst = mod,
                    ErrorsMessage = M3L.GetString(M3L.string_invalidSfarSize),
                    ErrorsTitle = M3L.GetString(M3L.string_wrongSfarSizesFound),
                    ValidationFunction = CheckModSFARs
                });
            }
            var customDLCJob = mod.GetJob(ModJob.JobHeader.CUSTOMDLC);
            if (customDLCJob != null)
            {
                var customDLCFolders = customDLCJob.CustomDLCFolderMapping.Keys.ToList();
                customDLCFolders.AddRange(customDLCJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC).Select(x => x.AlternateDLCFolder));

                if (customDLCFolders.Count > 0)
                {
                    DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                    {
                        ItemText = M3L.GetString(M3L.string_languageSupportCheck),
                        ModToValidateAgainst = mod,
                        ValidationFunction = CheckLocalizations,
                        ErrorsMessage = M3L.GetString(M3L.string_languageSupportCheckDetectedFollowingIssues),
                        ErrorsTitle = M3L.GetString(M3L.string_languageSupportIssuesDetectedInMod)
                    });
                }
            }
            if (mod.Game >= MEGame.ME2)
            {
                DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                {
                    ItemText = M3L.GetString(M3L.string_audioCheck),
                    ModToValidateAgainst = mod,
                    ValidationFunction = CheckAFCs,
                    ErrorsMessage = M3L.GetString(M3L.string_audioCheckDetectedErrors),
                    ErrorsTitle = M3L.GetString(M3L.string_audioIssuesDetectedInMod)
                });
            }
            DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = M3L.GetString(M3L.string_texturesCheck),
                ModToValidateAgainst = mod,
                ErrorsMessage = M3L.GetString(M3L.string_texturesCheckDetectedErrors),
                ErrorsTitle = M3L.GetString(M3L.string_textureErrorsInMod),
                ValidationFunction = CheckTextures
            });
            DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = M3L.GetString(M3L.string_miscellaneousChecks),
                ModToValidateAgainst = mod,
                ErrorsMessage = M3L.GetString(M3L.string_atLeastOneMiscellaneousCheckFailed),
                ErrorsTitle = M3L.GetString(M3L.string_detectedMiscellaneousIssues),
                ValidationFunction = CheckModForMiscellaneousIssues
            });
            LoadCommands();
            InitializeComponent();

        }

        private void CheckModForMiscellaneousIssues(DeploymentChecklistItem item)
        {
            bool hasError = false;
            bool blocking = false;
            item.HasError = false;
            item.ItemText = M3L.GetString(M3L.string_checkingForMiscellaneousIssues);
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences(false);

            var metacmms = referencedFiles.Where(x => Path.GetFileName(x) == @"_metacmm.txt").ToList();

            if (metacmms.Any())
            {
                item.HasError = true;
                foreach (var m in metacmms)
                {
                    //Mods cannot include metacmm files
                    item.Errors.Add(M3L.GetString(M3L.string_interp_modReferencesMetaCmm, m));
                }
                blocking = true;
            }

            // Check for ALOT markers
            var packageFiles = Utilities.GetPackagesInDirectory(item.ModToValidateAgainst.ModPath, true);
            foreach (var p in packageFiles)
            {
                if (Utilities.HasALOTMarker(p))
                {
                    item.Errors.Add(M3L.GetString(M3L.string_interp_error_textureTaggedFileFound, p));
                    blocking = true;
                }

                var package = MEPackageHandler.QuickOpenMEPackage(p);
                {
                    if (package.NameCount == 0)
                    {
                        item.Errors.Add(M3L.GetString(M3L.string_interp_packageFileNoNames, p));
                        blocking = true;
                    }

                    if (package.ImportCount == 0)
                    {
                        // Is there always an import? I assume from native classes...?
                        item.Errors.Add(M3L.GetString(M3L.string_interp_packageFileNoImports, p));
                        blocking = true;
                    }

                    if (package.ExportCount == 0)
                    {
                        item.Errors.Add(M3L.GetString(M3L.string_interp_packageFileNoExports, p));
                        blocking = true;
                    }
                }
            }

            //Check moddesc.ini for things that shouldn't be present - unofficial
            if (ModBeingDeployed.IsUnofficial)
            {
                item.Errors.Add(M3L.GetString(M3L.string_error_foundUnofficialDescriptor));
                blocking = true;
            }

            //Check moddesc.ini for things that shouldn't be present - importedby
            if (ModBeingDeployed.ImportedByBuild > 0)
            {
                item.Errors.Add(M3L.GetString(M3L.string_error_foundImportedByDesriptor));
                blocking = true;
                DeployButtonText = M3L.GetString(M3L.string_deploymentBlocked);
            }

            //end setup
            if (!item.Errors.Any())
            {
                item.Foreground = Brushes.Green;
                item.Icon = FontAwesomeIcon.CheckCircle;
                item.ItemText = M3L.GetString(M3L.string_noMiscellaneousIssuesDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.ItemText = M3L.GetString(M3L.string_detectedMiscellaneousIssues);
                item.ToolTip = M3L.GetString(M3L.string_tooltip_deploymentChecksFoundMiscIssues);
                item.Foreground = Brushes.Red;
                item.DeploymentBlocking = blocking;
                item.Icon = FontAwesomeIcon.TimesCircle;
            }

            item.HasError = item.Errors.Any();

        }

        private void URLValidation(DeploymentChecklistItem obj)
        {
            bool OK = Uri.TryCreate(ModBeingDeployed.ModWebsite, UriKind.Absolute, out var uriResult)
                      && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            if (!OK)
            {
                obj.HasError = true;
                obj.Icon = FontAwesomeIcon.TimesCircle;
                obj.Foreground = Brushes.Red;
                string url = ModBeingDeployed.ModWebsite ?? @"null";
                obj.Errors = new List<string>(new[] { M3L.GetString(M3L.string_interp_urlIsNotValid, url) });
                obj.ItemText = M3L.GetString(M3L.string_emptyOrInvalidModUrl);
                obj.ToolTip = M3L.GetString(M3L.string_validationFailed);

            }
            else
            {
                if (ModBeingDeployed.ModWebsite == objects.mod.Mod.DefaultWebsite)
                {
                    obj.Icon = FontAwesomeIcon.TimesCircle;
                    obj.Foreground = Brushes.Red;
                    obj.ItemText = M3L.GetString(M3L.string_moddescMissingModsite);
                    obj.Spinning = false;
                    obj.HasError = true;
                    obj.Errors.Add(M3L.GetString(M3L.string_noModWebsiteSet));
                }
                else
                {
                    obj.Icon = FontAwesomeIcon.CheckCircle;
                    obj.Foreground = Brushes.Green;
                    obj.ItemText = M3L.GetString(M3L.string_interp_modURLOK, ModBeingDeployed.ModWebsite);
                    obj.ToolTip = M3L.GetString(M3L.string_validationOK);
                }
            }
        }

        private void CheckLocalizations(DeploymentChecklistItem obj)
        {
            var customDLCJob = ModBeingDeployed.GetJob(ModJob.JobHeader.CUSTOMDLC);
            var customDLCFolders = customDLCJob.CustomDLCFolderMapping.Keys.ToList();
            customDLCFolders.AddRange(customDLCJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC).Select(x => x.AlternateDLCFolder));
            var languages = StarterKitGeneratorWindow.me3languages;
            List<string> errors = new List<string>();
            obj.ItemText = M3L.GetString(M3L.string_languageCheckInProgress);
            foreach (var customDLC in customDLCFolders)
            {
                if (_closed) return;
                if (ModBeingDeployed.Game >= MEGame.ME2)
                {
                    var modCookedDir = Path.Combine(ModBeingDeployed.ModPath, customDLC, ModBeingDeployed.Game == MEGame.ME2 ? @"CookedPC" : @"CookedPCConsole");
                    var mountFile = Path.Combine(modCookedDir, @"mount.dlc");
                    if (!File.Exists(mountFile))
                    {
                        errors.Add(M3L.GetString(M3L.string_interp_noMountDlcFile, customDLC));
                        obj.DeploymentBlocking = true;
                        continue;
                    }

                    var mount = new MountFile(mountFile);

                    int moduleNum = -1;
                    if (ModBeingDeployed.Game == MEGame.ME2)
                    {
                        // Look up the module number
                        var bioengine = Path.Combine(modCookedDir, @"BioEngine.ini");
                        if (!File.Exists(bioengine))
                        {
                            errors.Add(M3L.GetString(M3L.string_interp_me2NoBioEngineFile, customDLC));
                            obj.DeploymentBlocking = true;
                            continue;
                        }
                        else
                        {
                            var ini = DuplicatingIni.LoadIni(Path.Combine(bioengine));
                            if (!int.TryParse(ini[@"Engine.DLCModules"][customDLC]?.Value, out moduleNum) || moduleNum < 1)
                            {
                                errors.Add(M3L.GetString(M3L.string_interp_me2MissingInvalidModuleNum, customDLC));
                                obj.DeploymentBlocking = true;
                                continue;
                            }
                        }
                    }

                    var tlkBasePath = ModBeingDeployed.Game == MEGame.ME2 ? $@"DLC_{moduleNum}" : customDLC;
                    Dictionary<string, List<ME1TalkFile.TLKStringRef>> tlkMappings = new Dictionary<string, List<ME1TalkFile.TLKStringRef>>();
                    foreach (var language in languages)
                    {
                        if (_closed) return;
                        var tlkLangPath = Path.Combine(modCookedDir, tlkBasePath + @"_" + language.filecode + @".tlk");
                        if (File.Exists(tlkLangPath))
                        {
                            //inspect
                            TalkFile tf = new TalkFile();
                            tf.LoadTlkData(tlkLangPath);
                            tlkMappings[language.filecode] = tf.StringRefs;

                            //Check string order
                            var malestringRefsInRightOrder = tf.StringRefs.Take(tf.Header.MaleEntryCount).IsAscending((x, y) => x.StringID.CompareTo(y.StringID)); //male strings
                            var femalestringRefsInRightOrder = tf.StringRefs.Skip(tf.Header.MaleEntryCount).Take(tf.Header.FemaleEntryCount).IsAscending((x, y) => x.StringID.CompareTo(y.StringID)); //male strings
                            string gender = M3L.GetString(M3L.string_male);
                            if (!malestringRefsInRightOrder)
                            {
                                //Some TLK strings will not work
                                errors.Add(M3L.GetString(M3L.string_interp_error_outOfOrderTLK, gender, language.filecode));
                            }

                            if (!femalestringRefsInRightOrder)
                            {
                                gender = M3L.GetString(M3L.string_female);
                                //Some TLK strings will not work
                                errors.Add(M3L.GetString(M3L.string_interp_error_outOfOrderTLK, gender, language.filecode));
                            }

                            // Check to make sure TLK contains the mount file TLK ID
                            var referencedStr = tf.findDataById(mount.TLKID);
                            if (referencedStr == null || referencedStr == @"No Data")
                            {
                                // TLK STRING REF NOT FOUND
                                errors.Add(M3L.GetString(M3L.string_interp_missingReferencedTlkStrInMod, customDLC, Path.GetFileName(tlkLangPath), mount.TLKID));
                                obj.DeploymentBlocking = true;
                                continue;
                            }
                        }
                        else
                        {
                            errors.Add(M3L.GetString(M3L.string_interp_customDLCMissingLocalizedTLK, customDLC, language.filecode));
                        }
                    }

                    if (tlkMappings.Any())
                    {
                        //find TLK with most entries
                        //var tlkCounts = tlkMappings.Select(x => (x.Key, x.Value.Count));
                        double numLoops = Math.Pow(tlkMappings.Count - 1, tlkMappings.Count - 1);
                        int numDone = 0;
                        foreach (var mapping1 in tlkMappings)
                        {
                            foreach (var mapping2 in tlkMappings)
                            {
                                if (mapping1.Equals(mapping2))
                                {
                                    continue;
                                }

                                var differences = mapping1.Value.Select(x => x.StringID).Except(mapping2.Value.Select(x => x.StringID));
                                foreach (var difference in differences)
                                {
                                    var str = mapping1.Value.FirstOrDefault(x => x.StringID == difference)?.Data ?? M3L.GetString(M3L.string_errorFindingString);
                                    errors.Add(M3L.GetString(M3L.string_interp_tlkDifference, difference.ToString(), mapping1.Key, mapping2.Key, str));
                                }

                                numDone++;
                                double percent = (numDone * 100.0) / numLoops;
                                obj.ItemText = $@"{M3L.GetString(M3L.string_languageCheckInProgress)} {percent:0.00}%";
                            }
                        }
                    }
                    else if (tlkMappings.Count == 0)
                    {
                        errors.Add(M3L.GetString(M3L.string_interp_dlcModHasNoTlkFiles, customDLC));
                        obj.DeploymentBlocking = true;
                    }
                }
                else
                {
                    // ME1
                    var parsedIni = DuplicatingIni.LoadIni(Path.Combine(ModBeingDeployed.ModPath, customDLC, @"AutoLoad.ini"));

                    if (int.TryParse(parsedIni[@"GUI"][@"NameStrRef"]?.Value, out var tlkid))
                    {
                        var tlkFile = parsedIni[@"Packages"][@"GlobalTalkTable1"]?.Value;
                        if (tlkFile == null)
                        {
                            errors.Add(M3L.GetString(M3L.string_interp_dlcModMissingTlkInAutoloadIni, customDLC));
                            obj.DeploymentBlocking = true;
                        }
                        else
                        {
                            // Check TLK exists.
                            var tlkExportObjName = tlkFile.Split('.').Last();
                            tlkFile = tlkFile.Substring(0, tlkFile.IndexOf(@"_tlk")); //They end with _tlk
                            var tlkPackagePath = Directory.GetFiles(Path.Combine(ModBeingDeployed.ModPath, customDLC), $@"{tlkFile}.upk").FirstOrDefault();
                            if (tlkPackagePath == null)
                            {
                                errors.Add(M3L.GetString(M3L.string_interp_dlcModMissingAutoLoadTlkFile, customDLC, tlkFile));
                                obj.DeploymentBlocking = true;
                            }
                            else
                            {
                                // Open and inspect TLK package.
                                var tlkPackage = MEPackageHandler.OpenMEPackage(tlkPackagePath);
                                var tfExp = tlkPackage.Exports.FirstOrDefault(x => x.ObjectName == tlkExportObjName && x.ClassName == @"BioTlkFile");
                                if (tfExp == null)
                                {
                                    errors.Add(M3L.GetString(M3L.string_interp_dlcModTlkPackageHasNoUsableTlk, customDLC));
                                    obj.DeploymentBlocking = true;
                                }

                                ME1TalkFile tf = new ME1TalkFile(tfExp);
                                var str = tf.findDataById(tlkid);
                                if (str == null || str == @"No Data")
                                {
                                    // INVALID
                                    errors.Add(M3L.GetString(M3L.string_interp_dlcModTlkPackageMissingStringId, customDLC, tlkid));
                                    obj.DeploymentBlocking = true;
                                }
                                else
                                {
                                    // Valid
                                }
                            }
                        }
                    }
                    else
                    {
                        errors.Add(M3L.GetString(M3L.string_interp_dlcModAutoLoadMissingNameStrRef, customDLC));
                        obj.DeploymentBlocking = true;
                    }
                }
            }

            if (errors.Count > 0)
            {
                obj.HasError = true;
                obj.Icon = obj.DeploymentBlocking ? FontAwesomeIcon.TimesCircle : FontAwesomeIcon.Warning;
                obj.Foreground = obj.DeploymentBlocking ? Brushes.Red : Brushes.Orange;
                obj.Errors = errors;
                obj.ItemText = M3L.GetString(M3L.string_languageCheckDetectedIssues);
                obj.ToolTip = M3L.GetString(M3L.string_validationFailed);

            }
            else
            {
                obj.Icon = FontAwesomeIcon.CheckCircle;
                obj.Foreground = Brushes.Green;
                obj.ItemText = M3L.GetString(M3L.string_noLanguageIssuesDetected);
                obj.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
        }

        private void CheckModSFARs(DeploymentChecklistItem item)
        {
            bool hasError = false;
            item.HasError = false;
            item.ItemText = M3L.GetString(M3L.string_checkingSFARFilesSizes);
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
            int numChecked = 0;
            var errors = new List<string>();
            bool hasSFARs = false;
            foreach (var f in referencedFiles)
            {
                if (_closed) return;
                if (Path.GetExtension(f) == @".sfar")
                {
                    hasSFARs = true;
                    if (new FileInfo(f).Length != 32)
                    {
                        {
                            hasError = true;
                            item.Icon = FontAwesomeIcon.TimesCircle;
                            item.Foreground = Brushes.Red;
                            item.Spinning = false;
                            errors.Add(f.Substring(ModBeingDeployed.ModPath.Length + 1));
                            item.DeploymentBlocking = true;
                        }
                    }
                }
            }

            if (!hasSFARs)
            {
                item.Foreground = Brushes.Green;
                item.Icon = FontAwesomeIcon.CheckCircle;
                item.ItemText = M3L.GetString(M3L.string_modDoesNotUseSFARs);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                if (!hasError)
                {
                    item.Foreground = Brushes.Green;
                    item.Icon = FontAwesomeIcon.CheckCircle;
                    item.ItemText = M3L.GetString(M3L.string_noSFARSizeIssuesWereDetected);
                    item.ToolTip = M3L.GetString(M3L.string_validationOK);
                }
                else
                {
                    item.Errors = errors;
                    item.ItemText = M3L.GetString(M3L.string_someSFARSizesAreTheIncorrectSize);
                    item.ToolTip = M3L.GetString(M3L.string_validationFailed);
                }

                item.HasError = hasError;
            }
        }

        private void ManualValidation(DeploymentChecklistItem item)
        {
            item.Foreground = Brushes.Gray;
            item.Icon = FontAwesomeIcon.CheckCircle;
            item.ToolTip = M3L.GetString(M3L.string_thisItemMustBeManuallyCheckedByYou);
        }

        private void CheckAFCs(DeploymentChecklistItem item)
        {
            bool hasError = false;
            item.HasError = false;
            item.ItemText = M3L.GetString(M3L.string_checkingAudioReferencesInMod);
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
            int numChecked = 0;

            Predicate<string> predicate = s => s.ToLowerInvariant().EndsWith(@".afc", true, null);
            List<string> gameFiles = M3Directories.EnumerateGameFiles(ValidationTarget, predicate);

            var errors = new List<string>();
            Dictionary<string, MemoryStream> cachedAudio = new Dictionary<string, MemoryStream>();

            foreach (var f in referencedFiles)
            {
                if (_closed) return;
                numChecked++;
                item.ItemText = $@"{M3L.GetString(M3L.string_checkingAudioReferencesInMod)} [{numChecked}/{referencedFiles.Count}]";
                if (f.RepresentsPackageFilePath())
                {
                    Log.Information(@"Checking file for audio issues: " + f);
                    var package = MEPackageHandler.OpenMEPackage(f);
                    var wwiseStreams = package.Exports.Where(x => x.ClassName == @"WwiseStream" && !x.IsDefaultObject).ToList();
                    foreach (var wwisestream in wwiseStreams)
                    {
                        if (_closed) return;
                        //Check each reference.
                        var afcNameProp = wwisestream.GetProperty<NameProperty>(@"Filename");
                        if (afcNameProp != null)
                        {
                            string afcNameWithExtension = afcNameProp + @".afc";
                            int audioSize = BitConverter.ToInt32(wwisestream.Data, wwisestream.Data.Length - 8);
                            int audioOffset = BitConverter.ToInt32(wwisestream.Data, wwisestream.Data.Length - 4);

                            string afcPath = null;
                            Stream audioStream = null;
                            var localDirectoryAFCPath = Path.Combine(Path.GetDirectoryName(wwisestream.FileRef.FilePath), afcNameWithExtension);
                            bool isInOfficialArea = false;
                            if (File.Exists(localDirectoryAFCPath))
                            {
                                //local afc
                                afcPath = localDirectoryAFCPath;
                            }
                            else if (referencedFiles.Any(x => Path.GetFileName(x).Equals(afcNameWithExtension, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                //found afc in mod.
                                //if there is multiple same-named AFCs in the mod, this might fail.
                                afcPath = Path.Combine(ModBeingDeployed.ModPath, referencedFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(afcNameWithExtension, StringComparison.InvariantCultureIgnoreCase)));
                                if (!File.Exists(afcPath))
                                {
                                    Debugger.Break();
                                }
                            }
                            else
                            {
                                //Check game
                                var fullPath = gameFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(afcNameWithExtension, StringComparison.InvariantCultureIgnoreCase));
                                if (fullPath != null)
                                {
                                    afcPath = fullPath;
                                    isInOfficialArea = M3Directories.IsInBasegame(afcPath, ValidationTarget) || M3Directories.IsInOfficialDLC(afcPath, ValidationTarget);
                                }
                                else if (cachedAudio.TryGetValue(afcNameProp.Value.Name, out var cachedAudioStream))
                                {
                                    audioStream = cachedAudioStream;
                                    //isInOfficialArea = true; //cached from vanilla SFAR
                                }
                                else if (MEDirectories.OfficialDLC(ValidationTarget.Game).Any(x => afcNameProp.Value.Name.StartsWith(x)))
                                {
                                    var dlcName = afcNameProp.Value.Name.Substring(0, afcNameProp.Value.Name.LastIndexOf(@"_", StringComparison.InvariantCultureIgnoreCase));
                                    var audio = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcName, afcNameWithExtension /*, ValidationTarget*/);
                                    if (audio != null)
                                    {
                                        cachedAudio[afcNameProp.Value.Name] = audio;
                                    }

                                    audioStream = audio;
                                    //isInOfficialArea = true; as this is in a vanilla SFAR we don't test against this since it will be correct.
                                    continue;
                                }
                                else
                                {
                                    Log.Warning($@"Could not find AFC file {afcNameProp.ToString()}.afc. Export: {wwisestream.UIndex} {wwisestream.ObjectName}");
                                    hasError = true;
                                    item.Icon = FontAwesomeIcon.TimesCircle;
                                    item.Foreground = Brushes.Red;
                                    item.Spinning = false;
                                    errors.Add(M3L.GetString(M3L.string_interp_couldNotFindReferencedAFC, wwisestream.FileRef.FilePath.Substring(ModBeingDeployed.ModPath.Length + 1), wwisestream.InstancedFullPath, afcNameProp.ToString()));
                                    continue;
                                }
                            }

                            if (afcPath != null)
                            {
                                audioStream = new FileStream(afcPath, FileMode.Open);
                            }

                            try
                            {
                                audioStream.Seek(audioOffset, SeekOrigin.Begin);
                                if (audioStream.Position > audioStream.Length - 4)
                                {
                                    Log.Warning($@"Found broken audio: {wwisestream.UIndex} {wwisestream.ObjectName} has broken audio, pointer points inside of AFC, but the size ofi t extends beyond the end of the AFC. Package file: {wwisestream.FileRef.FilePath}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}. The AFC is only 0x{audioStream.Length:X8} bytes long.");
                                    hasError = true;
                                    item.Icon = FontAwesomeIcon.TimesCircle;
                                    item.Foreground = Brushes.Red;
                                    item.Spinning = false;
                                    errors.Add(M3L.GetString(M3L.string_interp_invalidAudioPointerOutsideAFC, wwisestream.FileRef.FilePath.Substring(ModBeingDeployed.ModPath.Length + 1), wwisestream.UIndex, wwisestream.ObjectName, audioOffset, afcPath, audioStream.Length));
                                    if (audioStream is FileStream) audioStream.Close();
                                    continue;
                                }

                                if (audioStream.ReadStringASCIINull(4) != @"RIFF")
                                {
                                    Log.Warning($@"Found broken audio: {wwisestream.UIndex} {wwisestream.ObjectName} has broken audio, pointer points to data that does not start with RIFF, which is the start of audio data. Package file: {wwisestream.FileRef.FilePath}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}.");
                                    hasError = true;
                                    item.Icon = FontAwesomeIcon.TimesCircle;
                                    item.Foreground = Brushes.Red;
                                    item.Spinning = false;
                                    errors.Add(M3L.GetString(M3L.string_interp_invalidAudioPointer, Path.GetFileName(wwisestream.FileRef.FilePath), wwisestream.InstancedFullPath));
                                    if (audioStream is FileStream) audioStream.Close();
                                    continue;
                                }

                                //attempt to seek audio length.
                                audioStream.Seek(audioSize + 4, SeekOrigin.Current);

                                //Check if this file is in basegame
                                if (isInOfficialArea)
                                {
                                    //Verify offset is not greater than vanilla size
                                    var vanillaInfo = VanillaDatabaseService.GetVanillaFileInfo(ValidationTarget, afcPath.Substring(ValidationTarget.TargetPath.Length + 1));
                                    if (vanillaInfo == null)
                                    {
                                        Crashes.TrackError(new Exception($@"Vanilla information was null when performing vanilla file check for {afcPath.Substring(ValidationTarget.TargetPath.Length + 1)}"));
                                    }
                                    if (audioOffset >= vanillaInfo[0].size)
                                    {
                                        Log.Warning($@"Found broken audio: {wwisestream.UIndex} {wwisestream.ObjectName} has broken audio, pointer points beyond the end of the AFC file. Package file: {wwisestream.FileRef.FilePath}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}.");

                                        hasError = true;
                                        item.Icon = FontAwesomeIcon.TimesCircle;
                                        item.Foreground = Brushes.Red;
                                        item.Spinning = false;
                                        errors.Add(M3L.GetString(M3L.string_interp_audioStoredInOfficialAFC, wwisestream.FileRef.FilePath, wwisestream.InstancedFullPath));
                                    }
                                }
                                if (audioStream is FileStream) audioStream.Close();
                            }
                            catch (Exception e)
                            {
                                Log.Error($@"Error checking for broken audio: {wwisestream?.UIndex} {wwisestream?.ObjectName}. Package file: {wwisestream?.FileRef?.FilePath?.Substring(ModBeingDeployed.ModPath.Length + 1)}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}. The error was: {e.Message}");
                                e.LogStackTrace();
                                hasError = true;
                                item.Icon = FontAwesomeIcon.TimesCircle;
                                item.Foreground = Brushes.Red;
                                item.Spinning = false;
                                if (audioStream is FileStream) audioStream.Close();
                                errors.Add(M3L.GetString(M3L.string_errorValidatingAudioReference, wwisestream.FileRef.FilePath.Substring(ModBeingDeployed.ModPath.Length + 1), wwisestream.InstancedFullPath, e.Message));
                                continue;
                            }
                        }
                    }
                }
            }


            if (!hasError)
            {
                item.Foreground = Brushes.Green;
                item.Icon = FontAwesomeIcon.CheckCircle;
                item.ItemText = M3L.GetString(M3L.string_noAudioIssuesWereDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.Errors = errors;
                item.ItemText = M3L.GetString(M3L.string_audioIssuesWereDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationFailed);
            }
            item.HasError = hasError;
            cachedAudio.Clear();
        }

        private void CheckTextures(DeploymentChecklistItem item)
        {
            // if (ModBeingDeployed.Game >= MEGame.ME2)
            //{
            bool hasError = false;
            item.HasError = false;
            item.ItemText = M3L.GetString(M3L.string_checkingTexturesInMod);
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
            var allTFCs = referencedFiles.Where(x => Path.GetExtension(x) == @".tfc").ToList();
            int numChecked = 0;
            GameTarget ValidationTarget = mainwindow.InstallationTargets.FirstOrDefault(x => x.Game == ModBeingDeployed.Game);
            var errors = new List<string>();
            foreach (var f in referencedFiles)
            {
                if (_closed) return;
                numChecked++;
                item.ItemText = $@"{M3L.GetString(M3L.string_checkingTexturesInMod)} [{numChecked}/{referencedFiles.Count}]";
                if (f.RepresentsPackageFilePath())
                {
                    Log.Information(@"Checking file for broken textures: " + f);
                    var package = MEPackageHandler.OpenMEPackage(f);
                    var textures = package.Exports.Where(x => x.IsTexture() && !x.IsDefaultObject).ToList();
                    foreach (var texture in textures)
                    {
                        if (_closed) return;

                        if (package.Game > MEGame.ME1)
                        {
                            // CHECK NEVERSTREAM
                            // 1. Has more than six mips.
                            // 2. Has no external mips.
                            Texture2D tex = new Texture2D(texture);

                            var topMip = tex.GetTopMip();
                            if (topMip.storageType == StorageTypes.pccUnc)
                            {
                                // It's an internally stored texture
                                if (!tex.NeverStream && tex.Mips.Count(x => x.storageType != StorageTypes.empty) > 6)
                                {
                                    // NEVERSTREAM SHOULD HAVE BEEN SET.
                                    Log.Error(@"Found texture missing 'NeverStream' attribute " + texture.InstancedFullPath);
                                    hasError = true;
                                    item.Icon = FontAwesomeIcon.TimesCircle;
                                    item.Foreground = Brushes.Red;
                                    item.Spinning = false;
                                    item.DeploymentBlocking = true;
                                    errors.Add($"{texture.FileRef.FilePath} texture {texture.UIndex} {texture.InstancedFullPath} is not externally stored, has more than 6 mips, but does not have the NeverStream flag. If LODs are raised this package will crash the game. Set the NeverStream flag to true to correct this issue, or use an external TFC (preferred). Using an external TFC for textures improves game performance.");
                                }
                            }

                            var cache = texture.GetProperty<NameProperty>(@"TextureFileCacheName");
                            if (cache != null)
                            {
                                if (!VanillaDatabaseService.IsBasegameTFCName(cache.Value, ModBeingDeployed.Game))
                                {
                                    //var mips = Texture2D.GetTexture2DMipInfos(texture, cache.Value);
                                    try
                                    {
                                        tex.GetImageBytesForMip(tex.GetTopMip(), ValidationTarget.Game, false, ValidationTarget.TargetPath, allTFCs); //use active target
                                    }
                                    catch (Exception e)
                                    {
                                        Log.Warning(@"Found broken texture: " + texture.InstancedFullPath);
                                        hasError = true;
                                        item.Icon = FontAwesomeIcon.TimesCircle;
                                        item.Foreground = Brushes.Red;
                                        item.Spinning = false;
                                        errors.Add(M3L.GetString(M3L.string_interp_couldNotLoadTextureData, texture.FileRef.FilePath, texture.InstancedFullPath, e.Message));
                                    }
                                }

                                if (cache.Value.Name.Contains(@"CustTextures"))
                                {
                                    // ME3Explorer 3.0 or below Texplorer
                                    hasError = true;
                                    item.Icon = FontAwesomeIcon.TimesCircle;
                                    item.Foreground = Brushes.Red;
                                    item.Spinning = false;
                                    errors.Add(M3L.GetString(M3L.string_interp_error_foundCustTexturesTFCRef, texture.FileRef.FilePath, texture.InstancedFullPath, cache.Value.Name));
                                }
                                else if (cache.Value.Name.Contains(@"TexturesMEM"))
                                {
                                    // Textures replaced by MEM. This is not allowed in mods as it'll immediately break
                                    hasError = true;
                                    item.Icon = FontAwesomeIcon.TimesCircle;
                                    item.Foreground = Brushes.Red;
                                    item.Spinning = false;
                                    item.DeploymentBlocking = true;
                                    DeployButtonText = M3L.GetString(M3L.string_deploymentBlocked);
                                    errors.Add(M3L.GetString(M3L.string_interp_error_foundTexturesMEMTFCRef, texture.FileRef.FilePath, texture.InstancedFullPath, cache.Value.Name));

                                }
                            }
                        }
                        else
                        {
                            Texture2D tex = new Texture2D(texture);
                            var cachename = tex.GetTopMip().TextureCacheName;
                            if (cachename != null)
                            {
                                foreach (var mip in tex.Mips)
                                {
                                    try
                                    {
                                        tex.GetImageBytesForMip(mip, ValidationTarget.Game, false, ValidationTarget.TargetPath);
                                    }
                                    catch (Exception e)
                                    {
                                        Log.Warning(@"Found broken texture: " + texture.InstancedFullPath);
                                        hasError = true;
                                        item.Icon = FontAwesomeIcon.TimesCircle;
                                        item.Foreground = Brushes.Red;
                                        item.Spinning = false;
                                        errors.Add(M3L.GetString(M3L.string_interp_couldNotLoadTextureData, texture.FileRef.FilePath, texture.InstancedFullPath, e.Message));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (!hasError)
            {
                item.Foreground = Brushes.Green;
                item.Icon = FontAwesomeIcon.CheckCircle;
                item.ItemText = M3L.GetString(M3L.string_noBrokenTexturesWereFound);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.Errors = errors;
                item.ItemText = M3L.GetString(M3L.string_textureIssuesWereDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationFailed);
            }
            item.HasError = hasError;
        }

        public ICommand DeployCommand { get; set; }
        public ICommand CloseCommand { get; set; }
        private void LoadCommands()
        {
            DeployCommand = new GenericCommand(StartDeployment, CanDeploy);
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
        }

        private void ClosePanel()
        {
            _closed = true;
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanClose() => !DeploymentInProgress;

        /// <summary>
        /// File extensnions that will be stored uncompressed in archive as they already have well compressed data and may be of a large size
        /// (which increases the solid block size)
        /// </summary>
        private static string[] NoCompressExtensions = new[] { @".tfc", @".bik" };
        private void StartDeployment()
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = $@"{M3L.GetString(M3L.string_7zipArchiveFile)}|*.7z",
                FileName = Utilities.SanitizePath($@"{ModBeingDeployed.ModName}_{ModBeingDeployed.ModVersionString}".Replace(@" ", ""), true)
            };
            var result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModDeploymentThread");
                nbw.DoWork += Deployment_BackgroundThread;
                nbw.WorkerReportsProgress = true;
                nbw.ProgressChanged += (a, b) =>
                {
                    if (b.UserState is double d)
                    {
                        TaskbarHelper.SetProgress(d);
                    }
                    else if (b.UserState is TaskbarProgressBarState tbs)
                    {
                        TaskbarHelper.SetProgressState(tbs);
                    }
                };
                nbw.RunWorkerCompleted += (a, b) =>
                {
                    TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);

                    if (b.Error != null)
                    {
                        Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                    }
                    else
                    {
                        Analytics.TrackEvent(@"Deployed mod", new Dictionary<string, string>()
                        {
                            { @"Mod name" , $@"{ModBeingDeployed.ModName} {ModBeingDeployed.ParsedModVersion}"}
                        });
                    }
                    DeploymentInProgress = false;
                    CommandManager.InvalidateRequerySuggested();
                };
                TaskbarHelper.SetProgress(0);
                TaskbarHelper.SetProgressState(TaskbarProgressBarState.Normal);
                nbw.RunWorkerAsync(d.FileName);
                DeploymentInProgress = true;
            }
        }

        private void Deployment_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            NamedBackgroundWorker worker = (NamedBackgroundWorker)sender;
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences(true);
            string archivePath = e.Argument as string;
            //Key is in-archive path, value is on disk path
            var archiveMapping = new Dictionary<string, string>();
            SortedSet<string> directories = new SortedSet<string>();

            // Add list of directories first so they appear first in the list of archive entries
            // This is required for proper archive format
            foreach (var file in referencedFiles)
            {
                var path = Path.Combine(ModBeingDeployed.ModPath, file);
                var directory = Directory.GetParent(path).FullName;
                if (directory.Length <= ModBeingDeployed.ModPath.Length) continue; //root file or directory.
                directory = directory.Substring(ModBeingDeployed.ModPath.Length + 1);

                //nested folders with no folders
                var relativeFolders = directory.Split('\\');
                string buildingFolderList = "";
                foreach (var relativeFolder in relativeFolders)
                {
                    if (buildingFolderList != "")
                    {
                        buildingFolderList += @"\";
                    }
                    buildingFolderList += relativeFolder;
                    if (directories.Add(buildingFolderList))
                    {
                        archiveMapping[buildingFolderList] = null;
                    }
                }

            }

            // Add the files
            archiveMapping.AddRange(referencedFiles.ToDictionary(x => x, x => Path.Combine(ModBeingDeployed.ModPath, x)));

            // setup the compressor for pass 1
            var compressor = new SevenZip.SevenZipCompressor();
            compressor.CustomParameters.Add(@"s", @"on");
            if (!MultithreadedCompression)
            {
                // Single thread
                compressor.CustomParameters.Add(@"mt", @"off");
            }
            else
            {
                // Multi thread
                var foldersize = Utilities.GetSizeOfDirectory(ModBeingDeployed.ModPath);
                if (foldersize > FileSize.GibiByte * 1.25)
                {
                    //cap threads to prevent huge memory usage when compressing big mods
                    var cores = Environment.ProcessorCount;
                    cores = Math.Min(cores, 5);
                    compressor.CustomParameters.Add(@"mt", cores.ToString());
                }
            }

            compressor.CustomParameters.Add(@"yx", @"9");
            compressor.CustomParameters.Add(@"d", @"28"); //Dictionary size 2^28 (256MB)
            string currentDeploymentStep = M3L.GetString(M3L.string_mod);
            compressor.Progressing += (a, b) =>
            {
                //Debug.WriteLine(b.AmountCompleted + "/" + b.TotalAmount);
                ProgressMax = b.TotalAmount;
                ProgressValue = b.AmountCompleted;
                var now = DateTime.Now;
                if ((now - lastPercentUpdateTime).Milliseconds > ModInstaller.PERCENT_REFRESH_COOLDOWN)
                {
                    //Don't update UI too often. Once per second is enough.
                    var progValue = ProgressValue * 100.0 / ProgressMax;
                    string percent = progValue.ToString(@"0.00");
                    worker.ReportProgress(0, progValue / 100.0);

                    OperationText = $@"[{currentDeploymentStep}] {M3L.GetString(M3L.string_deploymentInProgress)} {percent}%";
                    lastPercentUpdateTime = now;
                }
                //Debug.WriteLine(ProgressValue + "/" + ProgressMax);
            };
            compressor.FileCompressionStarted += (a, b) => { Debug.WriteLine(b.FileName); };

            // Pass 1: Compressed items and empty folders
            // Includes package files and other basic file types
            // Does not include AFC, TFC, or .BIK
            // Does not include moddesc.ini
            // Does not include any referenced image files under M3Images
            currentDeploymentStep = M3L.GetString(M3L.string_compressedModItems);

            //x.Value == null means it's a folder
            var compressItems = archiveMapping.Where(ShouldBeCompressed).ToDictionary(p => p.Key, p => p.Value);
            compressor.CompressFileDictionary(compressItems, archivePath);


            // Pass 2: Uncompressed items that are not moddesc.ini
            compressor.CustomParameters.Clear(); //remove custom params as it seems to force LZMA
            compressor.CompressionMode = CompressionMode.Append; //Append to 
            compressor.CompressionLevel = CompressionLevel.None;

            currentDeploymentStep = M3L.GetString(M3L.string_uncompressedModItems);
            var nocompressItems = archiveMapping.Where(x => !ShouldBeCompressed(x)).ToDictionary(p => p.Key, p => p.Value);

            compressor.CompressFileDictionary(nocompressItems, archivePath);

            // Pass 3: Moddesc.ini
            // Appends at end of archive
            //currentDeploymentStep = @"moddesc.ini";
            //compressor.CompressFiles(archivePath, new string[]
            //{
            //    Path.Combine(ModBeingDeployed.ModPath, @"moddesc.ini")
            //});
            OperationText = M3L.GetString(M3L.string_deploymentSucceeded);
            Utilities.HighlightInExplorer(archivePath);
        }

        /// <summary>
        /// Determines if a file should be compressed into the archive, or stored uncompressed
        /// </summary>
        /// <param name="fileMapping">Mapping of the in-archive path to the source file. If the value is null, it means it's a folder.</param>
        /// <returns></returns>
        private bool ShouldBeCompressed(KeyValuePair<string, string> fileMapping)
        {
            if (fileMapping.Value == null) return true; //This can't be compressed but it should be done in the compression pass
            if (NoCompressExtensions.Contains(Path.GetExtension(fileMapping.Value))) return false; //Do not compress these extensions
            var modRelPath = fileMapping.Value.Substring(ModBeingDeployed.ModPath.Length + 1);
            if (modRelPath.StartsWith(@"M3Images", StringComparison.InvariantCultureIgnoreCase))
                return false; // Referenced image file should not be compressed.
            if (modRelPath == @"moddesc.ini")
                return false; // moddesc.ini should not be compressed.
            return true;
        }

        private bool CanDeploy()
        {
            return PrecheckCompleted && !DeploymentInProgress &&
                   !DeploymentChecklistItems.Any(x => x.DeploymentBlocking);
        }

        public ObservableCollectionExtended<DeploymentChecklistItem> DeploymentChecklistItems { get; } = new ObservableCollectionExtended<DeploymentChecklistItem>();
        public bool PrecheckCompleted { get; private set; }
        public bool DeploymentInProgress { get; private set; }
        public ulong ProgressMax { get; set; } = 100;
        public ulong ProgressValue { get; set; } = 0;
        public string OperationText { get; set; } = M3L.GetString(M3L.string_checkingModBeforeDeployment);
        //M3L.GetString(M3L.string_verifyAboveItemsBeforeDeployment);

        private DateTime lastPercentUpdateTime;
        private bool _closed;
        private GameTarget ValidationTarget;

        public class DeploymentChecklistItem : INotifyPropertyChanged
        {
            public string ItemText { get; set; }
            public SolidColorBrush Foreground { get; set; }
            public FontAwesomeIcon Icon { get; set; }
            public bool Spinning { get; set; }
            public bool DeploymentBlocking { get; set; }

            public Action<DeploymentChecklistItem> ValidationFunction;
            public objects.mod.Mod ModToValidateAgainst;
            internal string ErrorsMessage;
            internal string ErrorsTitle;

            public event PropertyChangedEventHandler PropertyChanged;

            public string ToolTip { get; set; }
            public bool HasError { get; internal set; }
            public List<string> Errors { get; internal set; } = new List<string>();

            //public DeploymentChecklistItem(string initialDisplayText, Mod m, Action<DeploymentChecklistItem> validationFunction)
            //{
            //    this.ItemText = initialDisplayText;
            //    this.ModToValidateAgainst = m;
            //    this.ValidationFunction = validationFunction;
            //    Icon = FontAwesomeIcon.Spinner;
            //    Spinning = true;
            //    Foreground = Brushes.Gray;
            //    ToolTip = "Validation in progress...";
            //}

            public DeploymentChecklistItem()
            {
                Icon = FontAwesomeIcon.Spinner;
                Spinning = true;
                Foreground = Application.Current.FindResource(AdonisUI.Brushes.DisabledAccentForegroundBrush) as SolidColorBrush;
                ToolTip = M3L.GetString(M3L.string_validationInProgress);
            }

            public void ExecuteValidationFunction()
            {
                Foreground = Application.Current.FindResource(AdonisUI.Brushes.HyperlinkBrush) as SolidColorBrush;
                ValidationFunction?.Invoke(this);
                //Debug.WriteLine("Invoke finished");
                Spinning = false;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (sender is Hyperlink hl && hl.DataContext is DeploymentChecklistItem dcli)
            {
                ListDialog ld = new ListDialog(dcli.Errors, dcli.ErrorsTitle, dcli.ErrorsMessage, Window.GetWindow(hl));
                ld.ShowDialog();
            }
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                ClosePanel();
            }
        }

        public override void OnPanelVisible()
        {
            lastPercentUpdateTime = DateTime.Now;
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"DeploymentValidation");
            nbw.DoWork += (a, b) =>
            {
                ProgressIndeterminate = true;
                foreach (var checkItem in DeploymentChecklistItems)
                {
                    checkItem.ExecuteValidationFunction();
                }
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                MELoadedFiles.InvalidateCaches();
                TaskbarHelper.SetProgress(0);
                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                PrecheckCompleted = true;
                ProgressIndeterminate = false;
                if (DeploymentChecklistItems.Any(x => x.DeploymentBlocking))
                {
                    OperationText = M3L.GetString(M3L.string_deploymentBlockedUntilAboveItemsAreFixed);
                    DeployButtonText = M3L.GetString(M3L.string_deploymentBlocked);
                }
                else
                {
                    DeployButtonText = M3L.GetString(M3L.string_deploy);
                    OperationText = M3L.GetString(M3L.string_verifyAboveItemsBeforeDeployment);
                }
                CommandManager.InvalidateRequerySuggested();
            };
            TaskbarHelper.SetProgressState(TaskbarProgressBarState.Indeterminate);

            nbw.RunWorkerAsync();
        }

        public bool ProgressIndeterminate { get; set; }
    }
}
