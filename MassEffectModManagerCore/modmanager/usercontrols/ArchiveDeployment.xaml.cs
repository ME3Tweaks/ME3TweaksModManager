using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.loaders;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.deployment;
using ME3TweaksModManager.modmanager.objects.deployment.checks;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.tlk;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Taskbar;
using PropertyChanged;
using SevenZip;

namespace ME3TweaksModManager.modmanager.usercontrols
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
            M3Log.Information($@"Initiating deployment for mod {mod.ModName} {mod.ModVersionString}");
            TelemetryInterposer.TrackEvent(@"Started deployment panel for mod", new Dictionary<string, string>()
            {
                { @"Mod name" , $@"{mod.ModName} {mod.ParsedModVersion}"}
            });
            initialMod = mod;
            LoadCommands();
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
            // Not very performant, but it works...
            var m = M3LoadedMods.Instance.AllLoadedMods.Where(x => BackupService.GetBackupStatus(x.Game).BackedUp).Except(ModsInDeployment.Select(x => x.ModBeingDeployed)).OrderBy(x => x.Game).ThenBy(x => x.ModName).ToList();
            ModSelectorDialog msd = new ModSelectorDialog(window, m, M3L.GetString(M3L.string_addModsToDeployment),
                M3L.GetString(M3L.string_description_addSelectedModsToDeployment),
                M3L.GetString(M3L.string_addSelectedModsToDeployment))
            {
                SelectionMode = SelectionMode.Extended // Can select multiple
            };
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

            M3Log.Information(@"Closing deployment panel");
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
                premadeName = M3Utilities.SanitizePath($@"{ModsInDeployment[0].ModBeingDeployed.ModName}_{ModsInDeployment[0].ModBeingDeployed.ModVersionString}_multipack".Replace(@" ", ""), true);
            }
            else
            {
                premadeName = M3Utilities.SanitizePath($@"{ModsInDeployment[0].ModBeingDeployed.ModName}_{ModsInDeployment[0].ModBeingDeployed.ModVersionString}".Replace(@" ", ""), true);
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
                        // Logged internally by nbw
                    }
                    else if (ModsInDeployment.Count == 1)
                    {
                        TelemetryInterposer.TrackEvent(@"Deployed mod", new Dictionary<string, string>()
                        {
                            { @"Mod name" , $@"{ModsInDeployment[0].ModBeingDeployed.ModName} {ModsInDeployment[0].ModBeingDeployed.ParsedModVersion}"}
                        });
                    }
                    else
                    {
                        TelemetryInterposer.TrackEvent(@"Deployed multipack of mods", new Dictionary<string, string>()
                        {
                            { @"Included mods" , string.Join(';', ModsInDeployment.Select(x=>$@"{x.ModBeingDeployed.ModName} {x.ModBeingDeployed.ParsedModVersion}"))}
                        });
                    }

                    DeploymentInProgress = false;
                    CommandManager.InvalidateRequerySuggested();

                    if (b.Error == null && b.Result is List<Mod> modsForTPMISubmission && modsForTPMISubmission.Any())
                    {
                        var nonSubmittableMods = modsForTPMISubmission.Where(x => x.ModWebsite == Mod.DefaultWebsite).ToList();
                        modsForTPMISubmission.RemoveAll(x => x.ModWebsite == Mod.DefaultWebsite);

                        if (nonSubmittableMods.Any())
                        {
                            M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_noModSiteDeployed, string.Join("\n - ", nonSubmittableMods.Select(x => x.ModName))), // do not localize
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

                if (modBeingDeployed.ModDescTargetVersion >= 8.0)
                {
                    // ModDesc 8 / Mod Manager 8 uses a CompressedTLKMergeInfo file instead. Do not compress these files at all, it will be handled in a separate step.
                    references = references.Where(x => !x.StartsWith(Mod.Game1EmbeddedTlkFolderName, StringComparison.CurrentCultureIgnoreCase)).ToList();
                }

                if (isMultiPack)
                {
                    modRefMap[modBeingDeployed] = references.ToDictionary(x => x, x => $@"{M3Utilities.SanitizePath(modBeingDeployed.ModName)}\{x}");
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
            // Does not include any GAME1_TLK_MERGE files if cmmver >= 8.0, since it uses a combined version
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

            void generatingCompressedFileProgress(uint done, uint total)
            {
                ProgressMax = total;
                ProgressValue = done;

                // Todo: Combine this with the other update.
                var now = DateTime.Now;
                if ((now - lastPercentUpdateTime).Milliseconds > ModInstaller.PERCENT_REFRESH_COOLDOWN)
                {
                    //Don't update UI too often. Once per second is enough.
                    var progValue = ProgressValue * 100.0 / ProgressMax;
                    string percent = progValue.ToString(@"0.00");
                    OperationText = $@"[{currentDeploymentStep}] {M3L.GetString(M3L.string_deploymentInProgress)} {percent}%";
                    lastPercentUpdateTime = now;
                }
            }

            // Pass 4: CompressedTLKMergeInfo
            foreach (var modBeingDeployed in modsBeingDeployed.Where(x => x.Game.IsGame1()))
            {
                var references = modBeingDeployed.GetAllRelativeReferences(true);

                if (modBeingDeployed.ModDescTargetVersion >= 8.0 && references.Any(x => x.StartsWith(Mod.Game1EmbeddedTlkFolderName)))
                {
                    // It needs a compression tlk merge file installed
                    currentDeploymentStep = M3L.GetString(M3L.string_creatingCombinedTLKMergeFile);
                    var inputFolder = Path.Combine(modBeingDeployed.ModPath, Mod.Game1EmbeddedTlkFolderName);
                    var compressedData = CompressedTLKMergeData.CreateCompressedTlkMergeFile(inputFolder, generatingCompressedFileProgress).GetBuffer();
                    var mergeFileTemp = Path.Combine(M3Filesystem.GetTempPath(), Mod.Game1EmbeddedTlkCompressedFilename);
                    File.WriteAllBytes(mergeFileTemp, compressedData);
                    var inArchiveMergeFilePath = $@"{Mod.Game1EmbeddedTlkFolderName}\{Mod.Game1EmbeddedTlkCompressedFilename}";
                    var inArchiveGame1TlkFolderPath = Mod.Game1EmbeddedTlkFolderName;
                    if (isMultiPack)
                    {
                        inArchiveMergeFilePath = $@"{M3Utilities.SanitizePath(modBeingDeployed.ModName)}\{inArchiveMergeFilePath}";
                        inArchiveGame1TlkFolderPath = $@"{M3Utilities.SanitizePath(modBeingDeployed.ModName)}\{inArchiveGame1TlkFolderPath}";
                    }

                    currentDeploymentStep = M3L.GetString(M3L.string_addingCombinedTLKMergeFile);
                    compressor.CompressFileDictionary(new Dictionary<string, string>()
                    {
                        { inArchiveGame1TlkFolderPath, null }, // The folder - this is required to be added so it shows up in the archive filesystem table
                        { inArchiveMergeFilePath, mergeFileTemp } // The actual file in the folder
                    }, archivePath);
                    File.Delete(mergeFileTemp);

                }
            }

            OperationText = M3L.GetString(M3L.string_deploymentSucceeded);
            M3Utilities.HighlightInExplorer(archivePath);

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
                    var tpmi = TPMIService.GetThirdPartyModInfo(f, v.Game);
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
            if (modBeingDeployed.ModDescTargetVersion >= 8.0 && fileMapping.Key.StartsWith(Mod.Game1EmbeddedTlkFolderName, StringComparison.CurrentCultureIgnoreCase))
                return false; // ModDesc 8 / Mod Manager 8 uses a CompressedTLKMergeInfo file instead.
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
        /// <summary>
        /// Maximum on the progress bar
        /// </summary>
        public ulong ProgressMax { get; set; } = 100;
        /// <summary>
        /// The current value of the progress bar
        /// </summary>
        public ulong ProgressValue { get; set; } = 0;
        /// <summary>
        /// The bottom left text that describes the current operation
        /// </summary>
        public string OperationText { get; set; } = M3L.GetString(M3L.string_checkingModBeforeDeployment);
        //M3L.GetString(M3L.string_verifyAboveItemsBeforeDeployment);
        public ConcurrentQueue<EncompassingModDeploymentCheck> PendingChecks { get; } = new ConcurrentQueue<EncompassingModDeploymentCheck>();

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
            InitializeComponent();
            AddModToDeployment(initialMod);
            initialMod = null;
        }

        private void StartCheck(EncompassingModDeploymentCheck emc)
        {
            M3Log.Information($@"Starting deployment check on mod {emc.ModBeingDeployed.Game} {emc.ModBeingDeployed.ModName}");
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
            M3Log.Information($@"Adding mod to deployment: {mod.ModName}");
            var dvt = ValidationTargets.FirstOrDefault(x => x.Game == mod.Game);
            if (dvt == null)
            {
                // No validation targets for this game yet
                var targets = mainwindow.InstallationTargets.Where(x => x.Game == mod.Game).ToList();
                M3Log.Information($@"Adding validation target for {mod.Game}. Num targets for this game: {targets.Count()}. Total target count in MW: {mainwindow.InstallationTargets.Count}");
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
            TriggerResize(); // Size of panel may change
        }

        public bool ProgressIndeterminate { get; set; }

        public ObservableCollectionExtended<DeploymentValidationTarget> ValidationTargets { get; } = new ObservableCollectionExtended<DeploymentValidationTarget>();


        // This is not incorrectly named. It's just got not matching value
        [SuppressPropertyChangedWarnings]
        internal void OnValidationTargetChanged(MEGame game)
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
