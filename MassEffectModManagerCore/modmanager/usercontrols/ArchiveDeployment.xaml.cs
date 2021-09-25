using FontAwesome.WPF;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.modmanager.windows;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Taskbar;
using PropertyChanged;
using Serilog;
using SevenZip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Brushes = System.Windows.Media.Brushes;
using ExportEntry = LegendaryExplorerCore.Packages.ExportEntry;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ArchiveDeployment.xaml
    /// </summary>
    public partial class ArchiveDeployment : MMBusyPanelBase
    {
        public string Header { get; set; } = M3L.GetString(M3L.string_prepareModForDistribution);
        public bool MultithreadedCompression { get; set; } = true;
        public string DeployButtonText { get; set; } = M3L.GetString(M3L.string_pleaseWait); //Initial value
        public ObservableCollectionExtended<EncompassingModDeploymentCheck> ModsInDeployment { get; } = new ObservableCollectionExtended<EncompassingModDeploymentCheck>();

        // Mod that will be first added to the deployment when the UI is loaded
        private Mod initialMod;

        public ArchiveDeployment(Mod mod)
        {
            Log.Information($@"Initiating deployment for mod {mod.ModName} {mod.ModVersionString}");
            Analytics.TrackEvent(@"Started deployment panel for mod", new Dictionary<string, string>()
            {
                { @"Mod name" , $@"{mod.ModName} {mod.ParsedModVersion}"}
            });
            initialMod = mod;
            LoadCommands();
            InitializeComponent();
        }

        public ConcurrentQueue<EncompassingModDeploymentCheck> PendingChecks { get; } = new ConcurrentQueue<EncompassingModDeploymentCheck>();

        /// <summary>
        ///  Class that checks a single mod for issues
        /// </summary>
        [AddINotifyPropertyChangedInterface]
        public class EncompassingModDeploymentCheck
        {
            public ObservableCollectionExtended<DeploymentChecklistItem> DeploymentChecklistItems { get; } = new ObservableCollectionExtended<DeploymentChecklistItem>();
            public DeploymentValidationTarget DepValidationTarget { get; set; }
            private GameTarget internalValidationTarget { get; set; }
            public Mod ModBeingDeployed { get; }
            public ICommand RerunChecksCommand { get; }
            public void RunChecks()
            {
                CanReRun = false;
                foreach (var checkItem in DeploymentChecklistItems)
                {
                    checkItem.Reset();
                }

                foreach (var checkItem in DeploymentChecklistItems)
                {
                    if (CheckCancelled) continue;
                    checkItem.ExecuteValidationFunction();
                }

                CanReRun = CanRerunCheck();
            }
            private bool CanRerunCheck()
            {
                return DeploymentChecklistItems.All(x => x.CheckDone) && DeploymentChecklistItems.Any(x => x.HasMessage);
            }

            public bool CheckCancelled { get; set; }

            public bool CanReRun { get; set; }
            public EncompassingModDeploymentCheck(Mod mod, DeploymentValidationTarget dvt)
            {
                ModBeingDeployed = mod;
                DepValidationTarget = dvt;
                internalValidationTarget = dvt.SelectedTarget;

                // Commands
                RerunChecksCommand = new GenericCommand(RunChecksWrapper, CanRerunCheck);

                string versionString = mod.ParsedModVersion != null ? mod.ParsedModVersion.ToString(Utilities.GetDisplayableVersionFieldCount(mod.ParsedModVersion)) : mod.ModVersionString;
                string versionFormat = mod.ModDescTargetVersion < 6 ? @"X.X" : @"X.X[.X[.X]]";
                string checklistItemText = mod.ParsedModVersion != null ? M3L.GetString(M3L.string_verifyModVersion) : M3L.GetString(M3L.string_recommendedVersionFormatNotFollowed, versionFormat);
                DeploymentChecklistItems.Add(new DeploymentChecklistItem() { ItemText = $@"{checklistItemText}: {versionString}", ValidationFunction = ManualValidation });
                DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                {
                    ItemText = $@"{M3L.GetString(M3L.string_verifyURLIsCorrect)} {mod.ModWebsite}",
                    ValidationFunction = URLValidation,
                    DialogMessage = M3L.GetString(M3L.string_validation_badUrl),
                    DialogTitle = M3L.GetString(M3L.string_modUrlErrorsWereFound)
                });
                DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                {
                    ItemText = M3L.GetString(M3L.string_verifyModDescription),
                    ValidationFunction = ManualValidation

                });
                if (mod.Game == MEGame.ME3) // SFAR Ceeck (ME3 only)
                {
                    DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                    {
                        ItemText = M3L.GetString(M3L.string_sfarFilesCheck),
                        ModToValidateAgainst = mod,
                        DialogMessage = M3L.GetString(M3L.string_invalidSfarSize),
                        DialogTitle = M3L.GetString(M3L.string_wrongSfarSizesFound),
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
                            DialogMessage = M3L.GetString(M3L.string_languageSupportCheckDetectedFollowingIssues),
                            DialogTitle = M3L.GetString(M3L.string_languageSupportIssuesDetectedInMod)
                        });
                    }
                }

                DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                {
                    ItemText = M3L.GetString(M3L.string_referencesCheck),
                    ModToValidateAgainst = mod,
                    DialogMessage = M3L.GetString(M3L.string_interp_dialog_invalidReferencesFound),
                    DialogTitle = M3L.GetString(M3L.string_invalidNameAndObjectReferences),
                    ValidationFunction = CheckReferences
                });

                if (mod.Game.IsGame2() || mod.Game.IsGame3())
                {
                    DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                    {
                        ItemText = M3L.GetString(M3L.string_audioCheck),
                        ModToValidateAgainst = mod,
                        ValidationFunction = CheckAFCs,
                        DialogMessage = M3L.GetString(M3L.string_audioCheckDetectedErrors),
                        DialogTitle = M3L.GetString(M3L.string_audioIssuesDetectedInMod)
                    });
                }

                DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                {
                    ItemText = M3L.GetString(M3L.string_texturesCheck),
                    ModToValidateAgainst = mod,
                    DialogMessage = M3L.GetString(M3L.string_texturesCheckDetectedErrors),
                    DialogTitle = M3L.GetString(M3L.string_textureErrorsInMod),
                    ValidationFunction = CheckTextures
                });

                DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                {
                    ItemText = M3L.GetString(M3L.string_miscellaneousChecks),
                    ModToValidateAgainst = mod,
                    DialogMessage = M3L.GetString(M3L.string_atLeastOneMiscellaneousCheckFailed),
                    DialogTitle = M3L.GetString(M3L.string_detectedMiscellaneousIssues),
                    ValidationFunction = CheckModForMiscellaneousIssues
                });
            }

            private void RunChecksWrapper()
            {
                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModChecksThread");
                nbw.DoWork += (sender, args) => RunChecks();
                nbw.RunWorkerAsync();
            }

            #region CHECK FUNCTIONS

            /// <summary>
            /// Checks for ALOT markers
            /// </summary>
            /// <param name="item"></param>
            private void CheckModForMiscellaneousIssues(DeploymentChecklistItem item)
            {
                item.ItemText = M3L.GetString(M3L.string_checkingForMiscellaneousIssues);
                var referencedFiles = ModBeingDeployed.GetAllRelativeReferences(false);

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
                var packageFiles = Utilities.GetPackagesInDirectory(item.ModToValidateAgainst.ModPath, true);
                foreach (var p in packageFiles)
                {
                    if (Utilities.HasALOTMarker(p))
                    {
                        item.AddBlockingError(M3L.GetString(M3L.string_interp_error_textureTaggedFileFound, p));
                    }

                    var package = MEPackageHandler.QuickOpenMEPackage(p);
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

                        if (package.Game != ModBeingDeployed.Game)
                        {
                            item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningPackageForOtherGameFound, package.FilePath.Substring(ModBeingDeployed.ModPath.Length + 1), package.Game, ModBeingDeployed.Game));
                        }
                    }
                }

                //Check moddesc.ini for things that shouldn't be present - unofficial
                if (ModBeingDeployed.IsUnofficial)
                {
                    item.AddBlockingError(M3L.GetString(M3L.string_error_foundUnofficialDescriptor));
                }

                //Check moddesc.ini for things that shouldn't be present - importedby
                if (ModBeingDeployed.ImportedByBuild > 0)
                {
                    item.AddBlockingError(M3L.GetString(M3L.string_error_foundImportedByDesriptor));
                }

                // Check mod name length
                if (ModBeingDeployed.ModName.Length > 40)
                {
                    item.AddInfoWarning(M3L.GetString(M3L.string_interp_infoModNameTooLong, ModBeingDeployed.ModName, ModBeingDeployed.ModName.Length));
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

            /// <summary>
            /// Validates the URL of the mod
            /// </summary>
            /// <param name="obj"></param>
            private void URLValidation(DeploymentChecklistItem obj)
            {
                bool OK = Uri.TryCreate(ModBeingDeployed.ModWebsite, UriKind.Absolute, out var uriResult)
                          && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (!OK)
                {
                    string url = ModBeingDeployed.ModWebsite ?? @"null";
                    obj.AddBlockingError(M3L.GetString(M3L.string_interp_urlIsNotValid, url));
                    obj.ItemText = M3L.GetString(M3L.string_emptyOrInvalidModUrl);
                    obj.ToolTip = M3L.GetString(M3L.string_validationFailed);
                }
                else
                {
                    if (ModBeingDeployed.ModWebsite == Mod.DefaultWebsite)
                    {
                        obj.AddSignificantIssue(M3L.GetString(M3L.string_noModWebsiteSet));
                        obj.ItemText = M3L.GetString(M3L.string_moddescMissingModsite);
                    }
                    else
                    {
                        obj.ItemText = M3L.GetString(M3L.string_interp_modURLOK, ModBeingDeployed.ModWebsite);
                        obj.ToolTip = M3L.GetString(M3L.string_validationOK);
                    }
                }
            }

            /// <summary>
            /// Checks localizations and proper DLC mod setup for TLK
            /// </summary>
            /// <param name="obj"></param>
            private void CheckLocalizations(DeploymentChecklistItem obj)
            {
                var customDLCJob = ModBeingDeployed.GetJob(ModJob.JobHeader.CUSTOMDLC);
                var customDLCFolders = customDLCJob.CustomDLCFolderMapping.Keys.ToList();
                customDLCFolders.AddRange(customDLCJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC).Select(x => x.AlternateDLCFolder));
                var languages = ModBeingDeployed.Game.IsGame2() ? StarterKitGeneratorWindow.me2languages : StarterKitGeneratorWindow.me3languages;
                obj.ItemText = M3L.GetString(M3L.string_languageCheckInProgress);
                foreach (var customDLC in customDLCFolders)
                {
                    if (CheckCancelled) return;
                    if (ModBeingDeployed.Game.IsGame2() || ModBeingDeployed.Game.IsGame3())
                    {
                        var modCookedDir = Path.Combine(ModBeingDeployed.ModPath, customDLC, ModBeingDeployed.Game.CookedDirName());
                        var mountFile = Path.Combine(modCookedDir, @"mount.dlc");
                        if (!File.Exists(mountFile))
                        {
                            obj.AddBlockingError(M3L.GetString(M3L.string_interp_noMountDlcFile, customDLC));
                            continue;
                        }

                        var mount = new MountFile(mountFile);

                        int moduleNum = -1;
                        if (ModBeingDeployed.Game.IsGame2())
                        {
                            // Look up the module number
                            var bioengine = Path.Combine(modCookedDir, @"BioEngine.ini");
                            if (!File.Exists(bioengine))
                            {
                                obj.AddBlockingError(M3L.GetString(M3L.string_interp_me2NoBioEngineFile, customDLC));
                                continue;
                            }
                            else
                            {
                                var ini = DuplicatingIni.LoadIni(Path.Combine(bioengine));
                                if (!int.TryParse(ini[@"Engine.DLCModules"][customDLC]?.Value, out moduleNum) || moduleNum < 1)
                                {
                                    obj.AddBlockingError(M3L.GetString(M3L.string_interp_me2MissingInvalidModuleNum, customDLC));
                                    continue;
                                }
                            }
                        }

                        var tlkBasePath = ModBeingDeployed.Game.IsGame2() ? $@"DLC_{moduleNum}" : customDLC;
                        Dictionary<string, List<ME1TalkFile.TLKStringRef>> tlkMappings = new Dictionary<string, List<ME1TalkFile.TLKStringRef>>();
                        foreach (var language in languages)
                        {
                            if (CheckCancelled) return;
                            var tlkLangPath = Path.Combine(modCookedDir, tlkBasePath + @"_" + language.filecode + @".tlk");
                            if (File.Exists(tlkLangPath))
                            {
                                //inspect
                                TalkFile tf = new TalkFile();
                                tf.LoadTlkData(tlkLangPath);
                                tlkMappings[language.filecode] = tf.StringRefs;

                                //Check string order
                                var malestringRefsInRightOrder = tf.StringRefs.Take(tf.Header.MaleEntryCount).IsAscending((x, y) => x.CalculatedID.CompareTo(y.CalculatedID)); //male strings
                                var femalestringRefsInRightOrder = tf.StringRefs.Skip(tf.Header.MaleEntryCount).Take(tf.Header.FemaleEntryCount).IsAscending((x, y) => x.CalculatedID.CompareTo(y.CalculatedID)); //male strings
                                string gender = M3L.GetString(M3L.string_male);
                                if (!malestringRefsInRightOrder)
                                {
                                    //Some TLK strings will not work
                                    obj.AddSignificantIssue(M3L.GetString(M3L.string_interp_error_outOfOrderTLK, gender, language.filecode));
                                }

                                if (!femalestringRefsInRightOrder)
                                {
                                    gender = M3L.GetString(M3L.string_female);
                                    //Some TLK strings will not work
                                    obj.AddSignificantIssue(M3L.GetString(M3L.string_interp_error_outOfOrderTLK, gender, language.filecode));
                                }

                                // Check to make sure TLK contains the mount file TLK ID
                                var referencedStr = tf.findDataById(mount.TLKID);
                                if (referencedStr == null || referencedStr == @"No Data")
                                {
                                    // TLK STRING REF NOT FOUND
                                    obj.AddBlockingError(M3L.GetString(M3L.string_interp_missingReferencedTlkStrInMod, customDLC, Path.GetFileName(tlkLangPath), mount.TLKID));
                                    continue;
                                }
                            }
                            else
                            {
                                obj.AddSignificantIssue(M3L.GetString(M3L.string_interp_customDLCMissingLocalizedTLK, customDLC, language.filecode));
                            }
                        }

                        if (tlkMappings.Any())
                        {
                            double numLoops = Math.Pow(tlkMappings.Count, 2);
                            int numDone = 0;
                            foreach (var mapping1 in tlkMappings)
                            {
                                foreach (var mapping2 in tlkMappings)
                                {
                                    var percent = (int)((numDone * 100.0) / numLoops);
                                    obj.ItemText = $@"{M3L.GetString(M3L.string_languageCheckInProgress)} {percent}%";
                                    numDone++;

                                    if (mapping1.Equals(mapping2))
                                    {
                                        continue;
                                    }

                                    var differences = mapping1.Value.Select(x => x.StringID).Except(mapping2.Value.Select(x => x.StringID));
                                    foreach (var difference in differences)
                                    {
                                        var str = mapping1.Value.FirstOrDefault(x => x.StringID == difference)?.Data ?? M3L.GetString(M3L.string_errorFindingString);
                                        obj.AddSignificantIssue(M3L.GetString(M3L.string_interp_tlkDifference, difference.ToString(), mapping1.Key, mapping2.Key, str));
                                    }
                                }
                            }
                        }
                        else if (tlkMappings.Count == 0)
                        {
                            obj.AddBlockingError(M3L.GetString(M3L.string_interp_dlcModHasNoTlkFiles, customDLC));
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
                                obj.AddBlockingError(M3L.GetString(M3L.string_interp_dlcModMissingTlkInAutoloadIni, customDLC));
                            }
                            else
                            {
                                // Check TLK exists.
                                var tlkExportObjName = tlkFile.Split('.').Last();
                                tlkFile = tlkFile.Substring(0, tlkFile.IndexOf(@".")); //They end with _tlk
                                var tlkExtension = ModBeingDeployed.Game == MEGame.ME1 ? @".upk" : @".pcc";
                                var tlkPackagePath = Directory.GetFiles(Path.Combine(ModBeingDeployed.ModPath, customDLC), $@"{tlkFile}{tlkExtension}", SearchOption.AllDirectories).FirstOrDefault();
                                if (tlkPackagePath == null)
                                {
                                    obj.AddBlockingError(M3L.GetString(M3L.string_interp_dlcModMissingAutoLoadTlkFile, customDLC, tlkFile, tlkExtension));
                                }
                                else
                                {
                                    // Open and inspect TLK package.
                                    var tlkPackage = MEPackageHandler.OpenMEPackage(tlkPackagePath);
                                    var tfExp = tlkPackage.Exports.FirstOrDefault(x => x.ObjectName == tlkExportObjName && x.ClassName == @"BioTlkFile");
                                    if (tfExp == null)
                                    {
                                        obj.AddBlockingError(M3L.GetString(M3L.string_interp_dlcModTlkPackageHasNoUsableTlk, customDLC));
                                    }

                                    ME1TalkFile tf = new ME1TalkFile(tfExp);
                                    var str = tf.findDataById(tlkid);
                                    if (str == null || str == @"No Data")
                                    {
                                        // INVALID
                                        obj.AddBlockingError(M3L.GetString(M3L.string_interp_dlcModTlkPackageMissingStringId, customDLC, tlkid));
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
                            obj.AddBlockingError(M3L.GetString(M3L.string_interp_dlcModAutoLoadMissingNameStrRef, customDLC));
                        }
                    }
                }

                if (obj.HasAnyMessages())
                {
                    obj.ItemText = M3L.GetString(M3L.string_languageCheckDetectedIssues);
                    obj.ToolTip = M3L.GetString(M3L.string_validationFailed);
                }
                else
                {
                    obj.ItemText = M3L.GetString(M3L.string_noLanguageIssuesDetected);
                    obj.ToolTip = M3L.GetString(M3L.string_validationOK);
                }
            }

            /// <summary>
            /// Checks SFAR sizes
            /// </summary>
            /// <param name="item"></param>
            private void CheckModSFARs(DeploymentChecklistItem item)
            {
                item.ItemText = M3L.GetString(M3L.string_checkingSFARFilesSizes);
                var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
                bool hasSFARs = false;
                foreach (var f in referencedFiles)
                {
                    if (CheckCancelled) return;
                    if (Path.GetExtension(f) == @".sfar")
                    {
                        hasSFARs = true;
                        if (new FileInfo(f).Length != 32)
                        {
                            {
                                item.AddBlockingError(f.Substring(ModBeingDeployed.ModPath.Length + 1));
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

            /// <summary>
            /// Manually validated items
            /// </summary>
            /// <param name="item"></param>
            private void ManualValidation(DeploymentChecklistItem item)
            {
                item.ToolTip = M3L.GetString(M3L.string_thisItemMustBeManuallyCheckedByYou);
            }

            /// <summary>
            /// Checks for broken audio
            /// </summary>
            /// <param name="item"></param>
            private void CheckAFCs(DeploymentChecklistItem item)
            {
                item.ItemText = M3L.GetString(M3L.string_checkingAudioReferencesInMod);
                var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
                int numChecked = 0;

                Predicate<string> predicate = s => s.ToLowerInvariant().EndsWith(@".afc", true, null);
                List<string> gameFiles = M3Directories.EnumerateGameFiles(internalValidationTarget, predicate);

                Dictionary<string, MemoryStream> cachedAudio = new Dictionary<string, MemoryStream>();

                foreach (var f in referencedFiles)
                {
                    if (CheckCancelled) return;
                    numChecked++;
                    item.ItemText = $@"{M3L.GetString(M3L.string_checkingAudioReferencesInMod)} [{numChecked}/{referencedFiles.Count}]";
                    if (f.RepresentsPackageFilePath())
                    {
                        var relativePath = f.Substring(ModBeingDeployed.ModPath.Length + 1);
                        Log.Information(@"Checking file for audio issues: " + f);
                        var package = MEPackageHandler.OpenMEPackage(f);
                        var wwiseStreams = package.Exports.Where(x => x.ClassName == @"WwiseStream" && !x.IsDefaultObject).ToList();
                        foreach (var wwisestream in wwiseStreams)
                        {
                            if (CheckCancelled) return;
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
                                        isInOfficialArea = M3Directories.IsInBasegame(afcPath, internalValidationTarget) || M3Directories.IsInOfficialDLC(afcPath, internalValidationTarget);
                                    }
                                    else if (cachedAudio.TryGetValue(afcNameProp.Value.Name, out var cachedAudioStream))
                                    {
                                        audioStream = cachedAudioStream;
                                        //isInOfficialArea = true; //cached from vanilla SFAR
                                    }
                                    else if (MEDirectories.OfficialDLC(internalValidationTarget.Game).Any(x => afcNameProp.Value.Name.StartsWith(x)))
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
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_couldNotFindReferencedAFC, relativePath, wwisestream.InstancedFullPath, afcNameProp.ToString()));
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
                                        Log.Warning($@"Found broken audio: {wwisestream.UIndex} {wwisestream.ObjectName} has broken audio, pointer points inside of AFC, but the size of it extends beyond the end of the AFC. Package file: {relativePath}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}. The AFC is only 0x{audioStream.Length:X8} bytes long.");
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_invalidAudioPointerOutsideAFC, relativePath, wwisestream.UIndex, wwisestream.ObjectName, audioOffset, afcPath, audioStream.Length));
                                        if (audioStream is FileStream) audioStream.Close();
                                        continue;
                                    }

                                    if (audioStream.ReadStringASCIINull(4) != @"RIFF")
                                    {
                                        Log.Warning($@"Found broken audio: {wwisestream.UIndex} {wwisestream.ObjectName} has broken audio, pointer points to data that does not start with RIFF, which is the start of audio data. Package file: {relativePath}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}.");
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_invalidAudioPointer, relativePath, wwisestream.InstancedFullPath));
                                        if (audioStream is FileStream) audioStream.Close();
                                        continue;
                                    }

                                    //attempt to seek audio length.
                                    audioStream.Seek(audioSize + 4, SeekOrigin.Current);

                                    //Check if this file is in basegame
                                    if (isInOfficialArea)
                                    {
                                        //Verify offset is not greater than vanilla size
                                        var vanillaInfo = VanillaDatabaseService.GetVanillaFileInfo(internalValidationTarget, afcPath.Substring(internalValidationTarget.TargetPath.Length + 1));
                                        if (vanillaInfo == null)
                                        {
                                            Crashes.TrackError(new Exception($@"Vanilla information was null when performing vanilla file check for {afcPath.Substring(internalValidationTarget.TargetPath.Length + 1)}"));
                                        }

                                        if (audioOffset >= vanillaInfo[0].size)
                                        {
                                            Log.Warning($@"Found broken audio: {wwisestream.UIndex} {wwisestream.ObjectName} has broken audio, pointer points beyond the end of the AFC file. Package file: {relativePath}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}.");
                                            item.AddSignificantIssue(M3L.GetString(M3L.string_interp_audioStoredInOfficialAFC, relativePath, wwisestream.InstancedFullPath));
                                        }
                                    }

                                    if (audioStream is FileStream) audioStream.Close();
                                }
                                catch (Exception e)
                                {
                                    Log.Error($@"Error checking for broken audio: {wwisestream?.UIndex} {wwisestream?.ObjectName}. Package file: {relativePath}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}. The error was: {e.Message}");
                                    e.LogStackTrace();
                                    if (audioStream is FileStream) audioStream.Close();
                                    item.AddSignificantIssue(M3L.GetString(M3L.string_errorValidatingAudioReference, relativePath, wwisestream.InstancedFullPath, e.Message));
                                    continue;
                                }
                            }
                        }
                    }
                }


                if (!item.HasAnyMessages())
                {
                    item.ItemText = M3L.GetString(M3L.string_noAudioIssuesWereDetected);
                    item.ToolTip = M3L.GetString(M3L.string_validationOK);
                }
                else
                {
                    item.ItemText = M3L.GetString(M3L.string_audioIssuesWereDetected);
                    item.ToolTip = M3L.GetString(M3L.string_validationFailed);
                }

                cachedAudio.Clear();
            }

            /// <summary>
            /// Checks texture references and known bad texture setups
            /// </summary>
            /// <param name="item"></param>
            private void CheckTextures(DeploymentChecklistItem item)
            {
                // if (ModBeingDeployed.Game >= MEGame.ME2)
                //{
                item.ItemText = M3L.GetString(M3L.string_checkingTexturesInMod);
                var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
                var allTFCs = referencedFiles.Where(x => Path.GetExtension(x) == @".tfc").ToList();
                int numChecked = 0;
                foreach (var f in referencedFiles)
                {
                    if (CheckCancelled) return;
                    numChecked++;
                    item.ItemText = $@"{M3L.GetString(M3L.string_checkingTexturesInMod)} [{numChecked}/{referencedFiles.Count}]";
                    if (f.RepresentsPackageFilePath())
                    {
                        var relativePath = f.Substring(ModBeingDeployed.ModPath.Length + 1);
                        Log.Information(@"Checking file for broken textures: " + f);
                        var package = MEPackageHandler.OpenMEPackage(f);
                        if (package.Game != ModBeingDeployed.Game)
                            continue; // Don't bother checking this
                        var textures = package.Exports.Where(x => x.IsTexture() && !x.IsDefaultObject).ToList();
                        foreach (var texture in textures)
                        {
                            if (CheckCancelled) return;

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
                                        item.AddBlockingError(M3L.GetString(M3L.string_interp_fatalMissingNeverstreamFlag, relativePath, texture.UIndex, texture.InstancedFullPath));
                                    }
                                }

                                if (package.Game == MEGame.ME3) // ME3 only. does not affect LE3
                                {
                                    // CHECK FOR 4K NORM
                                    var compressionSettings = texture.GetProperty<EnumProperty>(@"CompressionSettings");
                                    if (compressionSettings != null && compressionSettings.Value == @"TC_NormalMapUncompressed")
                                    {
                                        var mipTailBaseIdx = texture.GetProperty<IntProperty>(@"MipTailBaseIdx");
                                        if (mipTailBaseIdx != null && mipTailBaseIdx == 12)
                                        {
                                            // It's 4K (2^12)
                                            Log.Error(@"Found 4K Norm. These are not used by game (they use up to 1 mip below the diff) and waste large amounts of memory. Drop the top mip to correct this issue. " + texture.InstancedFullPath);
                                            item.AddBlockingError(M3L.GetString(M3L.string_interp_fatalFound4KNorm, relativePath, texture.UIndex, texture.InstancedFullPath));
                                        }
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
                                            tex.GetImageBytesForMip(tex.GetTopMip(), internalValidationTarget.Game, false, out _, internalValidationTarget.TargetPath, allTFCs); //use active target
                                        }
                                        catch (Exception e)
                                        {
                                            Log.Warning(@"Found broken texture: " + texture.InstancedFullPath);
                                            item.AddSignificantIssue(M3L.GetString(M3L.string_interp_couldNotLoadTextureData, relativePath, texture.InstancedFullPath, e.Message));
                                        }
                                    }

                                    if (cache.Value.Name.Contains(@"CustTextures"))
                                    {
                                        // ME3Explorer 3.0 or below Texplorer
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_error_foundCustTexturesTFCRef, relativePath, texture.InstancedFullPath, cache.Value.Name));
                                    }
                                    else if (cache.Value.Name.Contains(@"TexturesMEM"))
                                    {
                                        // Textures replaced by MEM. This is not allowed in mods as it'll immediately be broken
                                        item.AddBlockingError(M3L.GetString(M3L.string_interp_error_foundTexturesMEMTFCRef, relativePath, texture.InstancedFullPath, cache.Value.Name));
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
                                            tex.GetImageBytesForMip(mip, internalValidationTarget.Game, false, out _, internalValidationTarget.TargetPath);
                                        }
                                        catch (Exception e)
                                        {
                                            Log.Warning(@"Found broken texture: " + texture.InstancedFullPath);
                                            item.AddSignificantIssue(M3L.GetString(M3L.string_interp_couldNotLoadTextureData, relativePath, texture.InstancedFullPath, e.Message));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (!item.HasAnyMessages())
                {
                    item.ItemText = M3L.GetString(M3L.string_noBrokenTexturesWereFound);
                    item.ToolTip = M3L.GetString(M3L.string_validationOK);
                }
                else
                {
                    item.ItemText = M3L.GetString(M3L.string_textureIssuesWereDetected);
                    item.ToolTip = M3L.GetString(M3L.string_validationFailed);
                }
            }


            /// <summary>
            /// Checks object and name references for invalid values and if values are of the incorrect typing.
            /// </summary>
            /// <param name="item"></param>
            private void CheckReferences(DeploymentChecklistItem item)
            {
                item.ItemText = M3L.GetString(M3L.string_checkingNameAndObjectReferences);
                var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Where(x => x.RepresentsPackageFilePath()).Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
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
                            if (CheckCancelled) return;
                            // Mostly ported from ME3Explorer
                            var lnumChecked = Interlocked.Increment(ref numChecked);
                            item.ItemText = M3L.GetString(M3L.string_checkingNameAndObjectReferences) + $@" [{lnumChecked - 1}/{referencedFiles.Count}]";

                            var relativePath = f.Substring(ModBeingDeployed.ModPath.Length + 1);
                            Log.Information($@"Checking package and name references in {relativePath}");
                            var package = MEPackageHandler.OpenMEPackage(Path.Combine(item.ModToValidateAgainst.ModPath, f));
                            EntryChecker.CheckReferences(item, package, M3L.GetString, relativePath);
                        });
                }
                catch (Exception e)
                {
                    Crashes.TrackError(new Exception(M3L.GetString(M3L.string_errorOccurredCheckingReferences), e));
                    Log.Error($@"An error occurred checking references for deployment: {e.Message}.");
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
            #endregion

            /// <summary>
            /// Sets all checks to the 'abandoned' state, as in they will not run due to previous blocking item
            /// </summary>
            public void SetAbandoned()
            {
                foreach (var check in DeploymentChecklistItems)
                {
                    check.SetAbandoned();
                }
            }
        }

        public ICommand DeployCommand { get; set; }
        public ICommand CloseCommand { get; set; }
        public ICommand AddModToDeploymentCommand { get; set; }
        private void LoadCommands()
        {
            DeployCommand = new GenericCommand(StartDeployment, CanDeploy);
            CloseCommand = new GenericCommand(CloseWrapper, CanClose);
            AddModToDeploymentCommand = new GenericCommand(AddModToDeploymentWrapper, CanAddModToDeployment);
        }

        private void CloseWrapper()
        {
            if (DeploymentInProgress)
            {
                var actuallyCancel = M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_7zDeploymentInProgress), M3L.GetString(M3L.string_cancellingDeployment), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                if (actuallyCancel)
                {

                    ClosePanel();
                }
            }
            else if (ModBeingChecked != null)
            {
                var actuallyCancel = M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_DeploymentChecksInProgress), M3L.GetString(M3L.string_cancellingDeployment), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                if (actuallyCancel)
                {
                    EndChecks();
                    ClosePanel();
                }
            }
            else
            {
                ClosePanel();
            }
        }

        private void AddModToDeploymentWrapper()
        {
            var m = mainwindow.AllLoadedMods.Except(ModsInDeployment.Select(x => x.ModBeingDeployed));
            ModSelectorDialog msd = new ModSelectorDialog(window, m.ToList());
            var result = msd.ShowDialog();
            if (result.HasValue && result.Value)
            {
                if (!DeploymentBlocked)
                {
                    foreach (var v in msd.SelectedMods)
                    {
                        AddModToDeployment(v);
                    }
                }
            }
        }

        private bool CanAddModToDeployment() => !DeploymentBlocked && ModsInDeployment.All(x => x.DeploymentChecklistItems.All(y => !y.DeploymentBlocking));

        private void ClosePanel()
        {
            foreach (var v in ModsInDeployment)
            {
                v.CheckCancelled = true;
            }

            Log.Information(@"Closing deployment panel");
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanClose() => !DeploymentInProgress;

        /// <summary>
        /// File extensions that will be stored uncompressed in archive as they already have well compressed data and may be of a large size
        /// (which increases the solid block size)
        /// </summary>
        private static readonly string[] NoCompressExtensions = new[] { @".tfc", @".bik", @".m3m", @".xml" };

        private static readonly string[] IndividualCompressExtensions = new string[] { /*@".xml" */};


        private void StartDeployment()
        {
            var premadeName = "";
            if (ModsInDeployment.Count > 1)
            {
                // Multipack
                premadeName = Utilities.SanitizePath($@"{ModsInDeployment[0].ModBeingDeployed.ModName}_{ModsInDeployment[0].ModBeingDeployed.ModVersionString}_multipack".Replace(@" ", ""), true);
            }
            else
            {
                premadeName = Utilities.SanitizePath($@"{ModsInDeployment[0].ModBeingDeployed.ModName}_{ModsInDeployment[0].ModBeingDeployed.ModVersionString}".Replace(@" ", ""), true);
            }

            SaveFileDialog d = new SaveFileDialog
            {
                Filter = $@"{M3L.GetString(M3L.string_7zipArchiveFile)}|*.7z",
                FileName = premadeName
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
                    else if (ModsInDeployment.Count == 1)
                    {
                        Analytics.TrackEvent(@"Deployed mod", new Dictionary<string, string>()
                        {
                            { @"Mod name" , $@"{ModsInDeployment[0].ModBeingDeployed.ModName} {ModsInDeployment[0].ModBeingDeployed.ParsedModVersion}"}
                        });
                    }
                    else
                    {
                        Analytics.TrackEvent(@"Deployed multipack of mods", new Dictionary<string, string>()
                        {
                            { @"Included mods" , string.Join(';', ModsInDeployment.Select(x=>$@"{x.ModBeingDeployed.ModName} {x.ModBeingDeployed.ParsedModVersion}"))}
                        });
                    }

                    DeploymentInProgress = false;
                    CommandManager.InvalidateRequerySuggested();

                    if (b.Error == null && b.Result is List<Mod> modsForTPMISubmission && modsForTPMISubmission.Any())
                    {
                        var nonSubmittableMods = modsForTPMISubmission.Where(x => x.ModWebsite == Mod.DefaultWebsite).ToList();
                        modsForTPMISubmission.Remove(x => x.ModWebsite == Mod.DefaultWebsite);

                        if (nonSubmittableMods.Any())
                        {
                            M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_noModSiteDeployed, string.Join("\n - ", nonSubmittableMods)), // do not localize
                                M3L.GetString(M3L.string_dialog_noModSiteDeployed), MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }

                        if (modsForTPMISubmission.Any())
                        {
                            var goToTPMIForm = M3L.ShowDialog(window,
                                M3L.GetString(M3L.string_interp_dialog_dlcFolderNotInTPMI,
                                    string.Join('\n', modsForTPMISubmission.Select(x => x.ModName))),
                                M3L.GetString(M3L.string_modsNotInThirdPartyIdentificationService),
                                MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            if (goToTPMIForm == MessageBoxResult.Yes)
                            {
                                OnClosing(new DataEventArgs(modsForTPMISubmission));
                            }
                        }
                    }

                };
                TaskbarHelper.SetProgress(0);
                TaskbarHelper.SetProgressState(TaskbarProgressBarState.Normal);
                nbw.RunWorkerAsync(d.FileName);
                DeploymentInProgress = true;
            }
        }

        private void Deployment_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            //Key is in-archive path, value is on disk path
            var archiveMapping = new Dictionary<string, string>();
            string archivePath = e.Argument as string;

            var modsBeingDeployed = ModsInDeployment.Select(x => x.ModBeingDeployed).ToList();
            bool isMultiPack = modsBeingDeployed.Count > 1;

            // If there is mods for more than one game we will place files into ME1/ME2/ME3 subdirectories.
            bool needsGamePrefix = modsBeingDeployed.Select(x => x.Game).Distinct().Count() > 1;

            // Map of Mod => referenced relative file => in archive dir path, relative. Does not include game prefix
            var modRefMap = new Dictionary<Mod, Dictionary<string, string>>();

            // Get a list of all referenced files by the mods being deployed.
            foreach (var modBeingDeployed in modsBeingDeployed)
            {
                var references = modBeingDeployed.GetAllRelativeReferences(true);
                if (isMultiPack)
                {
                    modRefMap[modBeingDeployed] = references.ToDictionary(x => x, x => $@"{Utilities.SanitizePath(modBeingDeployed.ModName)}\{x}");
                }
                else
                {
                    modRefMap[modBeingDeployed] = references.ToDictionary(x => x, x => x);
                }
            }

            // Build the master mapping by prepending game prefix if necessary
            // Also create a map that maps the archive paths back to the mod they come from, so 
            // we can use it to prefix the path of the mod when determining
            // what files should not be compressed
            var archiveMappingToSourceMod = new Dictionary<string, Mod>();
            foreach (var refMap in modRefMap)
            {
                foreach (var singleMapping in refMap.Value)
                {

                    if (needsGamePrefix)
                    {
                        var inArchivePath = $@"{refMap.Key.Game}\{singleMapping.Value}";
                        archiveMapping[inArchivePath] = Path.Combine(refMap.Key.ModPath, singleMapping.Key);
                        archiveMappingToSourceMod[inArchivePath] = refMap.Key;
                    }
                    else
                    {
                        archiveMapping[singleMapping.Value] = Path.Combine(refMap.Key.ModPath, singleMapping.Key);
                        archiveMappingToSourceMod[singleMapping.Value] = refMap.Key;
                    }
                }
            }

            // Add folders and calculate total size in same pass
            long totalSize = 0;
            var directories = new SortedSet<string>();
            foreach (var v in archiveMapping.ToList()) //tolist prevents concurrent modification
            {
                totalSize += new FileInfo(v.Value).Length;
                var relativeFolders = v.Key.Split('\\');
                string buildingFolderList = "";
                for (int i = 0; i < relativeFolders.Length - 1; i++)
                {
                    var relativeFolder = relativeFolders[i];
                    if (buildingFolderList != "")
                    {
                        buildingFolderList += @"\";
                    }

                    buildingFolderList += relativeFolder;
                    if (directories.Add(buildingFolderList))
                    {
                        // hasn't beed added yet
                        //Debug.WriteLine($"Add folder {buildingFolderList}");
                        archiveMapping[buildingFolderList] = null;
                    }
                }
            }



            //var padWidth = archiveMapping.Keys.MaxBy(x => x.Length).Length + 2;
            //foreach (var v in archiveMapping)
            //{
            //    Debug.WriteLine($@"{v.Key.PadRight(padWidth, ' ')} = {v.Value}");
            //}

            //Debug.WriteLine("DONE");

            // setup the compressor for pass 2
            var compressor = new SevenZipCompressor();

            // Pass 1: Directories
            // Stored uncompressed
            string currentDeploymentStep = M3L.GetString(M3L.string_folders);
            compressor.CustomParameters.Add(@"s", @"off");
            compressor.CompressionMode = CompressionMode.Create;
            compressor.CompressionLevel = CompressionLevel.None;
            compressor.CompressFileDictionary(archiveMapping.Where(x => x.Value == null).Select(x => x.Key).ToDictionary(x => x, x => (string)null), archivePath);

            // Setup compress for pass 2
            compressor.CustomParameters.Clear();
            compressor.CompressionMode = CompressionMode.Append; //Append to 
            compressor.CustomParameters.Add(@"s", @"on");
            if (!MultithreadedCompression)
            {
                // Single thread
                compressor.CustomParameters.Add(@"mt", @"off");
            }
            else
            {
                // Multi thread
                // Get size of all files in total
                if (totalSize > FileSize.GibiByte * 1.25)
                {
                    //cap threads to prevent huge memory usage when compressing big mods
                    var cores = Environment.ProcessorCount;
                    cores = Math.Min(cores, 5);
                    compressor.CustomParameters.Add(@"mt", cores.ToString());
                }
            }

            compressor.CustomParameters.Add(@"yx", @"9");
            compressor.CustomParameters.Add(@"d", @"28"); //Dictionary size 2^28 (256MB)
            currentDeploymentStep = M3L.GetString(M3L.string_mod);

            DateTime lastPercentUpdateTime = DateTime.Now;
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
                    OperationText = $@"[{currentDeploymentStep}] {M3L.GetString(M3L.string_deploymentInProgress)} {percent}%";
                    lastPercentUpdateTime = now;
                }
                //Debug.WriteLine(ProgressValue + "/" + ProgressMax);
            };
            compressor.FileCompressionStarted += (a, b) => { Debug.WriteLine(b.FileName); };

            // Pass 2: Compressed items and empty folders
            // Includes package files and other basic file types
            // Does not include AFC, TFC, or .BIK
            // Does not include moddesc.ini
            // Does not include any referenced image files under M3Images
            currentDeploymentStep = M3L.GetString(M3L.string_compressedModItems);

            var compressItems = archiveMapping.Where(x => x.Value != null && ShouldBeSolidCompressed(x, archiveMappingToSourceMod[x.Key])).ToDictionary(p => p.Key, p => p.Value);
            if (compressItems.Any())
            {
                compressor.CompressFileDictionary(compressItems, archivePath);
            }

            // Pass 2: Individual compressed items (non-solid)
            compressor.CustomParameters.Clear(); //remove custom params as it seems to force LZMA
            compressor.CustomParameters.Add(@"s", @"off");
            compressor.CompressionMode = CompressionMode.Append; //Append to 
            compressor.CustomParameters.Add(@"yx", @"9");
            compressor.CustomParameters.Add(@"d", @"28"); //Dictionary size 2^28 (256MB)
            var individualcompressItems = archiveMapping.Where(x => x.Value != null && ShouldBeIndividualCompressed(x, archiveMappingToSourceMod[x.Key])).ToDictionary(p => p.Key, p => p.Value);

            if (individualcompressItems.Any())
            {
                currentDeploymentStep = M3L.GetString(M3L.string_individuallyCompressedItems);
                compressor.CompressFileDictionary(individualcompressItems, archivePath);
            }
            // Compress files one at a time to prevent solid
            //foreach (var item in individualcompressItems)
            //{
            //    var d = new Dictionary<string, string>();
            //    d[item.Key] = item.Value;
            //    compressor.CompressFileDictionary(d, archivePath);
            //}

            // Pass 3: Uncompressed items
            compressor.CustomParameters.Clear(); //remove custom params as it seems to force LZMA
            compressor.CompressionMode = CompressionMode.Append; //Append to 
            compressor.CompressionLevel = CompressionLevel.None;

            currentDeploymentStep = M3L.GetString(M3L.string_uncompressedModItems);
            var nocompressItems = archiveMapping.Where(x => x.Value != null && !ShouldBeSolidCompressed(x, archiveMappingToSourceMod[x.Key]) && !ShouldBeIndividualCompressed(x, archiveMappingToSourceMod[x.Key])).Reverse().ToDictionary(p => p.Key, p => p.Value);

            compressor.CompressFileDictionary(nocompressItems, archivePath);

            OperationText = M3L.GetString(M3L.string_deploymentSucceeded);
            Utilities.HighlightInExplorer(archivePath);

            e.Result = GetModsNeedingTPMISubmission(modsBeingDeployed);
        }

        private List<Mod> GetModsNeedingTPMISubmission(List<Mod> modsBeingDeployed)
        {
            List<Mod> modsNeedingSubmission = new List<Mod>();
            foreach (var v in modsBeingDeployed)
            {
                var folders = v.GetAllPossibleCustomDLCFolders();
                foreach (var f in folders)
                {
                    var tpmi = ThirdPartyServices.GetThirdPartyModInfo(f, v.Game);
                    if (tpmi == null)
                    {
                        modsNeedingSubmission.Add(v);
                        break; // don't need to parse more of this mod
                    }
                }
            }

            return modsNeedingSubmission;
        }

        /// <summary>
        /// Determines if a file should be compressed into the archive, or stored uncompressed
        /// </summary>
        /// <param name="fileMapping">Mapping of the in-archive path to the source file. If the value is null, it means it's a folder.</param>
        /// <returns></returns>
        private bool ShouldBeSolidCompressed(KeyValuePair<string, string> fileMapping, Mod modBeingDeployed)
        {
            if (fileMapping.Value == null) return false; //This can't be compressed (it's a folder). Do not put it in solid block as it must appear before.
            if (NoCompressExtensions.Contains(Path.GetExtension(fileMapping.Value))) return false; //Do not compress these extensions
            if (IndividualCompressExtensions.Contains(Path.GetExtension(fileMapping.Value), StringComparer.InvariantCultureIgnoreCase)) return false; //Do not compress these extensions solid
            var modRelPath = fileMapping.Value.Substring(modBeingDeployed.ModPath.Length + 1);
            if (modRelPath.StartsWith(@"M3Images", StringComparison.InvariantCultureIgnoreCase))
                return false; // Referenced image file should not be compressed.
            if (modRelPath == @"moddesc.ini")
                return false; // moddesc.ini should not be compressed.
            return true;
        }

        /// <summary>
        /// Determines if a file should be compressed into the archive with individual compression (non-solid). Does not check if it would also match ShouldBeSolidCompressed.
        /// </summary>
        /// <param name="fileMapping">Mapping of the in-archive path to the source file. If the value is null, it means it's a folder.</param>
        /// <returns></returns>
        private bool ShouldBeIndividualCompressed(KeyValuePair<string, string> fileMapping, Mod modBeingDeployed)
        {
            if (fileMapping.Value == null) return false; //This can't be compressed (it's a folder). It should be done in the solid pass
            return IndividualCompressExtensions.Contains(Path.GetExtension(fileMapping.Value), StringComparer.InvariantCultureIgnoreCase);
        }

        private bool CanDeploy()
        {
            return PrecheckCompleted && !DeploymentInProgress && !DeploymentBlocked && ModsInDeployment.All(x => x.DeploymentChecklistItems.All(x => !x.DeploymentBlocking));
        }

        public bool CanChangeValidationTarget => !DeploymentInProgress && ModBeingChecked == null;

        public bool PrecheckCompleted { get; private set; }
        [AlsoNotifyFor(nameof(CanChangeValidationTarget))]
        public bool DeploymentInProgress { get; private set; }
        public ulong ProgressMax { get; set; } = 100;
        public ulong ProgressValue { get; set; } = 0;
        public string OperationText { get; set; } = M3L.GetString(M3L.string_checkingModBeforeDeployment);
        //M3L.GetString(M3L.string_verifyAboveItemsBeforeDeployment);

        /// <summary>
        /// A single deployment checklist item and state
        /// </summary>
        [AddINotifyPropertyChangedInterface]
        public class DeploymentChecklistItem : ReferenceCheckPackage, INotifyPropertyChanged
        {
            public Mod ModToValidateAgainst;

            // Bindings
            public string ItemText { get; set; }
            public SolidColorBrush Foreground { get; private set; }
            public FontAwesomeIcon Icon { get; private set; }
            public bool Spinning { get; private set; }
            public bool DeploymentBlocking { get; private set; }
            public string ToolTip { get; set; }
            public Action<DeploymentChecklistItem> ValidationFunction { get; set; }
            public string DialogMessage { get; set; }
            public string DialogTitle { get; set; }
            public bool CheckDone { get; private set; }

            public bool HasMessage => CheckDone && HasAnyMessages();

            public DeploymentChecklistItem()
            {
                Initialize();
            }

            private void Initialize()
            {
                Icon = FontAwesomeIcon.Spinner;
                Spinning = true;
                Foreground = Application.Current.FindResource(AdonisUI.Brushes.DisabledAccentForegroundBrush) as SolidColorBrush;
                ToolTip = M3L.GetString(M3L.string_validationInProgress);
            }

            public void SetDone()
            {
                CheckDone = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMessage)));
            }

            public void ExecuteValidationFunction()
            {
                Foreground = Application.Current.FindResource(AdonisUI.Brushes.HyperlinkBrush) as SolidColorBrush;
                ValidationFunction?.Invoke(this);
                Spinning = false;
                if (!HasAnyMessages())
                {
                    Foreground = Brushes.Green;
                    Icon = FontAwesomeIcon.CheckCircle;
                }
                else if (GetBlockingErrors().Any())
                {
                    Foreground = Brushes.Red;
                    Icon = FontAwesomeIcon.TimesCircle;
                }
                else if (GetSignificantIssues().Any())
                {
                    Foreground = Brushes.Orange;
                    Icon = FontAwesomeIcon.Warning;
                }
                else if (GetInfoWarnings().Any())
                {
                    Foreground = Brushes.DodgerBlue;
                    Icon = FontAwesomeIcon.InfoCircle;
                }

                SetDone();
            }

            //Fody uses this property on weaving
#pragma warning disable
            public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

            public bool HasAnyMessages() => GetInfoWarnings().Any() || GetSignificantIssues().Any() || GetBlockingErrors().Any();

            public void Reset()
            {
                ClearMessages();
                CheckDone = false;
                Initialize();
            }

            public void SetAbandoned()
            {
                Foreground = Brushes.Gray;
                Icon = FontAwesomeIcon.Ban;
                Spinning = false;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (sender is Hyperlink hl && hl.DataContext is DeploymentChecklistItem dcli)
            {
                DeploymentListDialog ld = new DeploymentListDialog(dcli, Window.GetWindow(hl));
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
            AddModToDeployment(initialMod);
            initialMod = null;
        }

        private void StartCheck(EncompassingModDeploymentCheck emc)
        {
            Log.Information($@"Starting deployment check on mod {emc.ModBeingDeployed.Game} {emc.ModBeingDeployed.ModName}");
            if (emc.DepValidationTarget.SelectedTarget == null)
            {
                // There's no selected target! There might not be one available.
                emc.SetAbandoned();
                DeploymentBlocked = true;
                DeployButtonText = M3L.GetString(M3L.string_deploymentBlocked);
                OperationText = M3L.GetString(M3L.string_interp_noValidationTarget, emc.DepValidationTarget.Game);
                while (!PendingChecks.IsEmpty)
                {
                    if (PendingChecks.TryDequeue(out var nEmc))
                    {
                        nEmc.SetAbandoned();
                    }
                }
                EndChecks();
                return;
            }


            ModBeingChecked = emc;

            // Ensure UI vars are set
            DeployButtonText = M3L.GetString(M3L.string_pleaseWait);
            PrecheckCompleted = false;
            ProgressIndeterminate = true;
            TaskbarHelper.SetProgressState(TaskbarProgressBarState.Indeterminate);

            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"DeploymentValidation");
            nbw.DoWork += (a, b) =>
            {
                ProgressIndeterminate = true;
                emc.RunChecks();
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }

                DeploymentBlocked = emc.DeploymentChecklistItems.Any(x => x.DeploymentBlocking);
                if (DeploymentBlocked)
                {
                    // Deployment has been blocked
                    OperationText = M3L.GetString(M3L.string_deploymentBlockedUntilAboveItemsAreFixed);
                    DeployButtonText = M3L.GetString(M3L.string_deploymentBlocked);

                    while (!PendingChecks.IsEmpty)
                    {
                        if (PendingChecks.TryDequeue(out var nEmc))
                        {
                            nEmc.SetAbandoned();
                        }
                    }
                    EndChecks();
                }
                else if (PendingChecks.TryDequeue(out var emc_next))
                {
                    // Run the next check
                    StartCheck(emc_next);
                }
                else
                {
                    // No more checks and deployment not blocked
                    DeployButtonText = M3L.GetString(M3L.string_deploy);
                    OperationText = M3L.GetString(M3L.string_verifyAboveItemsBeforeDeployment);
                    EndChecks();
                }
            };

            nbw.RunWorkerAsync();
        }

        public bool DeploymentBlocked { get; set; }

        private void EndChecks()
        {
            ModBeingChecked = null;
            MELoadedFiles.InvalidateCaches();
            TaskbarHelper.SetProgress(0);
            TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
            PrecheckCompleted = true;
            ProgressIndeterminate = false;
            CommandManager.InvalidateRequerySuggested();
        }

        [AlsoNotifyFor(nameof(CanChangeValidationTarget))]
        public EncompassingModDeploymentCheck ModBeingChecked { get; set; }

        public void AddModToDeployment(Mod mod)
        {
            Log.Information($@"Adding mod to deployment: {mod.ModName}");
            var dvt = ValidationTargets.FirstOrDefault(x => x.Game == mod.Game);
            if (dvt == null)
            {
                // No validation targets for this game yet
                var targets = mainwindow.InstallationTargets.Where(x => x.Game == mod.Game).ToList();
                Log.Information($@"Adding validation target for {mod.Game}. Num targets for this game: {targets.Count()}. Total target count in MW: {mainwindow.InstallationTargets.Count}");
                dvt = new DeploymentValidationTarget(this, mod.Game, targets); // new target

                // Add validation target and sort game list
                var sortedTargetList = ValidationTargets.ToList();
                sortedTargetList.Add(dvt);
                ValidationTargets.ReplaceAll(sortedTargetList.OrderBy(x => x.Game));
            }

            var depMod = new EncompassingModDeploymentCheck(mod, dvt);
            ModsInDeployment.Add(depMod);
            if (ModBeingChecked != null)
            {
                PendingChecks.Enqueue(depMod);
            }
            else
            {
                StartCheck(depMod);
            }
        }

        public bool ProgressIndeterminate { get; set; }

        public ObservableCollectionExtended<DeploymentValidationTarget> ValidationTargets { get; } = new ObservableCollectionExtended<DeploymentValidationTarget>();

        /// <summary>
        /// Object that contains info about the validation targets for a mod. Only one of these can exist per game
        /// </summary>
        [AddINotifyPropertyChangedInterface]
        public class DeploymentValidationTarget
        {
            public MEGame Game { get; }
            public GameTarget SelectedTarget { get; set; }
            public string HeaderString { get; }
            public ObservableCollectionExtended<GameTarget> AvailableTargets { get; } = new ObservableCollectionExtended<GameTarget>();
            public ArchiveDeployment DeploymentHost { get; set; }

            public DeploymentValidationTarget(ArchiveDeployment deploymentHost, MEGame game, IEnumerable<GameTarget> targets)
            {
                DeploymentHost = deploymentHost;
                Game = game;
                HeaderString = M3L.GetString(M3L.string_interp_gamenameValidationTarget, game.ToGameName());
                foreach (var t in targets)
                {
                    if (t.TextureModded)
                    {
                        Log.Warning($@"Target is texture modded, cannot be used for validation: {t.TargetPath}, skipping...");
                        continue;
                    }
                    AvailableTargets.Add(t);
                }
                SelectedTarget = AvailableTargets.FirstOrDefault();
            }


            public void OnSelectedTargetChanged(object before, object after)
            {
                if (before != null)
                {
                    //Target has changed
                    DeploymentHost.OnValidationTargetChanged((after as GameTarget).Game);
                }
            }

        }

        // This is not incorrectly named. It's just got not matching value
        [SuppressPropertyChangedWarnings]
        private void OnValidationTargetChanged(MEGame game)
        {
            foreach (var v in ModsInDeployment.Where(x => x.ModBeingDeployed.Game == game))
            {
                PendingChecks.Enqueue(v);
                foreach (var c in v.DeploymentChecklistItems)
                {
                    c.Reset();
                }
            }

            if (PendingChecks.TryDequeue(out var mod))
            {
                StartCheck(mod);
            }
        }
    }
}
