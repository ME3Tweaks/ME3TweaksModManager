using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.gameini;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects.batch;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using SevenZip;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Services.FileSource;
using ME3TweaksModManager.modmanager.objects.mod.interfaces;
using LegendaryExplorerCore.Helpers;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.ui;
using SevenZip.EventArguments;

namespace ME3TweaksModManager.modmanager.importer
{

    public enum EModArchiveImportState
    {
        INVALID,
        /// <summary>
        /// Operation has failed
        /// </summary>
        FAILED,
        /// <summary>
        /// Archive is being scanned for mods
        /// </summary>
        SCANNING,
        /// <summary>
        /// Archive has been scanned for mods and data is now populated
        /// </summary>
        SCANCOMPLETED,
        /// <summary>
        /// Mods are currently importing from this archive to the library
        /// </summary>
        IMPORTING,
        /// <summary>
        /// Mods have been imported from the archive into the library
        /// </summary>
        COMPLETE
    }

    /// <summary>
    /// Describes result of import
    /// </summary>
    public enum EModImportResult
    {
        USER_ABORTED_IMPORT,
        ERROR_COULD_NOT_DELETE_EXISTING_DIR,
        ERROR_INSUFFICIENT_DISK_SPACE,
        ERROR_EXTRACTING_ARCHIVE,
        ERROR_COULD_NOT_CREATE_MOD_FOLDER,
        OK,
    }

    /// <summary>
    /// Container class for an archive file's contents and handling the importing
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class ModArchiveImport
    {
        /// <summary>
        /// Current activity state
        /// </summary>
        public EModArchiveImportState CurrentState { get; private set; }

        private void OnCurrentStateChanged()
        {
            ImportStateChanged?.Invoke(this, EventArgs.Empty);
        }

        #region UI Bindings
        public string ActionText { get; internal set; }
        public long ProgressValue { get; private set; }
        public long ProgressMaximum { get; private set; }
        public bool ProgressIndeterminate { get; private set; }


        #region Testing and precomputed
        /// <summary>
        /// For forcing size of test archive
        /// </summary>
        public long? ForcedSize { get; set; }
        /// <summary>
        /// For forcing md5 of test archive or precomputed archive md5
        /// </summary>
        public string ForcedMD5 { get; set; }
        #endregion


        private void OnProgressValueChanged()
        {
            ProgressChanged?.Invoke(this, new M3ProgressEventArgs(ProgressValue, ProgressMaximum, ProgressIndeterminate));
        }

        private void OnProgressMaximumChanged()
        {
            ProgressChanged?.Invoke(this, new M3ProgressEventArgs(ProgressValue, ProgressMaximum, ProgressIndeterminate));
        }

        private void OnProgressIndeterminateChanged()
        {
            ProgressChanged?.Invoke(this, new M3ProgressEventArgs(ProgressValue, ProgressMaximum, ProgressIndeterminate));
        }

        // OT uses these.
        public int CompressionProgressValue { get; set; }
        public int CompressionProgressMaximum { get; set; } = 100;

        #endregion

        #region UI Callbacks

        /// <summary>
        /// Invoke when you want to signal an error has occurred. Signature is title, message.
        /// </summary>
        public Action<string, string> ErrorCallback;

        /// <summary>
        /// Invoke when you want to show a full dialog with result. Strings are title, message.
        /// </summary>
        public Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult, MessageBoxResult> ShowDialogCallback;

        /// <summary>
        /// Invoked when an item is added to CompressedMods
        /// </summary>
        public Action OnCompressedModAdded;

        /// <summary>
        /// Invoked when a mod fails to load from archive
        /// </summary>
        public Action<Mod> OnModFailedToLoad;

        // Todo: Change to vars for caller to access
        /// <summary>
        /// Invoked when the CurrentState value changes
        /// </summary>
        public event EventHandler ImportStateChanged;

        /// <summary>
        /// Invoked when the importer needs to set results on this panel
        /// </summary>
        public Func<PanelResult> GetPanelResult;


        /// <summary>
        /// Invoked when progress has changed
        /// </summary>
        public event EventHandler<M3ProgressEventArgs> ProgressChanged;
        #endregion

        #region DATA

        /// <summary>
        /// If this scan and import is automated with no user interaction
        /// </summary>
        public bool AutomatedMode { get; set; }
        /// <summary>
        /// Base filename of archive, from stream or from disk.
        /// </summary>
        public string ScanningFile { get; private set; }
        /// <summary>
        /// Reason scan failed, if any
        /// </summary>
        public string ScanFailureReason { get; private set; }
        /// <summary>
        /// Archive file on disk this was initialized from (if any)
        /// </summary>
        public string ArchiveFilePath { get; set; }
        /// <summary>
        /// Stream containing the archive
        /// </summary>
        public Stream ArchiveStream { get; set; }
        /// <summary>
        /// OT only - compress packages after extraction using package compression
        /// </summary>
        public bool CompressPackages { get; set; }
        /// <summary>
        /// List of supported mods found in the archive
        /// </summary>
        public ObservableCollectionExtended<IImportableMod> CompressedMods { get; } = new();
        // Used to help build batch source database
        /// <summary>
        /// The NXM link that was used to produce this archive file for inspection
        /// </summary>
        public NexusProtocolLink SourceNXMLink { get; set; }
        #endregion

        #region RESULTS
        /// <summary>
        /// If this object imported an LE texture mod file
        /// </summary>
        public bool ImportedLETextureMod { get; set; }

        /// <summary>
        /// If this object imported an OT texture mod file (.mem)
        /// </summary>
        public bool ImportedOTTextureMod { get; set; }

        /// <summary>
        /// If this object imported a batch queue file
        /// </summary>
        public bool ImportedBatchQueue { get; set; }

        /// <summary>
        /// List of moddesc.ini files that have been modified due to the extract operation
        /// </summary>
        public List<string> ModifiedModdescFiles { get; } = new List<string>(1);

        /// <summary>
        /// Result of import
        /// </summary>
        public EModImportResult ImportResult { get; private set; }

        /// <summary>
        /// Nexus Mod download object. Not sure we should keep this reference.
        /// </summary>
        public NexusModDownload UpdateModObject { get; set; }

        #endregion


        /// <summary>
        /// Begins inspection of archive file. This method will spawn a background thread that will
        /// run asynchronously.
        /// </summary>
        private void InspectArchiveFile()
        {
            if (!ValidateSetup())
                return; // Validation failed
            CurrentState = EModArchiveImportState.SCANNING;

            ScanningFile = Path.GetFileName(ArchiveFilePath);
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModArchiveInspector");
            nbw.DoWork += InspectArchiveBackgroundThread;
            ProgressValue = 0;
            ProgressMaximum = 100;
            ProgressIndeterminate = true;

            nbw.RunWorkerCompleted += (a, b) =>
            {
                HandleScanResults();
            };
            ActionText = M3L.GetString(M3L.string_interp_scanningX, ScanningFile);
            nbw.RunWorkerAsync(ArchiveFilePath);
        }

        private bool ValidateSetup()
        {
            bool setupIsBad()
            {
                M3Log.Error(@"Bad ModArchiveImport setup. This is a bug in Mod Manager, please report it");
                CurrentState = EModArchiveImportState.FAILED;
                Debugger.Break();
                return false;
            }

            // Object must be able to access panel result
            if (GetPanelResult == null)
            {
                return setupIsBad();
            }

            if (!AutomatedMode)
            {
                // In non automated mode we must have a way to show things to the user
                if (ErrorCallback == null || ShowDialogCallback == null)
                {
                    return setupIsBad();
                }

                // In non automated mode we must have handling for bad mods.
                if (OnModFailedToLoad == null)
                {
                    return setupIsBad();
                }
            }

            return true;
        }

        private void HandleScanResults()
        {
            if (SourceNXMLink != null)
            {
                var downloadLink = SourceNXMLink.ToNexusDownloadPageLink();
                var dictionary = new CaseInsensitiveDictionary<FileSourceRecord>();
                foreach (var mod in CompressedMods.OfType<Mod>())
                {
                    if (mod.ModDescHash == null)
                        continue; // We don't add these, they may have been server loaded
                    dictionary[mod.ModDescHash] = new FileSourceRecord()
                    {
                        DownloadLink = downloadLink,
                        Hash = mod.ModDescHash,
                        Size = mod.ModDescSize,
                        Name = $@"moddesc.ini: ({mod.Game}) {mod.ModName} {mod.ModVersionString}"
                    };
                }

                FileSourceService.AddFileSourceEntries(dictionary, Settings.EnableTelemetry ? ServerManifest.GetInt(ServerManifest.SERVER_ALIGNMENT) : null);
            }

            ActionText = null;
            M3Log.Information(@"Archive scan thread exited");
            ProgressValue = 0;
            ProgressIndeterminate = false;


            var hasAnyImproperlyPackedMods = CompressedMods.Any(x => x is Mod { CheckDeployedWithM3: true, DeployedWithM3: false });

            if (hasAnyImproperlyPackedMods)
            {
                TelemetryInterposer.TrackEvent(@"Detected improperly packed M3 mod v2",
                    new Dictionary<string, string>()
                    {
                        {@"Archive name", Path.GetFileName(ArchiveFilePath)}
                    });

                M3Log.Error(@"A mod in the archive was not deployed using M3 and targets 6.0 or higher! You should contact the developer and tell them to deploy it properly.");
                if (!AutomatedMode)
                {
                    ShowDialogCallback?.Invoke(
                        M3L.GetString(M3L.string_improperlyDeployedMod),
                        M3L.GetString(M3L.string_dialog_improperlyDeployedMod),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning,
                        MessageBoxResult.OK);
                }
            }

            // This will fire off listeners for this object
            CurrentState = EModArchiveImportState.SCANCOMPLETED;

            if (AutomatedMode)
                BeginImporting();
        }

        /// <summary>
        /// Inspects an 'archive' file. Archives may contain one or more mods (or none).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InspectArchiveBackgroundThread(object sender, DoWorkEventArgs e)
        {
            ActionText = M3L.GetString(M3L.string_interp_openingX, ScanningFile);

            var archive = e.Argument as string;

            M3Log.Information($@"Scanning archive for mods: {archive}");
            void AddCompressedModCallback(Mod m)
            {
#if !AZURE
                Application.Current.Dispatcher.Invoke(delegate
                {
#endif
                    CompressedMods.Add(m);
                    OnCompressedModAdded?.Invoke();
                    CompressedMods.Sort(x => x.ModName);
#if !AZURE
                });
#endif
            }

            var archiveSize = ForcedSize ?? ArchiveStream?.Length ?? new FileInfo(archive).Length;

            // ModManager 8: Blacklisting files by size/hash
            string calculatedMD5 = ForcedMD5; // If we calc it here don't calc it later

            var blacklistings = BlacklistingService.GetBlacklistings(archiveSize);
            if (blacklistings.Any())
            {
                calculatedMD5 ??= ArchiveStream != null ? MUtilities.CalculateHash(ArchiveStream) : MUtilities.CalculateHash(archive);
                if (blacklistings.Any(x => x.MD5 == calculatedMD5))
                {
                    // This archive is blacklisted
                    AddCompressedModCallback(new Mod(false)
                    {
                        ModName = M3L.GetString(M3L.string_blacklistedMod),
                        ModDeveloper = M3L.GetString(M3L.string_NA),
                        LoadFailedReason = M3L.GetString(M3L.string_description_blacklistedMod)
                    });
                    return;
                }
            }


            // We consider .me2mod an archive file since it can be segmented
            if (Path.GetExtension(archive) == @".me2mod")
            {
                //RCW
                var RCWMods = RCWMod.ParseRCWMods(Path.GetFileNameWithoutExtension(archive), File.ReadAllText(archive));
                foreach (var rcw in RCWMods)
                {
                    AddCompressedModCallback(new Mod(rcw));
                }
                return;
            }

            //Embedded executables.



            var knownModsOfThisSize = TPIService.GetImportingInfosBySize(archiveSize);
            string pathOverride = null;
            if (knownModsOfThisSize.Count > 0 && knownModsOfThisSize.Any(x => x.zippedexepath != null))
            {
                //might have embedded exe
                if (archive.RepresentsFileArchive())
                {
                    // ADDED 'using' 06/04/2022 to make it dispose
                    using SevenZipExtractor sve = new SevenZipExtractor(archive);
                    string embeddedExePath = null;
                    M3Log.Information(@"This file may contain a known exe-based mod.");
                    foreach (var importingInfo in knownModsOfThisSize)
                    {
                        if (importingInfo.zippedexepath == null) continue;
                        if (sve.ArchiveFileNames.Contains(importingInfo.zippedexepath))
                        {
                            embeddedExePath = importingInfo.zippedexepath;
                            //Ensure embedded exe is supported at least by decompressed size
                            var exedata = sve.ArchiveFileData.FirstOrDefault(x => x.FileName == embeddedExePath);
                            if (exedata.FileName != null)
                            {
                                var importingInfo2 = TPIService.GetImportingInfosBySize((long)exedata.Size);
                                if (importingInfo2.Count == 0)
                                {
                                    M3Log.Warning(@"zip wrapper for this file has importing information but the embedded exe does not!");
                                    break; //no importing info
                                }

                                M3Log.Information(@"Reading embedded executable file in archive: " + embeddedExePath);
                                ActionText = M3L.GetString(M3L.string_readingZippedExecutable);
                                pathOverride = Path.Combine(MCoreFilesystem.GetTempDirectory(), Path.GetFileName(embeddedExePath));
                                using var outstream = new FileStream(pathOverride, FileMode.Create);
                                sve.Extracting += (o, pea) => { ActionText = $@"{M3L.GetString(M3L.string_readingZippedExecutable)} {pea.PercentDone}%"; };
                                sve.ExtractFile(embeddedExePath, outstream);
                                ArchiveFilePath = pathOverride; //set new path so further extraction calls use correct archive path.
                                break;
                            }
                        }
                    }
                }
            }

            void ActionTextUpdateCallback(string newText)
            {
                ActionText = newText;
            }

            void AddTextureModCallback(MEMMod memFile)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    CompressedMods.Add(memFile);
                    OnCompressedModAdded?.Invoke();
                    CompressedMods.Sort(x => x.ModName);
                });
            }

            void AddBIQCallback(BatchLibraryInstallQueue biq)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    CompressedMods.Add(biq);
                    OnCompressedModAdded?.Invoke();
                    CompressedMods.Sort(x => x.ModName);
                });
            }

            ScanFailureReason = ModArchiveInspector.FindModsInArchive(pathOverride ?? archive, AddCompressedModCallback, OnModFailedToLoad, AddTextureModCallback, AddBIQCallback, ActionTextUpdateCallback, archiveStream: ArchiveStream, forcedMD5: calculatedMD5);
        }

        private void BeginImportingMods()
        {
            var modsToExtract = CompressedMods.Where(x => x.SelectedForImport).ToList();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModExtractor");
            nbw.DoWork += ExtractModsBackgroundThread;
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null && b.Result is List<IImportableMod> modList && modList.Any(x => x is Mod))
                {
                    var pr = GetPanelResult();
                    pr.ReloadMods = true;
                    var updatedContentMods = modList.OfType<Mod>().ToList();

                    // Make sure we submit all items here - the filtering for update checks
                    // will be handled by the updater system and this must be accurate or 
                    // mod loader won't work properly since it scopes the reload
                    pr.ModsToCheckForUpdates.AddRange(updatedContentMods);

                    // If only one mod was imported, highlight it on reload
                    if (updatedContentMods.Count == 1)
                    {
                        pr.ModToHighlightOnReload = updatedContentMods[0];
                    }
                }


                Exception e = null;
                long requiredSpace = 0;
                EModImportResult result = EModImportResult.OK;
                if (b.Result is (long spaceRequired, EModImportResult res))
                {
                    result = res;
                    requiredSpace = spaceRequired;
                }
                else if (b.Result is (Exception ex, EModImportResult res3))
                {
                    e = ex; // Used in diagnostics
                    result = res3;
                }
                else if (b.Result is EModImportResult res2)
                {
                    result = res2;
                }

                switch (result)
                {
                    case EModImportResult.USER_ABORTED_IMPORT:
                        {
                            ProgressValue = 0;
                            ProgressMaximum = 100;
                            ProgressIndeterminate = false;
                            ActionText = M3L.GetString(M3L.string_selectModsToImportOrInstall);
                            CurrentState = EModArchiveImportState.SCANCOMPLETED;
                            return; //Don't do anything.
                        }
                    case EModImportResult.ERROR_COULD_NOT_DELETE_EXISTING_DIR:
                        {
                            ProgressValue = 0;
                            ProgressMaximum = 100;
                            ProgressIndeterminate = false;
                            ActionText = M3L.GetString(M3L.string_errorUnableToDeleteExistingModDirectory);
                            CurrentState = EModArchiveImportState.SCANCOMPLETED;
                            // Should this throw a dialog here...?
                            return; //Don't do anything.
                        }
                    case EModImportResult.ERROR_INSUFFICIENT_DISK_SPACE:
                        {
                            ProgressValue = 0;
                            ProgressMaximum = 100;
                            ProgressIndeterminate = false;
                            ActionText = M3L.GetString(M3L.string_insufficientDiskSpaceToExtractSelectedMods);
                            M3Utilities.DriveFreeBytes(M3LoadedMods.GetCurrentModLibraryDirectory(), out var freeSpace);
                            ErrorCallback?.Invoke(
                                M3L.GetString(M3L.string_insufficientDiskSpace),
                                M3L.GetString(M3L.string_interp_dialogNotEnoughFreeSpaceToExtract, FileSize.FormatSize(requiredSpace), FileSize.FormatSize(freeSpace)));
                            CurrentState = EModArchiveImportState.SCANCOMPLETED;
                            return; //Don't do anything.
                        }
                    case EModImportResult.ERROR_COULD_NOT_CREATE_MOD_FOLDER:
                        {
                            ProgressValue = 0;
                            ProgressMaximum = 100;
                            ProgressIndeterminate = false;
                            ActionText = M3L.GetString(M3L.string_errorExtractingArchive);
                            ErrorCallback?.Invoke(
                                M3L.GetString(M3L.string_errorExtractingArchive),
                                M3L.GetString(M3L.string_interp_errorCreatingModFolderX, e?.Message));
                            CurrentState = EModArchiveImportState.SCANCOMPLETED;
                            return; //Don't do anything.
                        }
                }

                //Close.
                ModifiedModdescFiles.AddRange(modsToExtract.OfType<Mod>().Select(x => x.ModDescPath));
                CurrentState = EModArchiveImportState.COMPLETE;
            };

            CurrentState = EModArchiveImportState.IMPORTING;
            nbw.RunWorkerAsync(modsToExtract);
        }


        private void ExtractModsBackgroundThread(object sender, DoWorkEventArgs e)
        {
            ActionText = M3L.GetString(M3L.string_preparingToImportMods);

            var mods = (List<IImportableMod>)e.Argument;
            var extractedMods = new List<IImportableMod>();

            void TextUpdateCallback(string x)
            {
                ActionText = x;
            }

            //get total size requirement
            long requiredDiskSpace = mods.Sum(x => x.SizeRequiredtoExtract);
            if (M3Utilities.DriveFreeBytes(M3LoadedMods.GetCurrentModLibraryDirectory(), out var freespaceBytes))
            {
                requiredDiskSpace = (long)(requiredDiskSpace * 1.05); //5% buffer
                M3Log.Information($@"Selected mods require: {FileSize.FormatSize(requiredDiskSpace)}");
                if ((long)freespaceBytes < requiredDiskSpace)
                {
                    M3Log.Error(@"There is not enough free space on the disk to extract these mods.");
                    M3Log.Error($@"Selected mods require: {FileSize.FormatSize(requiredDiskSpace)} | Disk space available in library partition: {FileSize.FormatSize(freespaceBytes)}");
                    e.Result = (requiredDiskSpace, EModImportResult.ERROR_INSUFFICIENT_DISK_SPACE);
                    return;
                }

            }
            else
            {
                M3Log.Error(@"Unable to get amount of free space for mod library directory disk! We will continue anyways. Path: " + M3LoadedMods.GetCurrentModLibraryDirectory());
            }


            foreach (var mod in mods)
            {
                M3Log.Information(@"Extracting mod: " + mod.ModName);
                ActionText = M3L.GetString(M3L.string_interp_extractingX, mod.ModName);
                ProgressValue = 0;
                ProgressMaximum = 100;
                ProgressIndeterminate = true;

                if (mod is BatchLibraryInstallQueue biq)
                {
                    var result = ExtractBiq(biq);
                    if (result == EModImportResult.OK)
                    {
                        ImportedBatchQueue = true;
                        extractedMods.Add(mod);
                    }
                    continue;
                }
                //Ensure directory
                var modDirectory = M3LoadedMods.GetExtractionDirectoryForMod(mod);
                var sanitizedPath = Path.Combine(modDirectory, MUtilities.SanitizePath(mod.ModName));


                if (mod is Mod && Directory.Exists(sanitizedPath))
                {
                    //Will delete on import
                    if (!AutomatedMode)
                    {
                        var result = ShowDialogCallback?.Invoke(
                            M3L.GetString(M3L.string_modAlreadyExists),
                            M3L.GetString(M3L.string_interp_dialogImportingModWillDeleteExistingMod, sanitizedPath),
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning,
                            MessageBoxResult.No);
                        if (result == MessageBoxResult.No)
                        {
                            e.Result = EModImportResult.USER_ABORTED_IMPORT;
                            return;
                        }
                    }

                    bool abort = false;
                    try
                    {
                        ActionText = M3L.GetString(M3L.string_deletingExistingModInLibrary);
                        var deletedOK = MUtilities.DeleteFilesAndFoldersRecursively(sanitizedPath);
                        if (!deletedOK)
                        {
                            M3Log.Error(@"Could not delete existing mod directory.");
                            e.Result = EModImportResult.ERROR_COULD_NOT_DELETE_EXISTING_DIR;
                            ErrorCallback?.Invoke(
                                M3L.GetString(M3L.string_errorDeletingExistingMod),
                                M3L.GetString(M3L.string_dialogErrorOccuredDeletingExistingMod));
                            abort = true;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        //I don't think this can be triggered but will leave as failsafe anyways.
                        M3Log.Error(@"Error while deleting existing output directory: " + App.FlattenException(ex));
                        ErrorCallback?.Invoke(
                            M3L.GetString(M3L.string_errorDeletingExistingMod),
                            M3L.GetString(M3L.string_interp_errorOccuredDeletingExistingModX, ex.Message));
                        e.Result = EModImportResult.ERROR_COULD_NOT_DELETE_EXISTING_DIR;
                        abort = true;
                    }

                    if (abort)
                    {
                        M3Log.Warning(@"Aborting mod import.");
                        return;
                    }
                }
                else if (mod is MEMMod m)
                {
                    // We will need to verify extraction path... somehow... we don't know the destination game yet
                }

                if (mod is Mod)
                {
                    try
                    {
                        Directory.CreateDirectory(sanitizedPath);
                    }
                    catch (Exception ex)
                    {
                        M3Log.Exception(ex, @"Error creating mod library during extraction. Telemetry shows it may be related to this issue: https://stackoverflow.com/questions/61719649/directory-createdirectory-could-not-find-file-errors:");
                        e.Result = (ex, EModImportResult.ERROR_COULD_NOT_CREATE_MOD_FOLDER);
                        return;
                    }
                }

                ActionText = M3L.GetString(M3L.string_interp_extractingX, mod.ModName);

                if (mod is Mod contentMod)
                {
                    //Check if RCW mod
                    if (contentMod.InstallationJobs.Count == 1 &&
                        contentMod.InstallationJobs[0].Header == ModJob.JobHeader.ME2_RCWMOD)
                    {
                        M3Log.Information(@"Generating M3 wrapper moddesc.ini for " + mod.ModName);
                        contentMod.ExtractRCWModToM3LibraryMod(sanitizedPath);
                        extractedMods.Add(mod);
                        continue;
                    }
                }
                try
                {
                    mod.ExtractFromArchive(ArchiveFilePath, sanitizedPath, CompressPackages, TextUpdateCallback, ExtractionProgressCallback, CompressedPackageCallback, false, ArchiveStream, SourceNXMLink);
                    if (mod is MEMMod mmod)
                    {
                        // Determine which game
                        mmod.Game = ModFileFormats.GetGameMEMFileIsFor(mmod.ExtractedPath);
                        ImportedLETextureMod |= mmod.Game.IsLEGame();
                        ImportedOTTextureMod |= mmod.Game.IsOTGame();
                    }
                }
                catch (Exception ex)
                {
                    //Extraction failed!
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        M3Log.Error(@"Error while extracting archive: " + App.FlattenException(ex));
                        ErrorCallback?.Invoke(
                            M3L.GetString(M3L.string_errorExtractingArchive),
                            M3L.GetString(M3L.string_interp_anErrorOccuredExtractingTheArchiveX, ex.Message));
                        e.Result = EModImportResult.ERROR_EXTRACTING_ARCHIVE;
                    });
                    return;
                }

                extractedMods.Add(mod);
            }
            e.Result = extractedMods;
        }

        private EModImportResult ExtractBiq(BatchLibraryInstallQueue biq)
        {
            // Either that or sanitize biq.ModName for filesystem use
            var destPath = Path.Combine(M3LoadedMods.GetBatchInstallGroupsDirectory(), MUtilities.SanitizePath(biq.ModName) + BatchLibraryInstallQueue.QUEUE_VERSION_BIQ2_EXTENSION);
            if (!AutomatedMode)
            {
                if (File.Exists(destPath))
                {
                    var result = ShowDialogCallback?.Invoke(
                        M3L.GetString(M3L.string_installGroupAlreadyExists),
                        M3L.GetString(M3L.string_dialog_biqAlreadyExists, biq.ModName),
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Warning,
                        MessageBoxResult.Cancel);
                    if (result == MessageBoxResult.Cancel)
                    {
                        return EModImportResult.USER_ABORTED_IMPORT;
                    }
                }
            }

            M3Log.Information($@"Writing batch installer group to {destPath}");
            File.WriteAllText(destPath, biq.BiqTextForExtraction);
            return EModImportResult.OK;
        }

        private void ExtractionProgressCallback(DetailedProgressEventArgs args)
        {
            // Debug.WriteLine("Extraction progress " + args.AmountCompleted + "/" + args.TotalAmount);
            ProgressValue = (long)args.AmountCompleted;
            ProgressMaximum = (long)args.TotalAmount;
            ProgressIndeterminate = ProgressValue == 0;
        }

        private void CompressedPackageCallback(string activityString, int numDone, int numToDo)
        {
            //progress for compression
            if (ProgressValue >= ProgressMaximum)
            {
                ActionText = activityString;
            }
            CompressionProgressMaximum = numToDo;
            CompressionProgressValue = numDone;
        }

        /// <summary>
        /// Class for exe-file extraction transformations
        /// </summary>
        public class ExeTransform
        {
            public ExeTransform(string xml)
            {
                var doc = XDocument.Parse(xml);
                VPatches.ReplaceAll(doc.Root.Elements(@"vpatch")
                    .Select(d => new VPatchDirective
                    {
                        inputfile = (string)d.Attribute(@"inputfile"),
                        outputfile = (string)d.Attribute(@"outputfile"),
                        patchfile = (string)d.Attribute(@"patchfile")
                    }).ToList());
                PatchRedirects.ReplaceAll(doc.Root.Elements(@"patchredirect")
                    .Select(d => ((int)d.Attribute(@"index"), (string)d.Attribute(@"outfile"))).ToList());

                AlternateRedirects.ReplaceAll(doc.Root.Elements(@"alternateredirect")
                    .Select(d => ((int)d.Attribute(@"index"), (string)d.Attribute(@"outfile"))).ToList());

                NoExtractIndexes.ReplaceAll(doc.Root.Elements(@"noextract")
                    .Select(d => (int)d.Attribute(@"index")).ToList());

                CopyFiles.ReplaceAll(doc.Root.Elements(@"copyfile")
                    .Select(d => new CopyFile()
                    {
                        inputfile = (string)d.Attribute(@"source"),
                        outputfile = (string)d.Attribute(@"destination"),
                    }).ToList());

                var postTransform = doc.Root.Elements(@"posttransformmoddesc");
                if (postTransform.Count() == 1)
                {
                    PostTransformModdesc = (string)postTransform.First();
                }
            }
            public List<VPatchDirective> VPatches = new List<VPatchDirective>();
            public List<CopyFile> CopyFiles = new List<CopyFile>();
            public List<(int index, string outfile)> PatchRedirects = new List<(int index, string outfile)>();
            public List<(int index, string outfile)> AlternateRedirects = new List<(int index, string outfile)>();
            public List<int> NoExtractIndexes = new List<int>();

            public string PostTransformModdesc { get; internal set; }

            public class VPatchDirective
            {
                public string inputfile;
                public string outputfile;
                public string patchfile;
            }

            public class CopyFile
            {
                public string inputfile;
                public string outputfile;
            }
        }



        public void BeginScan()
        {
            InspectArchiveFile();
        }

        public void BeginImporting()
        {
            BeginImportingMods();
        }

        public static void ImportTextureFiles(List<string> memFiles, MEGame memGame = MEGame.Unknown, BackgroundTask task = null)
        {
            foreach (var memFile in memFiles)
            {
                var game = memGame != MEGame.Unknown ? memGame : ModFileFormats.GetGameMEMFileIsFor(memFile);
                var tml = M3LoadedMods.GetTextureLibraryDirectory(game);
                var destPath = Path.Combine(tml, Path.GetFileName(memFile));
                M3Log.Information($@"Importing {memFile} to {destPath}");
                if (task != null)
                {
                    BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task, M3L.GetString(M3L.string_interp_importingX, Path.GetFileName(memFile)));
                }
                File.Copy(memFile, destPath, true);
            }
        }
    }
}
