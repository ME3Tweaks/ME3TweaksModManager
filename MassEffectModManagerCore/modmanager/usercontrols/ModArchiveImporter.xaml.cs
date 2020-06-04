using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
using Threading;
using MassEffectModManagerCore.modmanager.gameini;
using System.Windows.Media.Animation;
using ByteSizeLib;
using MassEffectModManagerCore.modmanager.localizations;
using Microsoft.AppCenter.Analytics;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic foru ModArchiveImporter.xaml
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

        public string ScanningFile { get; private set; } = M3L.GetString(M3L.string_pleaseWait);
        public string ActionText { get; private set; }
        public long ProgressValue { get; private set; }
        public long ProgressMaximum { get; private set; }
        public bool ProgressIndeterminate { get; private set; }

        public bool CanCompressPackages { get; private set; }

        public ObservableCollectionExtended<Mod> CompressedMods { get; } = new ObservableCollectionExtended<Mod>();
        public ModArchiveImporter(string file)
        {
            DataContext = this;
            LoadCommands();
            ArchiveFilePath = file;
            InitializeComponent();
        }

        /// <summary>
        /// Begins inspection of archive file. This method will spawn a background thread that will
        /// run asynchronously.
        /// </summary>
        /// <param name="filepath">Path to the archive file</param>
        private void InspectArchiveFile(string filepath)
        {
            ScanningFile = Path.GetFileName(filepath);
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ModArchiveInspector");
            bw.DoWork += InspectArchiveBackgroundThread;
            ProgressValue = 0;
            ProgressMaximum = 100;
            ProgressIndeterminate = true;

            bw.RunWorkerCompleted += (a, b) =>
            {
                if (CompressedMods.Count > 0)
                {
                    ActionText = M3L.GetString(M3L.string_selectModsToImportIntoModManagerLibrary);
                    if (CompressedMods.Count == 1)
                    {
                        CompressedMods_ListBox.SelectedIndex = 0; //Select the only item
                    }
                    ArchiveScanned = true;

                    //Initial release disables this.
                    //TODO: RE-ENABLE THIS
                    CanCompressPackages = false && CompressedMods.Any() && CompressedMods.Any(x => x.Game == Mod.MEGame.ME3); //Change to include ME2 when support for LZO is improved
                }
                else if (TextureFilesImported)
                {
                    CancelButtonText = M3L.GetString(M3L.string_close);
                    NoModSelectedText = M3L.GetString(M3L.string_interp_dialogImportedALOTMainToTextureLibrary, ScanningFile, Utilities.GetALOTInstallerTextureLibraryDirectory());
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
            };
            ActionText = M3L.GetString(M3L.string_interp_scanningX, Path.GetFileName(filepath));

            bw.RunWorkerAsync(filepath);
        }


        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void hack_NotifyPropertyChanged([CallerMemberName] string propertyname = null)
        {
            hack_PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }
        private bool openedMultipanel = false;

        /// <summary>
        /// Inspects an 'archive' file. Archives may contain one or more mods (or none).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InspectArchiveBackgroundThread(object sender, DoWorkEventArgs e)
        {
            TaskRunning = true;
            ActionText = M3L.GetString(M3L.string_interp_openingX, ScanningFile);

            var archive = e.Argument as string;
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
            var archiveSize = new FileInfo(archive).Length;
            var knownModsOfThisSize = ThirdPartyServices.GetImportingInfosBySize(archiveSize);
            string pathOverride = null;
            if (knownModsOfThisSize.Count > 0 && knownModsOfThisSize.Any(x => x.zippedexepath != null))
            {
                //might have embedded exe
                if (archive.RepresentsFileArchive())
                {
                    SevenZipExtractor sve = new SevenZipExtractor(archive);
                    string embeddedExePath = null;
                    Log.Information(@"This file may contain a known exe-based mod.");
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
                                var importingInfo2 = ThirdPartyServices.GetImportingInfosBySize((long)exedata.Size);
                                if (importingInfo2.Count == 0)
                                {
                                    Log.Warning(@"zip wrapper for this file has importing information but the embedded exe does not!");
                                    break; //no importing info
                                }

                                Log.Information(@"Reading embedded executable file in archive: " + embeddedExePath);
                                ActionText = M3L.GetString(M3L.string_readingZippedExecutable);
                                pathOverride = Path.Combine(Utilities.GetTempPath(), Path.GetFileName(embeddedExePath));
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
            InspectArchive(pathOverride ?? archive, AddCompressedModCallback, CompressedModFailedCallback, ActionTextUpdateCallback, ShowALOTLauncher);
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
            Action showALOTLauncher = null, string forcedMD5 = null, int forcedSize = -1)
        {
            string relayVersionResponse = @"-1";
            List<Mod> internalModList = new List<Mod>(); //internal mod list is for this function only so we don't need a callback to get our list since results are returned immediately
            var isExe = filepath.EndsWith(@".exe");
            var archiveFile = isExe ? new SevenZipExtractor(filepath, InArchiveFormat.Nsis) : new SevenZipExtractor(filepath);
            using (archiveFile)
            {
#if DEBUG
                foreach (var v in archiveFile.ArchiveFileData)
                {
                    Debug.WriteLine($@"{v.FileName} | Index {v.Index} | Size {v.Size} | Last Modified {v.LastWriteTime}");
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
                                //for unofficial lookups
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
                    Log.Error($@"Unable to inspect archive {filepath}: SevenZipException occured! It may be corrupt. The specific error was: {svae.Message}");
                    failedToLoadModeCallback?.Invoke(failed);
                    addCompressedModCallback?.Invoke(failed);
                    return;
                }

                // Used for TPIS information lookup
                long archiveSize = forcedSize > 0 ? forcedSize : new FileInfo(filepath).Length;

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
                else if (textureModEntries.Any() && isAlotFile)
                {
                    if (isAlotFile)
                    {
                        //is alot installer
                        Log.Information(@"This file contains texture files and ALOTInstaller.exe - this is an ALOT main file");
                        var textureLibraryPath = Utilities.GetALOTInstallerTextureLibraryDirectory();
                        if (textureLibraryPath != null)
                        {
                            //we have destination
                            var destPath = Path.Combine(textureLibraryPath, Path.GetFileName(filepath));
                            if (!File.Exists(destPath))
                            {
                                Log.Information(M3L.GetString(M3L.string_thisFileIsNotInTheTextureLibraryMovingItToTheTextureLibrary));
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
                    Log.Information(@"Querying third party importing service for information about this file: " + filepath);
                    currentOperationTextCallback?.Invoke(M3L.GetString(M3L.string_queryingThirdPartyImportingService));
                    var md5 = forcedMD5 ?? Utilities.CalculateMD5(filepath);
                    var potentialImportinInfos = ThirdPartyServices.GetImportingInfosBySize(archiveSize);
                    var importingInfo = potentialImportinInfos.FirstOrDefault(x => x.md5 == md5);

                    if (importingInfo == null && isExe)
                    {
                        Log.Error(@"EXE-based mods must be validated by ME3Tweaks before they can be imported into M3. This is to prevent breaking third party mods.");
                        return;
                    }

                    if (importingInfo?.servermoddescname != null)
                    {
                        //Partially supported unofficial third party mod
                        //Mod has a custom written moddesc.ini stored on ME3Tweaks
                        Log.Information(@"Fetching premade moddesc.ini from ME3Tweaks for this mod archive");
                        string custommoddesc = null;
                        string loadFailedReason = null;
                        try
                        {
                            custommoddesc = OnlineContent.FetchThirdPartyModdesc(importingInfo.servermoddescname);
                        }
                        catch (Exception e)
                        {
                            loadFailedReason = e.Message;
                            Log.Error(@"Error fetching moddesc from server: " + e.Message);
                        }

                        Mod virutalCustomMod = new Mod(custommoddesc, "", archiveFile); //Load virutal mod
                        if (virutalCustomMod.ValidMod)
                        {
                            Log.Information(@"Mod loaded from server moddesc.");
                            addCompressedModCallback?.Invoke(virutalCustomMod);
                            internalModList.Add(virutalCustomMod);
                            return; //Don't do further parsing as this is custom written
                        }
                        else
                        {
                            if (loadFailedReason != null)
                            {
                                virutalCustomMod.LoadFailedReason = M3L.GetString(M3L.string_interp_failedToFetchModdesciniFileFromServerReasonLoadFailedReason, loadFailedReason);
                            }
                            else
                            {
                                Log.Error(@"Server moddesc was not valid for this mod. This shouldn't occur. Please report to Mgamerz.");
                            }
                            return;
                        }
                    }

                    ExeTransform transform = null;
                    if (importingInfo?.exetransform != null)
                    {
                        Log.Information(@"TPIS lists exe transform for this mod: " + importingInfo.exetransform);
                        transform = new ExeTransform(OnlineContent.FetchExeTransform(importingInfo.exetransform));
                    }

                    //Fully unofficial third party mod.

                    //ME3
                    foreach (var sfarEntry in sfarEntries)
                    {
                        var vMod = AttemptLoadVirtualMod(sfarEntry, archiveFile, Mod.MEGame.ME3, md5);
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
                    //    var vMod = AttemptLoadVirtualMod(entry, archiveFile, Mod.MEGame.ME2, md5);
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
                        Log.Information(@"Querying ME3Tweaks for additional information for this file...");
                        var modInfo = OnlineContent.QueryModRelay(md5, archiveSize);
                        //todo: make this work offline.
                        if (modInfo != null && modInfo.TryGetValue(@"version", out string value))
                        {
                            Log.Information(@"ME3Tweaks reports version number for this file is: " + value);
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
                            Log.Information(@"ME3Tweaks does not have additional version information for this file.");
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
                        Log.Warning($@"No importing information is available for file with hash {md5}. No mods could be found.");
                        Analytics.TrackEvent(@"Non Mod Manager Mod Dropped", new Dictionary<string, string>()
                        {
                            {@"Filename", Path.GetFileName(filepath)},
                            {@"MD5", md5}
                        });
                    }
                }
            }

        }


        private static Mod AttemptLoadVirtualMod(ArchiveFileInfo sfarEntry, SevenZipExtractor archive, Mod.MEGame game, string md5)
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
                    var thirdPartyInfo = ThirdPartyServices.GetThirdPartyModInfo(dlcFolderName, game);
                    if (thirdPartyInfo != null)
                    {
                        if (thirdPartyInfo.PreventImport == false)
                        {
                            Log.Information($@"Third party mod found: {thirdPartyInfo.modname}, preparing virtual moddesc.ini");
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

                            var archiveSize = new FileInfo(archive.FileName).Length;
                            var importingInfos = ThirdPartyServices.GetImportingInfosBySize(archiveSize);
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
                        Log.Information($@"No third party mod information for importing {dlcFolderName}. Should this be supported for import? Contact Mgamerz on the ME3Tweaks Discord if it should.");
                    }
                }
            }
            return null;
        }

        private void BeginImportingMods()
        {
            var modsToExtract = CompressedMods.Where(x => x.SelectedForImport).ToList();
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ModExtractor");
            bw.DoWork += ExtractModsBackgroundThread;
            bw.RunWorkerCompleted += (a, b) =>
            {
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
                            Utilities.DriveFreeBytes(Utilities.GetModsDirectory(), out var freeSpace);
                            M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialogNotEnoughFreeSpaceToExtract, ByteSize.FromBytes(requiredSpace).ToString(), ByteSize.FromBytes(freeSpace).ToString()), M3L.GetString(M3L.string_insufficientDiskSpace), MessageBoxButton.OK, MessageBoxImage.Error);
                            return; //Don't do anything.
                        }
                }
                //Close.
                OnClosing(DataEventArgs.Empty);
            };
            TaskRunning = true;
            bw.RunWorkerAsync(modsToExtract);
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
            long requiredDiskSpace = mods.Sum(x => x.GetRequiredSpaceForExtraction(ArchiveFilePath));
            if (Utilities.DriveFreeBytes(Utilities.GetModsDirectory(), out var freespaceBytes))
            {
                requiredDiskSpace = (long)(requiredDiskSpace * 1.05); //5% buffer
                Log.Information($@"Selected mods require: {ByteSize.FromBytes(requiredDiskSpace)}");
                if ((long)freespaceBytes < requiredDiskSpace)
                {
                    Log.Error(@"There is not enough free space on the disk to extract these mods.");
                    Log.Error($@"Selected mods require: {ByteSize.FromBytes(requiredDiskSpace)} | Disk space available in library partition: {ByteSize.FromBytes(freespaceBytes)}");
                    e.Result = (requiredDiskSpace, ModImportResult.ERROR_INSUFFICIENT_DISK_SPACE);
                    return;
                }

            }
            else
            {
                Log.Error(@"Unable to get amount of free space for mod library directory disk! We will continue anyways. Path: " + Utilities.GetModsDirectory());
            }



            foreach (var mod in mods)
            {
                //Todo: Extract files
                Log.Information(@"Extracting mod: " + mod.ModName);
                ActionText = M3L.GetString(M3L.string_interp_extractingX, mod.ModName);
                ProgressValue = 0;
                ProgressMaximum = 100;
                ProgressIndeterminate = true;
                //Ensure directory
                var modDirectory = Utilities.GetModDirectoryForGame(mod.Game);
                var sanitizedPath = Path.Combine(modDirectory, Utilities.SanitizePath(mod.ModName));


                if (Directory.Exists(sanitizedPath))
                {
                    //Will delete on import
                    bool abort = false;
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        var result = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_dialogImportingModWillDeleteExistingMod, sanitizedPath), M3L.GetString(M3L.string_modAlreadyExists), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                        if (result == MessageBoxResult.No)
                        {
                            e.Result = ModImportResult.USER_ABORTED_IMPORT;
                            abort = true;
                            return;
                        }

                        try
                        {
                            if (!Utilities.DeleteFilesAndFoldersRecursively(sanitizedPath))
                            {
                                Log.Error(@"Could not delete existing mod directory.");
                                e.Result = ModImportResult.ERROR_COULD_NOT_DELETE_EXISTING_DIR;
                                M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_dialogErrorOccuredDeletingExistingMod), M3L.GetString(M3L.string_errorDeletingExistingMod), MessageBoxButton.OK, MessageBoxImage.Error);
                                abort = true;
                                return;
                            }

                        }
                        catch (Exception ex)
                        {
                            //I don't think this can be triggered but will leave as failsafe anyways.
                            Log.Error(@"Error while deleting existing output directory: " + App.FlattenException(ex));
                            M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_errorOccuredDeletingExistingModX, ex.Message), M3L.GetString(M3L.string_errorDeletingExistingMod), MessageBoxButton.OK, MessageBoxImage.Error);
                            e.Result = ModImportResult.ERROR_COULD_NOT_DELETE_EXISTING_DIR;
                            abort = true;
                        }
                    });
                    if (abort)
                    {
                        Log.Warning(@"Aborting mod import.");
                        return;
                    }
                }

                Directory.CreateDirectory(sanitizedPath);

                //Check if RCW mod
                if (mod.InstallationJobs.Count == 1 && mod.InstallationJobs[0].Header == ModJob.JobHeader.ME2_RCWMOD)
                {
                    Log.Information(@"Generating M3 wrapper moddesc.ini for " + mod.ModName);
                    mod.ExtractRCWModToM3LibraryMod(sanitizedPath);
                    extractedMods.Add(mod);
                }
                else
                {
                    try
                    {
                        mod.ExtractFromArchive(ArchiveFilePath, sanitizedPath, CompressPackages, TextUpdateCallback, ExtractionProgressCallback, CompressedPackageCallback);
                    }
                    catch (Exception ex)
                    {
                        //Extraction failed!
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            Log.Error(@"Error while extracting archive: " + App.FlattenException(ex));
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

        private SerialQueue fileCompressionQueue = new SerialQueue();

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
                   && !TaskRunning;
        }

        private void InstallCompressedMod()
        {
            OnClosing(new DataEventArgs(CompressedMods_ListBox.SelectedItem));
        }

        private void Cancel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanCancel() => !TaskRunning;

        private bool CanImportMods() => !TaskRunning && CompressedMods.Any(x => x.SelectedForImport && x.ValidMod);

        public event PropertyChangedEventHandler hack_PropertyChanged;

        private void SelectedMod_Changed(object sender, SelectionChangedEventArgs e)
        {
            SelectedMod = CompressedMods_ListBox.SelectedItem as Mod;
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
