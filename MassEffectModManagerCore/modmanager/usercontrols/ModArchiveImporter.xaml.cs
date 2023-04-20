using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Xml.Linq;
using IniParser.Model;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.gameini;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.importer;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.interfaces;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using ME3TweaksModManager.ui;
using SevenZip;
using SevenZip.EventArguments;
using M3OnlineContent = ME3TweaksModManager.modmanager.me3tweaks.services.M3OnlineContent;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModArchiveImporter.xaml
    /// </summary>
    public partial class ModArchiveImporter : MMBusyPanelBase
    {
        public string CancelButtonText { get; set; } = M3L.GetString(M3L.string_cancel);

        public bool TaskRunning { get; private set; }
        public string NoModSelectedText { get; set; } = M3L.GetString(M3L.string_selectAModOnTheLeftToViewItsDescription);
        public bool ArchiveScanned { get; set; }
        public bool TextureFilesImported { get; set; }

        // LE games do not even show this option
        public bool CanShowCompressPackages => CompressedMods.Any(x => x is Mod m && m.Game.IsOTGame());
        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanCancel())
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public bool CompressPackages { get; set; }

        public int CompressionProgressValue { get; set; }
        public int CompressionProgressMaximum { get; set; } = 100;

        public Mod SelectedMod { get; private set; }


        private string ArchiveFilePath;
        /// <summary>
        /// Stream containing the archive
        /// </summary>
        public Stream ArchiveStream { get; set; }

        public string ScanningFile { get; private set; } = M3L.GetString(M3L.string_pleaseWait);
        public string ActionText { get; private set; }
        public long ProgressValue { get; private set; }
        public long ProgressMaximum { get; private set; }
        public bool ProgressIndeterminate { get; private set; }

        // Must be ME2 or ME3, cannot have a transform, we allow it, archive has been scanned, we haven't started an operation
        // Mods that use the updater service cannot be compressed to ensure the update checks are reliable
        // Excludes Legendary Edition games.
        public bool CanCompressPackages => CompressedMods.Any(x => x is Mod m && m.Game is MEGame.ME2 or MEGame.ME3) && CompressedMods.All(x => x is Mod m && m.ExeExtractionTransform == null && m.ModClassicUpdateCode == 0) && App.AllowCompressingPackagesOnImport && ArchiveScanned && !TaskRunning;
        /// <summary>
        /// List of mods listed in the importer panel
        /// </summary>
        public ObservableCollectionExtended<IImportableMod> CompressedMods { get; } = new();
        public ModArchiveImporter(string file, Stream archiveStream = null)
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem($@"Mod Archive Importer ({Path.GetFileName(file)})", this);
            ArchiveFilePath = file;
            ArchiveStream = archiveStream;
            LoadCommands();
        }


        /// <summary>
        /// Begins inspection of archive file. This method will spawn a background thread that will
        /// run asynchronously.
        /// </summary>
        /// <param name="filepath">Path to the archive file. If the filepath is virtual, just pass the filename instead.</param>
        private void InspectArchiveFile(string filepath)
        {
            ScanningFile = Path.GetFileName(filepath);
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModArchiveInspector");
            nbw.DoWork += InspectArchiveBackgroundThread;
            ProgressValue = 0;
            ProgressMaximum = 100;
            ProgressIndeterminate = true;

            nbw.RunWorkerCompleted += (a, b) =>
            {
                ActionText = null;
                M3Log.Information(@"Archive scan thread exited");

                if (CompressedMods.Count > 0)
                {
                    ActionText = M3L.GetString(M3L.string_selectModsToImportIntoModManagerLibrary);
                    if (CompressedMods.Count == 1)
                    {
                        CompressedMods_ListBox.SelectedIndex = 0; //Select the only item
                    }

                    ArchiveScanned = true;
                    TriggerPropertyChangedFor(nameof(CanCompressPackages));
                    TriggerPropertyChangedFor(nameof(CanShowCompressPackages));
                }
                else if (TextureFilesImported)
                {
                    CancelButtonText = M3L.GetString(M3L.string_close);
                    NoModSelectedText = M3L.GetString(M3L.string_interp_dialogImportedALOTMainToTextureLibrary,
                        ScanningFile, M3Utilities.GetALOTInstallerTextureLibraryDirectory());
                    ActionText = M3L.GetString(M3L.string_importCompleted);
                }
                else
                {
                    ActionText = M3L.GetString(M3L.string_noCompatibleModsFoundInArchive);
                    if (filepath.EndsWith(@".exe"))
                    {
                        NoModSelectedText = M3L.GetString(M3L.string_executableModsMustBeValidatedByME3Tweaks);
                    }
                    else
                    {
                        if (Settings.GenerationSettingLE && !Settings.GenerationSettingOT)
                        {
                            // Show LE string
                            NoModSelectedText = M3L.GetString(M3L.string_noCompatibleModsFoundInArchiveLEExtended);
                        }
                        else if (!Settings.GenerationSettingLE && Settings.GenerationSettingOT)
                        {
                            // Show OT string
                            NoModSelectedText = M3L.GetString(M3L.string_noCompatibleModsFoundInArchiveOTExtended);
                        }
                        else
                        {
                            // Show combined string
                            NoModSelectedText = M3L.GetString(M3L.string_noCompatibleModsFoundInArchiveBothGensExtended);
                        }
                    }
                }

                ProgressValue = 0;
                ProgressIndeterminate = false;
                TaskRunning = false;
                CommandManager.InvalidateRequerySuggested();

                var hasAnyImproperlyPackedMods = CompressedMods.Any(x => x is Mod { CheckDeployedWithM3: true, DeployedWithM3: false });

                if (hasAnyImproperlyPackedMods)
                {
                    TelemetryInterposer.TrackEvent(@"Detected improperly packed M3 mod v2", new Dictionary<string, string>()
                    {
                        {@"Archive name", Path.GetFileName(filepath)}
                    });
                    M3Log.Error(@"A mod in the archive was not deployed using M3 and targets 6.0 or higher! You should contact the developer and tell them to deploy it properly.");
                    M3L.ShowDialog(Window.GetWindow(this),
                        M3L.GetString(M3L.string_dialog_improperlyDeployedMod),
                        M3L.GetString(M3L.string_improperlyDeployedMod), MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            };
            ActionText = M3L.GetString(M3L.string_interp_scanningX, Path.GetFileName(filepath));
            nbw.RunWorkerAsync(filepath);
        }


        ///// <summary>
        ///// Notifies listeners when given property is updated.
        ///// </summary>
        ///// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        //protected virtual void hack_NotifyPropertyChanged([CallerMemberName] string propertyname = null)
        //{
        //    hack_PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        //}
        private bool openedMultipanel = false;

        /// <summary>
        /// Inspects an 'archive' file. Archives may contain one or more mods (or none).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InspectArchiveBackgroundThread(object sender, DoWorkEventArgs e)
        {
            TaskRunning = true;
            TriggerPropertyChangedFor(nameof(CanCompressPackages));
            ActionText = M3L.GetString(M3L.string_interp_openingX, ScanningFile);

            var archive = e.Argument as string;

            M3Log.Information($@"Scanning archive for mods: {archive}");
            void AddCompressedModCallback(Mod m)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    CompressedMods.Add(m);
                    if (CompressedMods.Count > 1 && !openedMultipanel)
                    {
                        Storyboard sb = FindResource(@"OpenWebsitePanel") as Storyboard;
                        if (sb.IsSealed)
                        {
                            sb = sb.Clone();
                        }
                        Storyboard.SetTarget(sb, MultipleModsPopupPanel);
                        sb.Begin();
                        openedMultipanel = true;
                    }
                    CompressedMods.Sort(x => x.ModName);
                });
            }
            void CompressedModFailedCallback(Mod m)
            {
                Application.Current.Dispatcher.Invoke(delegate { NoModSelectedText += M3L.GetString(M3L.string_interp_XfailedToLoadY, m.ModName, m.LoadFailedReason); });
            }

            var archiveSize = ArchiveStream?.Length ?? new FileInfo(archive).Length;

            // ModManager 8: Blacklisting files by size/hash
            string calculatedMD5 = null; // If we calc it here don't calc it later

            var blacklistings = BlacklistingService.GetBlacklistings(archiveSize);
            if (blacklistings.Any())
            {
                calculatedMD5 = ArchiveStream != null ? MUtilities.CalculateHash(ArchiveStream) : MUtilities.CalculateHash(archive);
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
                                pathOverride = Path.Combine(M3Filesystem.GetTempPath(), Path.GetFileName(embeddedExePath));
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

            void ShowALOTLauncher()
            {
                TextureFilesImported = true;
            }

            void AddTextureModCallback(MEMMod memFile)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    CompressedMods.Add(memFile);
                    if (CompressedMods.Count > 1 && !openedMultipanel)
                    {
                        Storyboard sb = FindResource(@"OpenWebsitePanel") as Storyboard;
                        if (sb.IsSealed)
                        {
                            sb = sb.Clone();
                        }
                        Storyboard.SetTarget(sb, MultipleModsPopupPanel);
                        sb.Begin();
                        openedMultipanel = true;
                    }
                    CompressedMods.Sort(x => x.ModName);
                });
            }

            ModImport.FindModsInArchive(pathOverride ?? archive, AddCompressedModCallback, CompressedModFailedCallback, AddTextureModCallback, ActionTextUpdateCallback, ShowALOTLauncher, archiveStream: ArchiveStream, forcedMD5: calculatedMD5);
        }

        protected override void OnClosing(DataEventArgs args)
        {
            if (ArchiveStream is FileStream fs)
            {
                // Memorystream does not need disposed
                fs.Dispose();
            }
            base.OnClosing(args);
        }

        private void BeginImportingMods()
        {
            var modsToExtract = CompressedMods.Where(x => x.SelectedForImport).ToList();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModExtractor");
            nbw.DoWork += ExtractModsBackgroundThread;
            nbw.RunWorkerCompleted += (a, b) =>
            {
                TaskRunning = false;
                if (b.Error == null && b.Result is List<IImportableMod> modList && modList.Any(x=>x is Mod))
                {
                    Result.ReloadMods = true;
                    var updatedContentMods = modList.OfType<Mod>().ToList();

                    // Make sure we submit all items here - the filtering for update checks
                    // will be handled by the updater system and this must be accurate or 
                    // mod loader won't work properly since it scopes the reload
                    Result.ModsToCheckForUpdates.AddRange(updatedContentMods);

                    // If only one mod was imported, highlight it on reload
                    if (updatedContentMods.Count == 1)
                    {
                        Result.ModToHighlightOnReload = updatedContentMods[0];
                    }
                }


                Exception e = null;
                long requiredSpace = 0;
                ModImportResult result = ModImportResult.None;
                if (b.Result is (long spaceRequired, ModImportResult res))
                {
                    result = res;
                    requiredSpace = spaceRequired;
                }
                else if (b.Result is (Exception ex, ModImportResult res3))
                {
                    e = ex; // Used in diagnostics
                    result = res3;
                }
                else if (b.Result is ModImportResult res2)
                {
                    result = res2;
                }

                switch (result)
                {
                    case ModImportResult.USER_ABORTED_IMPORT:
                        {
                            ProgressValue = 0;
                            ProgressMaximum = 100;
                            ProgressIndeterminate = false;
                            ActionText = M3L.GetString(M3L.string_selectModsToImportOrInstall);
                            return; //Don't do anything.
                        }
                    case ModImportResult.ERROR_COULD_NOT_DELETE_EXISTING_DIR:
                        {
                            ProgressValue = 0;
                            ProgressMaximum = 100;
                            ProgressIndeterminate = false;
                            ActionText = M3L.GetString(M3L.string_errorUnableToDeleteExistingModDirectory);
                            return; //Don't do anything.
                        }
                    case ModImportResult.ERROR_INSUFFICIENT_DISK_SPACE:
                        {
                            ProgressValue = 0;
                            ProgressMaximum = 100;
                            ProgressIndeterminate = false;
                            ActionText = M3L.GetString(M3L.string_insufficientDiskSpaceToExtractSelectedMods);
                            M3Utilities.DriveFreeBytes(M3LoadedMods.GetCurrentModLibraryDirectory(), out var freeSpace);
                            M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialogNotEnoughFreeSpaceToExtract, FileSize.FormatSize(requiredSpace), FileSize.FormatSize(freeSpace)), M3L.GetString(M3L.string_insufficientDiskSpace), MessageBoxButton.OK, MessageBoxImage.Error);
                            return; //Don't do anything.
                        }
                    case ModImportResult.ERROR_COULD_NOT_CREATE_MOD_FOLDER:
                        {
                            ProgressValue = 0;
                            ProgressMaximum = 100;
                            ProgressIndeterminate = false;
                            ActionText = M3L.GetString(M3L.string_errorExtractingArchive);
                            M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_errorCreatingModFolderX, e?.Message), M3L.GetString(M3L.string_errorExtractingArchive), MessageBoxButton.OK, MessageBoxImage.Error);
                            return; //Don't do anything.
                        }
                }
                //Close.
                OnClosing(DataEventArgs.Empty);
            };
            TaskRunning = true;
            TriggerPropertyChangedFor(nameof(CanCompressPackages));
            nbw.RunWorkerAsync(modsToExtract);
        }

        private void ExtractModsBackgroundThread(object sender, DoWorkEventArgs e)
        {
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
                    e.Result = (requiredDiskSpace, ModImportResult.ERROR_INSUFFICIENT_DISK_SPACE);
                    return;
                }

            }
            else
            {
                M3Log.Error(@"Unable to get amount of free space for mod library directory disk! We will continue anyways. Path: " + M3LoadedMods.GetCurrentModLibraryDirectory());
            }


            foreach (var mod in mods)
            {
                //Todo: Extract files
                M3Log.Information(@"Extracting mod: " + mod.ModName);
                ActionText = M3L.GetString(M3L.string_interp_extractingX, mod.ModName);
                ProgressValue = 0;
                ProgressMaximum = 100;
                ProgressIndeterminate = true;
                //Ensure directory
                var modDirectory = M3LoadedMods.GetExtractionDirectoryForMod(mod);
                var sanitizedPath = Path.Combine(modDirectory, M3Utilities.SanitizePath(mod.ModName));


                if (mod is Mod && Directory.Exists(sanitizedPath))
                {
                    //Will delete on import
                    bool abort = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var result = M3L.ShowDialog(Window.GetWindow(this),
                            M3L.GetString(M3L.string_interp_dialogImportingModWillDeleteExistingMod, sanitizedPath),
                            M3L.GetString(M3L.string_modAlreadyExists), MessageBoxButton.YesNo, MessageBoxImage.Warning,
                            MessageBoxResult.No);
                        if (result == MessageBoxResult.No)
                        {
                            e.Result = ModImportResult.USER_ABORTED_IMPORT;
                            abort = true;
                            return;
                        }
                    });
                    if (abort)
                        return;

                    try
                    {
                        ActionText = M3L.GetString(M3L.string_deletingExistingModInLibrary);
                        var deletedOK = M3Utilities.DeleteFilesAndFoldersRecursively(sanitizedPath);
                        if (!deletedOK)
                        {
                            M3Log.Error(@"Could not delete existing mod directory.");
                            e.Result = ModImportResult.ERROR_COULD_NOT_DELETE_EXISTING_DIR;
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_dialogErrorOccuredDeletingExistingMod), M3L.GetString(M3L.string_errorDeletingExistingMod), MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                            abort = true;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        //I don't think this can be triggered but will leave as failsafe anyways.
                        M3Log.Error(@"Error while deleting existing output directory: " + App.FlattenException(ex));
                        Application.Current.Dispatcher.Invoke(
                            () =>
                            {
                                M3L.ShowDialog(Window.GetWindow(this),
                                    M3L.GetString(M3L.string_interp_errorOccuredDeletingExistingModX, ex.Message),
                                    M3L.GetString(M3L.string_errorDeletingExistingMod), MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            });
                        e.Result = ModImportResult.ERROR_COULD_NOT_DELETE_EXISTING_DIR;
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
                        M3Log.Exception(ex,
                            @"Error creating mod library during extraction. Telemetry shows it may be related to this issue: https://stackoverflow.com/questions/61719649/directory-createdirectory-could-not-find-file-errors:");
                        e.Result = (ex, ModImportResult.ERROR_COULD_NOT_CREATE_MOD_FOLDER);
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
                    mod.ExtractFromArchive(ArchiveFilePath, sanitizedPath, CompressPackages, TextUpdateCallback, ExtractionProgressCallback, CompressedPackageCallback, false, ArchiveStream);
                }
                catch (Exception ex)
                {
                    //Extraction failed!
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        M3Log.Error(@"Error while extracting archive: " + App.FlattenException(ex));
                        M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_anErrorOccuredExtractingTheArchiveX, ex.Message), M3L.GetString(M3L.string_errorExtractingArchive), MessageBoxButton.OK, MessageBoxImage.Error);
                        e.Result = ModImportResult.ERROR_EXTRACTING_ARCHIVE;
                    });
                    return;
                }

                extractedMods.Add(mod);
            }
            e.Result = extractedMods;
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

        private void ExtractionProgressCallback(DetailedProgressEventArgs args)
        {
            //Debug.WriteLine("Extraction progress " + args.AmountCompleted + "/" + args.TotalAmount);
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

        public ICommand ImportModsCommand { get; set; }
        public ICommand CancelCommand { get; set; }
        public ICommand InstallModCommand { get; set; }
        public ICommand SelectAllCommand { get; set; }
        public ICommand UnselectAllCommand { get; set; }

        public string InstallModText
        {
            get
            {
                if (SelectedMod != null)
                {
                    if (SelectedMod.ExeExtractionTransform != null)
                    {
                        return M3L.GetString(M3L.string_exeModsMustBeImportedBeforeInstall);
                    }
                    return M3L.GetString(M3L.string_interp_installX, SelectedMod.ModName);
                }

                return M3L.GetString(M3L.string_install);
            }
        }


        private void LoadCommands()
        {
            ImportModsCommand = new GenericCommand(BeginImportingMods, CanImportMods);
            CancelCommand = new GenericCommand(Cancel, CanCancel);
            InstallModCommand = new GenericCommand(InstallCompressedMod, CanInstallCompressedMod);
            UnselectAllCommand = new GenericCommand(() => checkAll(false), CanCancel);
            SelectAllCommand = new GenericCommand(() => checkAll(true), CanCancel);
        }

        public enum ModImportResult
        {
            USER_ABORTED_IMPORT, ERROR_COULD_NOT_DELETE_EXISTING_DIR,
            ERROR_INSUFFICIENT_DISK_SPACE,
            None,
            ERROR_EXTRACTING_ARCHIVE,
            ERROR_COULD_NOT_CREATE_MOD_FOLDER
        }

        private bool CanInstallCompressedMod()
        {
            //This will have to pass some sort of validation code later.
            return IsPanelOpen && CompressedMods_ListBox != null
                               && CompressedMods_ListBox.SelectedItem is Mod cm
                               && cm.ExeExtractionTransform == null
                               && cm.ValidMod
                               && !TaskRunning /*&& !CompressPackages*/
                               && mainwindow != null // Might happen if app is closing or panel closed?
                               && mainwindow.InstallationTargets.Any(x => x.Game == cm.Game);
        }

        private void InstallCompressedMod()
        {
            OnClosing(new DataEventArgs((CompressedMods_ListBox.SelectedItem, CompressPackages)));
        }

        private void Cancel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanCancel() => !TaskRunning;

        private bool CanImportMods() => !TaskRunning && CompressedMods.Any(x => x.SelectedForImport && x.ValidMod);

        private void SelectedMod_Changed(object sender, SelectionChangedEventArgs e)
        {
            SelectedMod = CompressedMods_ListBox.SelectedItem as Mod;
            if (SelectedMod != null && SelectedMod.Game > MEGame.ME1 && SelectedMod.PreferCompressed)
            {
                CompressPackages = true;
            }

            if (SelectedMod != null && SelectedMod.Game == MEGame.ME1)
            {
                CompressPackages = false;
            }

        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            InspectArchiveFile(ArchiveFilePath);
        }

        private void checkAll(bool check)
        {
            foreach (var mod in CompressedMods)
            {
                mod.SelectedForImport = check;
            }
        }
    }
}
