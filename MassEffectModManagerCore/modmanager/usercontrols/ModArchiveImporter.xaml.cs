using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.helpers;
using SevenZip;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using SevenZip.EventArguments;
using MassEffectModManagerCore.modmanager.gameini;
using System.Windows.Media.Animation;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.objects.mod;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.diagnostics;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using Microsoft.AppCenter.Analytics;
using MemoryAnalyzer = MassEffectModManagerCore.modmanager.memoryanalyzer.MemoryAnalyzer;

namespace MassEffectModManagerCore.modmanager.usercontrols
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
        public bool CanCompressPackages => CompressedMods.Any(x => x.Game is MEGame.ME2 or MEGame.ME3) && CompressedMods.All(x => x.ExeExtractionTransform == null && x.ModClassicUpdateCode == 0) && App.AllowCompressingPackagesOnImport && ArchiveScanned && !TaskRunning;

        public ObservableCollectionExtended<Mod> CompressedMods { get; } = new ObservableCollectionExtended<Mod>();
        public ModArchiveImporter(string file, Stream archiveStream = null)
        {
            MemoryAnalyzer.AddTrackedMemoryItem($@"Mod Archive Importer ({Path.GetFileName(file)})", new WeakReference(this));
            ArchiveFilePath = file;
            ArchiveStream = archiveStream;
            LoadCommands();
            InitializeComponent();
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
                if (b.Error != null)
                {
                    M3Log.Error($@"Exception occurred in {nbw.Name} thread:");
                    M3Log.Error(b.Error.StackTrace);
                }

                if (CompressedMods.Count > 0)
                {
                    ActionText = M3L.GetString(M3L.string_selectModsToImportIntoModManagerLibrary);
                    if (CompressedMods.Count == 1)
                    {
                        CompressedMods_ListBox.SelectedIndex = 0; //Select the only item
                    }

                    ArchiveScanned = true;
                    TriggerPropertyChangedFor(nameof(CanCompressPackages));
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
                        NoModSelectedText = M3L.GetString(M3L.string_noCompatibleModsFoundInArchiveExtended);
                    }
                }

                ProgressValue = 0;
                ProgressIndeterminate = false;
                TaskRunning = false;
                CommandManager.InvalidateRequerySuggested();

                var hasAnyImproperlyPackedMods =
                    CompressedMods.Any(x => x.CheckDeployedWithM3 && !x.DeployedWithM3);

                if (hasAnyImproperlyPackedMods)
                {
                    Analytics.TrackEvent(@"Detected improperly packed M3 mod v2", new Dictionary<string, string>()
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
            var archiveSize = ArchiveStream != null ? ArchiveStream.Length : new FileInfo(archive).Length;
            var knownModsOfThisSize = TPIService.GetImportingInfosBySize(archiveSize);
            string pathOverride = null;
            if (knownModsOfThisSize.Count > 0 && knownModsOfThisSize.Any(x => x.zippedexepath != null))
            {
                //might have embedded exe
                if (archive.RepresentsFileArchive())
                {
                    SevenZipExtractor sve = new SevenZipExtractor(archive);
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
                                pathOverride = Path.Combine(M3Utilities.GetTempPath(), Path.GetFileName(embeddedExePath));
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

            // Telemetry data to help find source of mods
            // This should only run if we need to somehow look up source, like if mod is not in TPMI
            /*try
            {
                // note: This currently doesn't do anything as it just parses it out. It doesn't actually send anything or use the results of it
                if (Settings.EnableTelemetry && archive != null)
                {
                    FileInfo fi = new FileInfo(archive);
                    if (fi.AlternateDataStreamExists(@"Zone.Identifier"))
                    {
                        var s = fi.GetAlternateDataStream(@"Zone.Identifier", FileMode.Open);
                        string fullText = string.Empty;
                        using var reader = s.OpenText();
                        fullText = string.Format(reader.ReadToEnd());
                        // The Zone Identifier is an ini file
                        try
                        {
                            DuplicatingIni ini = DuplicatingIni.ParseIni(fullText);
                            var zoneId = ini[@"ZoneTransfer"][@"ZoneId"]?.Value;
                            if (zoneId == @"3")
                            {
                                // File came from internet
                                // Get the download url. We can identify which mod on nexus this is by it's CDN scheme
                                var hostUrl = ini[@"ZoneTransfer"][@"HostUrl"]?.Value;
                                if (hostUrl != null)
                                {
                                    // Grab the pre-calculated MD5.
                                    // Make sure to NOT read any other parameters - they contain sensitive info!
                                    var uri = new Uri(hostUrl);
                                    var downloadLinkSanitized = $@"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";

                                    var parameters = HttpUtility.ParseQueryString(uri.Query);
                                    string nexusMd5 = parameters[@"md5"];
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                M3Log.Error($@"Error doing preinspection, it will be skipped. Error: {ex.Message}");
            }*/

            void ActionTextUpdateCallback(string newText)
            {
                ActionText = newText;
            }

            void ShowALOTLauncher()
            {
                TextureFilesImported = true;
            }
            InspectArchive(pathOverride ?? archive, AddCompressedModCallback, CompressedModFailedCallback, ActionTextUpdateCallback, ShowALOTLauncher, archiveStream: ArchiveStream);
        }

        //this should be private but no way to test it private for now...

        /// <summary>
        /// Inspects and loads compressed mods from an archive.
        /// </summary>
        /// <param name="filepath">Path of the archive</param>
        /// <param name="addCompressedModCallback">Callback indicating that the mod should be added to the collection of found mods</param>
        /// <param name="currentOperationTextCallback">Callback to tell caller what's going on'</param>
        /// <param name="forcedOverrideData">Override data about archive. Used for testing only</param>
        public static void InspectArchive(string filepath, Action<Mod> addCompressedModCallback = null, Action<Mod> failedToLoadModeCallback = null, Action<string> currentOperationTextCallback = null,
            Action showALOTLauncher = null, string forcedMD5 = null, int forcedSize = -1, Stream archiveStream = null)
        {
            string relayVersionResponse = @"-1";
            List<Mod> internalModList = new List<Mod>(); //internal mod list is for this function only so we don't need a callback to get our list since results are returned immediately
            var isExe = filepath.EndsWith(@".exe");
            SevenZipExtractor archiveFile = null;

            bool closeStreamOnComplete = true;
            if (archiveStream != null)
            {
                closeStreamOnComplete = false;
                archiveStream.Position = 0;
                archiveFile = isExe ? new SevenZipExtractor(archiveStream, InArchiveFormat.Nsis) : new SevenZipExtractor(archiveStream);
                archiveFile.SetFilename(filepath);
            }
            else
            {
                archiveFile = isExe ? new SevenZipExtractor(filepath, InArchiveFormat.Nsis) : new SevenZipExtractor(filepath);
            }
#if DEBUG
            foreach (var v in archiveFile.ArchiveFileData)
            {
                Debug.WriteLine($@"{v.FileName} | Index {v.Index} | Size {v.Size} | Method {v.Method} | IsDirectory {v.IsDirectory} | Last Modified {v.LastWriteTime}");
            }
#endif
            var moddesciniEntries = new List<ArchiveFileInfo>();
            var sfarEntries = new List<ArchiveFileInfo>(); //ME3 DLC
            var bioengineEntries = new List<ArchiveFileInfo>(); //ME2 DLC
            var me2mods = new List<ArchiveFileInfo>(); //ME2 RCW Mods
            var textureModEntries = new List<ArchiveFileInfo>(); //TPF MEM MOD files
            bool isAlotFile = false;
            try
            {
                foreach (var entry in archiveFile.ArchiveFileData)
                {
                    if (!entry.IsDirectory)
                    {
                        string fname = Path.GetFileName(entry.FileName);
                        if (fname.Equals(@"ALOTInstaller.exe", StringComparison.InvariantCultureIgnoreCase))
                        {
                            isAlotFile = true;
                        }
                        else if (fname.Equals(@"moddesc.ini", StringComparison.InvariantCultureIgnoreCase))
                        {
                            moddesciniEntries.Add(entry);
                        }
                        else if (fname.Equals(@"Default.sfar", StringComparison.InvariantCultureIgnoreCase))
                        {
                            //for unofficial lookups
                            sfarEntries.Add(entry);
                        }
                        else if (fname.Equals(@"BIOEngine.ini", StringComparison.InvariantCultureIgnoreCase))
                        {
                            //for unofficial lookups [NOT USED]
                            bioengineEntries.Add(entry);
                        }
                        else if (Path.GetExtension(fname) == @".me2mod")
                        {
                            me2mods.Add(entry);
                        }
                        else if (Path.GetExtension(fname) == @".mem" || Path.GetExtension(fname) == @".tpf" || Path.GetExtension(fname) == @".mod")
                        {
                            //for forwarding to ALOT Installer
                            textureModEntries.Add(entry);
                        }
                    }
                }
            }
            catch (SevenZipArchiveException svae)
            {
                //error reading archive!
                Mod failed = new Mod(false);
                failed.ModName = M3L.GetString(M3L.string_archiveError);
                failed.LoadFailedReason = M3L.GetString(M3L.string_couldNotInspectArchive7zException);
                M3Log.Error($@"Unable to inspect archive {filepath}: SevenZipException occurred! It may be corrupt. The specific error was: {svae.Message}");
                failedToLoadModeCallback?.Invoke(failed);
                addCompressedModCallback?.Invoke(failed);
                if (closeStreamOnComplete)
                {
                    archiveFile?.Dispose();
                }
                else
                {
                    archiveFile?.DisposeObjectOnly();

                }
                return;
            }

            // Used for TPIS information lookup
            long archiveSize = forcedSize > 0 ? forcedSize : archiveStream != null ? archiveStream.Length : new FileInfo(filepath).Length;

            if (moddesciniEntries.Count > 0)
            {
                foreach (var entry in moddesciniEntries)
                {
                    currentOperationTextCallback?.Invoke(M3L.GetString(M3L.string_interp_readingX, entry.FileName));
                    Mod m = new Mod(entry, archiveFile);
                    if (!m.ValidMod)
                    {
                        failedToLoadModeCallback?.Invoke(m);
                        m.SelectedForImport = false;
                    }

                    addCompressedModCallback?.Invoke(m);
                    internalModList.Add(m);
                }
            }
            else if (me2mods.Count > 0)
            {
                //found some .me2mod files.
                foreach (var entry in me2mods)
                {
                    currentOperationTextCallback?.Invoke(M3L.GetString(M3L.string_interp_readingX, entry.FileName));
                    MemoryStream ms = new MemoryStream();
                    archiveFile.ExtractFile(entry.Index, ms);
                    ms.Position = 0;
                    StreamReader reader = new StreamReader(ms);
                    string text = reader.ReadToEnd();
                    var rcwModsForFile = RCWMod.ParseRCWMods(Path.GetFileNameWithoutExtension(entry.FileName), text);
                    foreach (var rcw in rcwModsForFile)
                    {
                        Mod m = new Mod(rcw);
                        addCompressedModCallback?.Invoke(m);
                        internalModList.Add(m);
                    }
                }
            }
            else if (Enumerable.Any(textureModEntries) && isAlotFile)
            {
                if (isAlotFile)
                {
                    //is alot installer
                    M3Log.Information(@"This file contains texture files and ALOTInstaller.exe - this is an ALOT main file");
                    var textureLibraryPath = M3Utilities.GetALOTInstallerTextureLibraryDirectory();
                    if (textureLibraryPath != null)
                    {
                        //we have destination
                        var destPath = Path.Combine(textureLibraryPath, Path.GetFileName(filepath));
                        if (!File.Exists(destPath))
                        {
                            M3Log.Information(M3L.GetString(M3L.string_thisFileIsNotInTheTextureLibraryMovingItToTheTextureLibrary));
                            currentOperationTextCallback?.Invoke(M3L.GetString(M3L.string_movingALOTFileToTextureLibraryPleaseWait));
                            archiveFile.Dispose();
                            File.Move(filepath, destPath, true);
                            showALOTLauncher?.Invoke();
                        }
                    }
                }
                //todo: Parse 
                //else
                //{
                //    //found some texture-mod only files
                //    foreach (var entry in textureModEntries)
                //    {
                //        currentOperationTextCallback?.Invoke(M3L.GetString(M3L.string_interp_readingX, entry.FileName));
                //        MemoryStream ms = new MemoryStream();
                //        archiveFile.ExtractFile(entry.Index, ms);
                //        ms.Position = 0;
                //        StreamReader reader = new StreamReader(ms);
                //        string text = reader.ReadToEnd();
                //        var rcwModsForFile = RCWMod.ParseRCWMods(Path.GetFileNameWithoutExtension(entry.FileName), text);
                //        foreach (var rcw in rcwModsForFile)
                //        {
                //            Mod m = new Mod(rcw);
                //            addCompressedModCallback?.Invoke(m);
                //            internalModList.Add(m);
                //        }
                //    }
                //}
            }
            else
            {
                M3Log.Information(@"Querying third party importing service for information about this file: " + filepath);
                currentOperationTextCallback?.Invoke(M3L.GetString(M3L.string_queryingThirdPartyImportingService));
                var md5 = forcedMD5 ?? (archiveStream != null ? M3Utilities.CalculateMD5(archiveStream) : M3Utilities.CalculateMD5(filepath));
                var potentialImportinInfos = TPIService.GetImportingInfosBySize(archiveSize);
                var importingInfo = potentialImportinInfos.FirstOrDefault(x => x.md5 == md5);

                if (importingInfo == null && isExe)
                {
                    M3Log.Error(@"EXE-based mods must be validated by ME3Tweaks before they can be imported into M3. This is to prevent breaking third party mods.");
                    return;
                }

                ExeTransform transform = null;
                if (importingInfo?.exetransform != null)
                {
                    M3Log.Information(@"TPIS lists exe transform for this mod: " + importingInfo.exetransform);
                    transform = new ExeTransform(OnlineContent.FetchExeTransform(importingInfo.exetransform));
                }

                string custommoddesc = null;
                if (importingInfo?.servermoddescname != null)
                {
                    //Partially supported unofficial third party mod
                    //Mod has a custom written moddesc.ini stored on ME3Tweaks
                    M3Log.Information(@"Fetching premade moddesc.ini from ME3Tweaks for this mod archive");
                    string loadFailedReason = null;
                    try
                    {
                        custommoddesc = OnlineContent.FetchThirdPartyModdesc(importingInfo.servermoddescname ?? transform.PostTransformModdesc);
                    }
                    catch (Exception e)
                    {
                        loadFailedReason = e.Message;
                        M3Log.Error(@"Error fetching moddesc from server: " + e.Message);
                    }

                    //if (!isExe)
                    //{
                    Mod virutalCustomMod = new Mod(custommoddesc, "", archiveFile); //Load virtual mod
                    if (virutalCustomMod.ValidMod)
                    {
                        M3Log.Information(@"Mod loaded from server moddesc.");
                        addCompressedModCallback?.Invoke(virutalCustomMod);
                        internalModList.Add(virutalCustomMod);
                        return; //Don't do further parsing as this is custom written
                    }
                    else
                    {
                        if (loadFailedReason != null)
                        {
                            virutalCustomMod.LoadFailedReason = M3L.GetString(
                                M3L.string_interp_failedToFetchModdesciniFileFromServerReasonLoadFailedReason,
                                loadFailedReason);
                        }
                        else
                        {
                            M3Log.Error(@"Server moddesc was not valid for this mod. This shouldn't occur. Please report to Mgamerz.");
                            Analytics.TrackEvent(@"Invalid servermoddesc detected", new Dictionary<string, string>()
                                {
                                    {@"moddesc.ini name", importingInfo.servermoddescname ?? transform.PostTransformModdesc}
                                });
                        }
                        if (closeStreamOnComplete)
                        {
                            archiveFile?.Dispose();
                        }
                        else
                        {
                            archiveFile?.DisposeObjectOnly();
                        }
                        return;
                    }
                    //} else
                    //{
                    //    M3Log.Information(@"Fetched premade moddesc.ini from server. We will fake the mod for the user");
                    //}
                }



                //Fully unofficial third party mod.

                //ME3
                foreach (var sfarEntry in sfarEntries)
                {
                    var vMod = AttemptLoadVirtualMod(sfarEntry, archiveFile, MEGame.ME3, md5);
                    if (vMod != null)
                    {
                        addCompressedModCallback?.Invoke(vMod);
                        internalModList.Add(vMod);
                        vMod.ExeExtractionTransform = transform;
                    }
                }

                //TODO: ME2 ?
                //foreach (var entry in bioengineEntries)
                //{
                //    var vMod = AttemptLoadVirtualMod(entry, archiveFile, MEGame.ME2, md5);
                //    if (vMod.ValidMod)
                //    {
                //        addCompressedModCallback?.Invoke(vMod);
                //        internalModList.Add(vMod);
                //    }
                //}

                //TODO: ME1 ?

                if (importingInfo?.version != null)
                {
                    foreach (Mod compressedMod in internalModList)
                    {
                        compressedMod.ModVersionString = importingInfo.version;
                        Version.TryParse(importingInfo.version, out var parsedValue);
                        compressedMod.ParsedModVersion = parsedValue;
                    }
                }
                else if (relayVersionResponse == @"-1")
                {
                    //If no version information, check ME3Tweaks to see if it's been added recently
                    //see if server has information on version number
                    currentOperationTextCallback?.Invoke(M3L.GetString(M3L.string_gettingAdditionalInformationAboutFileFromME3Tweaks));
                    M3Log.Information(@"Querying ME3Tweaks for additional information for this file...");
                    var modInfo = OnlineContent.QueryModRelay(md5, archiveSize);
                    //todo: make this work offline.
                    if (modInfo != null && modInfo.TryGetValue(@"version", out string value))
                    {
                        M3Log.Information(@"ME3Tweaks reports version number for this file is: " + value);
                        foreach (Mod compressedMod in internalModList)
                        {
                            compressedMod.ModVersionString = value;
                            Version.TryParse(value, out var parsedValue);
                            compressedMod.ParsedModVersion = parsedValue;
                        }
                        relayVersionResponse = value;
                    }
                    else
                    {
                        M3Log.Information(@"ME3Tweaks does not have additional version information for this file.");
                        Analytics.TrackEvent(@"Non Mod Manager Mod Dropped", new Dictionary<string, string>()
                            {
                                {@"Filename", Path.GetFileName(filepath)},
                                {@"MD5", md5}
                            });
                        foreach (Mod compressedMod in internalModList)
                        {
                            compressedMod.ModVersionString = M3L.GetString(M3L.string_unknown);
                        }
                    }
                }

                else
                {
                    //Try straight up TPMI import?
                    M3Log.Warning($@"No importing information is available for file with hash {md5}. No mods could be found.");
                    Analytics.TrackEvent(@"Non Mod Manager Mod Dropped", new Dictionary<string, string>()
                        {
                            {@"Filename", Path.GetFileName(filepath)},
                            {@"MD5", md5}
                        });
                }
            }

            if (closeStreamOnComplete)
            {
                archiveFile?.Dispose();
            }
            else
            {
                archiveFile?.DisposeObjectOnly();
            }
        }


        private static Mod AttemptLoadVirtualMod(ArchiveFileInfo sfarEntry, SevenZipExtractor archive, MEGame game, string md5)
        {
            var sfarPath = sfarEntry.FileName;
            var cookedPath = FilesystemInterposer.DirectoryGetParent(sfarPath, true);
            //Todo: Check if value is CookedPC/CookedPCConsole as further validation
            if (!string.IsNullOrEmpty(FilesystemInterposer.DirectoryGetParent(cookedPath, true)))
            {
                var dlcDir = FilesystemInterposer.DirectoryGetParent(cookedPath, true);
                var dlcFolderName = Path.GetFileName(dlcDir);
                if (!string.IsNullOrEmpty(dlcFolderName))
                {
                    var thirdPartyInfo = TPMIService.GetThirdPartyModInfo(dlcFolderName, game);
                    if (thirdPartyInfo != null)
                    {
                        if (thirdPartyInfo.PreventImport == false)
                        {
                            M3Log.Information($@"Third party mod found: {thirdPartyInfo.modname}, preparing virtual moddesc.ini");
                            //We will have to load a virtual moddesc. Since Mod constructor requires reading an ini, we will build and feed it a virtual one.
                            IniData virtualModDesc = new IniData();
                            virtualModDesc[@"ModManager"][@"cmmver"] = App.HighestSupportedModDesc.ToString();
                            virtualModDesc[@"ModManager"][@"importedby"] = App.BuildNumber.ToString();
                            virtualModDesc[@"ModInfo"][@"game"] = @"ME3";
                            virtualModDesc[@"ModInfo"][@"modname"] = thirdPartyInfo.modname;
                            virtualModDesc[@"ModInfo"][@"moddev"] = thirdPartyInfo.moddev;
                            virtualModDesc[@"ModInfo"][@"modsite"] = thirdPartyInfo.modsite;
                            virtualModDesc[@"ModInfo"][@"moddesc"] = thirdPartyInfo.moddesc;
                            virtualModDesc[@"ModInfo"][@"unofficial"] = @"true";
                            if (int.TryParse(thirdPartyInfo.updatecode, out var updatecode) && updatecode > 0)
                            {
                                virtualModDesc[@"ModInfo"][@"updatecode"] = updatecode.ToString();
                                virtualModDesc[@"ModInfo"][@"modver"] = 0.001.ToString(CultureInfo.InvariantCulture); //This will force mod to check for update after reload
                            }
                            else
                            {
                                virtualModDesc[@"ModInfo"][@"modver"] = 0.0.ToString(CultureInfo.InvariantCulture); //Will attempt to look up later after mods have parsed.
                            }

                            virtualModDesc[@"CUSTOMDLC"][@"sourcedirs"] = dlcFolderName;
                            virtualModDesc[@"CUSTOMDLC"][@"destdirs"] = dlcFolderName;
                            virtualModDesc[@"UPDATES"][@"originalarchivehash"] = md5;

                            var archiveSize = archive.ArchiveSize;
                            var importingInfos = TPIService.GetImportingInfosBySize(archiveSize);
                            if (importingInfos.Count == 1 && importingInfos[0].GetParsedRequiredDLC().Count > 0)
                            {
                                OnlineContent.QueryModRelay(importingInfos[0].md5, archiveSize); //Tell telemetry relay we are accessing the TPIS for an existing item so it can update latest for tracking
                                virtualModDesc[@"ModInfo"][@"requireddlc"] = importingInfos[0].requireddlc;
                            }

                            return new Mod(virtualModDesc.ToString(), FilesystemInterposer.DirectoryGetParent(dlcDir, true), archive);
                        }

                        //Mod is marked for preventing import
                        return new Mod(false)
                        {
                            ModName = thirdPartyInfo.modname,
                            ModDeveloper = thirdPartyInfo.moddev,
                            LoadFailedReason = M3L.GetString(M3L.string_modCannotBeImportedDueToOneOfTheFollowingReasons)
                        };
                    }
                    else
                    {
                        M3Log.Information($@"No third party mod information for importing {dlcFolderName}. Should this be supported for import? Contact Mgamerz on the ME3Tweaks Discord if it should.");
                    }
                }
            }
            return null;
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
                if (b.Error != null)
                {
                    M3Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                TaskRunning = false;
                if (b.Result is List<Mod> modList)
                {
                    OnClosing(new DataEventArgs(modList));
                    return;
                }


                long requiredSpace = 0;
                ModImportResult result = ModImportResult.None;
                if (b.Result is (long spaceRequired, ModImportResult res))
                {
                    result = res;
                    requiredSpace = spaceRequired;
                }

                if (b.Result is ModImportResult res2)
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
                            ActionText = M3L.GetString(M3L.string_insufficientDiskSpaceToExtractSelectedMods); //localize me
                            M3Utilities.DriveFreeBytes(M3Utilities.GetModsDirectory(), out var freeSpace);
                            M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialogNotEnoughFreeSpaceToExtract, FileSize.FormatSize(requiredSpace), FileSize.FormatSize(freeSpace)), M3L.GetString(M3L.string_insufficientDiskSpace), MessageBoxButton.OK, MessageBoxImage.Error);
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
            List<Mod> mods = (List<Mod>)e.Argument;
            List<Mod> extractedMods = new List<Mod>();

            void TextUpdateCallback(string x)
            {
                ActionText = x;
            }

            //get total size requirement
            long requiredDiskSpace = mods.Sum(x => x.SizeRequiredtoExtract);
            if (M3Utilities.DriveFreeBytes(M3Utilities.GetModsDirectory(), out var freespaceBytes))
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
                M3Log.Error(@"Unable to get amount of free space for mod library directory disk! We will continue anyways. Path: " + M3Utilities.GetModsDirectory());
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
                var modDirectory = M3Utilities.GetModDirectoryForGame(mod.Game);
                var sanitizedPath = Path.Combine(modDirectory, M3Utilities.SanitizePath(mod.ModName));


                if (Directory.Exists(sanitizedPath))
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
                            M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_dialogErrorOccuredDeletingExistingMod), M3L.GetString(M3L.string_errorDeletingExistingMod), MessageBoxButton.OK, MessageBoxImage.Error);
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

                Directory.CreateDirectory(sanitizedPath);

                ActionText = M3L.GetString(M3L.string_interp_extractingX, mod.ModName);
                //Check if RCW mod
                if (mod.InstallationJobs.Count == 1 && mod.InstallationJobs[0].Header == ModJob.JobHeader.ME2_RCWMOD)
                {
                    M3Log.Information(@"Generating M3 wrapper moddesc.ini for " + mod.ModName);
                    mod.ExtractRCWModToM3LibraryMod(sanitizedPath);
                    extractedMods.Add(mod);
                }
                else
                {
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
        }

        public enum ModImportResult
        {
            USER_ABORTED_IMPORT, ERROR_COULD_NOT_DELETE_EXISTING_DIR,
            ERROR_INSUFFICIENT_DISK_SPACE,
            None,
            ERROR_EXTRACTING_ARCHIVE
        }

        private bool CanInstallCompressedMod()
        {
            //This will have to pass some sort of validation code later.
            return CompressedMods_ListBox != null && CompressedMods_ListBox.SelectedItem is Mod cm &&
                   cm.ExeExtractionTransform == null && cm.ValidMod
                   && !TaskRunning /*&& !CompressPackages*/ && mainwindow.InstallationTargets.Any(x => x.Game == cm.Game);
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
            InspectArchiveFile(ArchiveFilePath);
        }

        private void CheckAll_Click(object sender, RoutedEventArgs e)
        {
            checkAll(true);
        }

        private void UncheckAll_Click(object sender, RoutedEventArgs e)
        {
            checkAll(false);
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
