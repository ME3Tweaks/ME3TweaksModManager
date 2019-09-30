using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IniParser.Model;
using MassEffectModManager;
using MassEffectModManagerCore.modmanager.helpers;
using SevenZip;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModArchiveImporter.xaml
    /// </summary>
    public partial class ModArchiveImporter : UserControl, INotifyPropertyChanged
    {
        private readonly Action<ProgressBarUpdate> progressBarCallback;
        private bool TaskRunning;
        public string NoModSelectedText { get; } = "Select a mod on the left to view its description";
        public bool ArchiveScanned { get; set; }
        public event EventHandler<DataEventArgs> Close;
        public bool CompressPackages { get; set; }
        protected virtual void OnClosing(DataEventArgs e)
        {
            EventHandler<DataEventArgs> handler = Close;
            handler?.Invoke(this, e);
        }

        public CompressedMod SelectedMod { get; private set; }

        private string ArchiveFilePath;

        public string ScanningFile { get; private set; } = "Please wait";
        public string ActionText { get; private set; }
        public ObservableCollectionExtended<CompressedMod> CompressedMods { get; } = new ObservableCollectionExtended<CompressedMod>();
        public ModArchiveImporter(Action<ProgressBarUpdate> progressBarCallback)
        {
            this.progressBarCallback = progressBarCallback;
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }


        public void InspectArchiveFile(string filepath)
        {
            ArchiveFilePath = filepath;
            ScanningFile = Path.GetFileName(filepath);
            NamedBackgroundWorker bw = new NamedBackgroundWorker("ModArchiveInspector");
            bw.DoWork += InspectArchiveBackgroundThread;
            progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VALUE, 0));
            progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_INDETERMINATE, true));
            progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VISIBILITY, Visibility.Visible));

            bw.RunWorkerCompleted += (a, b) =>
            {
                if (CompressedMods.Count > 0)
                {
                    ActionText = $"Select mods to import into Mod Manager library";
                    if (CompressedMods.Count == 1)
                    {
                        CompressedMods_ListBox.SelectedIndex = 0; //Select the only item
                    }

                    ArchiveScanned = true;
                }
                else
                {
                    ActionText = "No compatible mods found in archive";
                }
                progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VALUE, 0));
                progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_INDETERMINATE, false));
                TaskRunning = false;
                CommandManager.InvalidateRequerySuggested();
            };
            ActionText = $"Scanning {Path.GetFileName(filepath)}";

            bw.RunWorkerAsync(filepath);
        }

        private void InspectArchiveBackgroundThread(object sender, DoWorkEventArgs e)
        {
            TaskRunning = true;
            string filepath = (string)e.Argument;
            ActionText = $"Opening {ScanningFile}";
            string relayVersionResponse = "-1";
            using (var archiveFile = new SevenZipExtractor(filepath))
            {
                var moddesciniEntries = new List<ArchiveFileInfo>();
                var sfarEntries = new List<ArchiveFileInfo>(); //ME3 DLC
                var bioengineEntries = new List<ArchiveFileInfo>(); //ME2 DLC
                foreach (var entry in archiveFile.ArchiveFileData)
                {
                    string fname = Path.GetFileName(entry.FileName);
                    if (!entry.IsDirectory && fname.Equals("moddesc.ini", StringComparison.InvariantCultureIgnoreCase))
                    {
                        moddesciniEntries.Add(entry);
                    }
                    else if (!entry.IsDirectory && fname.Equals("Default.sfar", StringComparison.InvariantCultureIgnoreCase))
                    {
                        //for unofficial lookups
                        sfarEntries.Add(entry);
                    }
                    else if (!entry.IsDirectory && fname.Equals("BIOEngine.ini", StringComparison.InvariantCultureIgnoreCase))
                    {
                        //for unofficial lookups
                        bioengineEntries.Add(entry);
                    }
                }

                if (moddesciniEntries.Count > 0)
                {
                    foreach (var entry in moddesciniEntries)
                    {
                        ActionText = $"Reading {entry.FileName}";
                        Mod m = new Mod(entry, archiveFile);
                        if (m.ValidMod)
                        {
                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                CompressedMods.Add(new CompressedMod(m));
                                CompressedMods.Sort(x => x.Mod.ModName);
                            });
                        }
                    }
                }
                else if (sfarEntries.Count > 0)
                {
                    //Todo: Run unofficially supported scan
                    var md5 = Utilities.CalculateMD5(filepath);
                    long size = new FileInfo(filepath).Length;
                    var potentialImportinInfos = ThirdPartyServices.GetImportingInfosBySize(size);
                    var importingInfo = potentialImportinInfos.FirstOrDefault(x => x.md5 == md5);
                    if (importingInfo != null)
                    {
                        if (importingInfo.servermoddescname != null)
                        {
                            //Partially supported unofficial third party mod
                            //Mod has a custom written moddesc.ini stored on ME3Tweaks
                            string custommoddesc = OnlineContent.FetchThirdPartyModdesc(importingInfo.servermoddescname);
                            Mod virutalCustomMod = new Mod(custommoddesc, "", archiveFile); //Load virutal mod
                            if (virutalCustomMod.ValidMod)
                            {
                                Application.Current.Dispatcher.Invoke(delegate
                                {
                                    CompressedMods.Add(new CompressedMod(virutalCustomMod));
                                    CompressedMods.Sort(x => x.Mod.ModName);
                                });
                                return; //Don't do further parsing as this is custom written
                            }
                        }
                        //Fully unofficial third party mod.

                        //ME3
                        foreach (var entry in sfarEntries)
                        {
                            var vMod = AttemptLoadVirtualMod(entry, archiveFile, Mod.MEGame.ME3);
                            if (vMod.ValidMod)
                            {
                                Application.Current.Dispatcher.Invoke(delegate
                                {
                                    CompressedMods.Add(new CompressedMod(vMod));
                                    CompressedMods.Sort(x => x.Mod.ModName);
                                });
                            }
                        }

                        if (importingInfo.version != null)
                        {
                            foreach (CompressedMod compressedMod in CompressedMods)
                            {
                                compressedMod.Mod.ModVersionString = importingInfo.version;
                                Double.TryParse(importingInfo.version, out double parsedValue);
                                compressedMod.Mod.ParsedModVersion = parsedValue;
                            }
                        }
                        else if (relayVersionResponse == "-1")
                        {
                            //If no version information, check ME3Tweaks to see if it's been added recently
                            //see if server has information on version number
                            ActionText = $"Getting additional information about file from ME3Tweaks";
                            Log.Information("Querying ME3Tweaks for additional information");
                            var modInfo = OnlineContent.QueryModRelay(md5, size);
                            //todo: make this work offline.
                            if (modInfo != null && modInfo.TryGetValue("version", out string value))
                            {
                                Log.Information("ME3Tweaks reports version number for this file is: " + value);
                                foreach (CompressedMod compressedMod in CompressedMods)
                                {
                                    compressedMod.Mod.ModVersionString = value;
                                    Double.TryParse(value, out double parsedValue);
                                    compressedMod.Mod.ParsedModVersion = parsedValue;
                                }

                                relayVersionResponse = value;
                            }
                            else
                            {
                                Log.Information("ME3Tweaks does not have additional version information for this file");
                            }
                        }
                    }
                    else
                    {
                        Log.Warning($"No importing information is available for file with hash {md5}");
                    }
                }
                else
                {
                    Log.Information("This archive does not appear to have any officially supported mods and does not contain any dlc-required content files, thus contains no mods.");
                }
            }
        }

        private Mod AttemptLoadVirtualMod(ArchiveFileInfo entry, SevenZipExtractor archive, Mod.MEGame game)
        {
            var sfarPath = entry.FileName;
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
                        Log.Information($"Third party mod found: {thirdPartyInfo.modname}, preparing virtual moddesc.ini");

                        //We will have to load a virtual moddesc. Since Mod constructor requires reading an ini, we will build an feed it a virtual one.
                        IniData virtualModDesc = new IniData();
                        virtualModDesc["ModManager"]["cmmver"] = App.HighestSupportedModDesc.ToString();
                        virtualModDesc["ModInfo"]["modname"] = thirdPartyInfo.modname;
                        virtualModDesc["ModInfo"]["moddev"] = thirdPartyInfo.moddev;
                        virtualModDesc["ModInfo"]["modsite"] = thirdPartyInfo.modsite;
                        virtualModDesc["ModInfo"]["moddesc"] = thirdPartyInfo.moddesc;
                        virtualModDesc["ModInfo"]["unofficial"] = "true";
                        if (int.TryParse(thirdPartyInfo.updatecode, out var updatecode) && updatecode > 0)
                        {
                            virtualModDesc["ModInfo"]["updatecode"] = updatecode.ToString();
                            virtualModDesc["ModInfo"]["modver"] = 0.001.ToString(); //This will force mod to check for update after reload
                        }
                        else
                        {
                            virtualModDesc["ModInfo"]["modver"] = 0.0.ToString(); //Will attempt to look up later after mods have parsed.
                        }

                        virtualModDesc["CUSTOMDLC"]["sourcedirs"] = dlcFolderName;
                        virtualModDesc["CUSTOMDLC"]["destdirs"] = dlcFolderName;

                        return new Mod(virtualModDesc.ToString(), FilesystemInterposer.DirectoryGetParent(dlcDir, true), archive);
                    }
                }
                else
                {
                    Log.Information($"No third party mod information for importing {dlcFolderName}. Should this be supported for import? Contact Mgamerz on the ME3Tweaks Discord if it should.");
                }
            }

            return null;
        }

        public void BeginImportingMods()
        {
            var modsToExtract = CompressedMods.Where(x => x.SelectedForImport).ToList();
            NamedBackgroundWorker bw = new NamedBackgroundWorker("ModExtractor");
            bw.DoWork += ExtractModsBackgroundThread;
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Result is List<Mod> modList)
                {
                    OnClosing(new DataEventArgs(modList));
                    return;
                }
                OnClosing(DataEventArgs.Empty);
            };
            bw.RunWorkerAsync(modsToExtract);
        }

        private void ExtractModsBackgroundThread(object sender, DoWorkEventArgs e)
        {
            List<CompressedMod> mods = (List<CompressedMod>)e.Argument;
            List<Mod> extractedMods = new List<Mod>();

            void TextUpdateCallback(string x)
            {
                ActionText = x;
            }

            foreach (var mod in mods)
            {
                //Todo: Extract files
                Log.Information("Extracting mod: " + mod.Mod.ModName);
                ActionText = $"Extracting {mod.Mod.ModName}";
                progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VALUE, 0));
                progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_INDETERMINATE, true));
                mod.Mod.ExtractFromArchive(ArchiveFilePath, CompressPackages, TextUpdateCallback, ExtractionProgressCallback);
                extractedMods.Add(mod.Mod);
            }

            e.Result = extractedMods;
        }

        private void ExtractionProgressCallback(ProgressEventArgs args)
        {
            progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_MAX, 100));
            progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VALUE, (int)args.PercentDone));
            progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_INDETERMINATE, false));

        }

        public ICommand ImportModsCommand { get; set; }
        public ICommand CancelCommand { get; set; }
        public ICommand InstallModCommand { get; set; }
        private void LoadCommands()
        {
            ImportModsCommand = new GenericCommand(BeginImportingMods, CanImportMods);
            CancelCommand = new GenericCommand(Cancel, CanCancel);
            InstallModCommand = new GenericCommand(InstallCompressedMod, CanInstallCompressedMod);
        }

        private static ModJob.JobHeader[] CurrentlyDirectInstallSupportedJobs = { ModJob.JobHeader.BASEGAME, ModJob.JobHeader.BALANCE_CHANGES, ModJob.JobHeader.CUSTOMDLC };
        private bool CanInstallCompressedMod()
        {
            //This will have to pass some sort of validation code later.
            return CompressedMods_ListBox != null && CompressedMods_ListBox.SelectedItem is CompressedMod cm && CurrentlyDirectInstallSupportedJobs.ContainsAll(cm.Mod.InstallationJobs.Select(x => x.Header));
        }

        private void InstallCompressedMod()
        {
            OnClosing(new DataEventArgs(((CompressedMod)CompressedMods_ListBox.SelectedItem).Mod));
        }

        private void Cancel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanCancel() => true;

        private bool CanImportMods() => !TaskRunning && CompressedMods.Any(x => x.SelectedForImport);

        public event PropertyChangedEventHandler PropertyChanged;

        private void SelectedMod_Changed(object sender, SelectionChangedEventArgs e)
        {
            SelectedMod = CompressedMods_ListBox.SelectedItem as CompressedMod;
        }
    }
}
