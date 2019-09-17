using MassEffectModManager.modmanager.helpers;
using MassEffectModManager.modmanager.objects;
using MassEffectModManager.ui;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SevenZip;

namespace MassEffectModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModArchiveImporter.xaml
    /// </summary>
    public partial class ModArchiveImporter : UserControl, INotifyPropertyChanged
    {
        private readonly Action<ProgressBarUpdate> progressBarCallback;
        private bool TaskRunning;
        public string NoModSelectedText { get; } = "Select a mod on the left to view its description";
        public event EventHandler Close;
        protected virtual void OnClosing(EventArgs e)
        {
            EventHandler handler = Close;
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
                }
                else
                {
                    ActionText = "No compatible mods found in archive";
                }
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (CompressedMods.Count == 1)
                {
                    CompressedMods_ListBox.SelectedIndex = 0; //Select the only item
                }
                progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VALUE, 0));
                progressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_INDETERMINATE, false));
                TaskRunning = false;
            };
            ActionText = $"Scanning {Path.GetFileName(filepath)}";

            bw.RunWorkerAsync(filepath);
        }

        private void InspectArchiveBackgroundThread(object sender, DoWorkEventArgs e)
        {
            TaskRunning = true;
            string filepath = (string)e.Argument;
            ActionText = $"Opening {ScanningFile}";
            using (var archiveFile = new SevenZipExtractor(filepath))
            {
                var moddesciniEntries = new List<ArchiveFileInfo>();
                foreach (var entry in archiveFile.ArchiveFileData)
                {
                    string fname = Path.GetFileName(entry.FileName);
                    if (!entry.IsDirectory && fname.Equals("moddesc.ini", StringComparison.InvariantCultureIgnoreCase))
                    {
                        moddesciniEntries.Add(entry);
                    }
                }

                if (moddesciniEntries.Count > 0)
                {
                    foreach (var entry in moddesciniEntries)
                    {
                        ActionText = $"Reading {entry.FileName}";

                        MemoryStream ms = new MemoryStream();
                        archiveFile.ExtractFile(entry.FileName, ms);
                        Mod m = new Mod(ms, ignoreLoadErrors: true);
                        if (m.ValidMod)
                        {
                            try
                            {
                                Application.Current.Dispatcher.Invoke(delegate
                                {
                                    CompressedMods.Add(new CompressedMod(m));
                                    CompressedMods.Sort(x => x.Mod.ModName);
                                });
                            }
                            catch (Exception ex)
                            {
                                //Don't load
                                Log.Warning("Unable to load compressed mod moddesc.ini: " + ex.Message);
                            }
                        }
                    }
                }
                else
                {
                    //Todo: Run unofficially supported scan
                }
            }
        }

        public void ExtractMods()
        {
            var modsToExtract = CompressedMods.Where(x => x.SelectedForImport).ToList();
            NamedBackgroundWorker bw = new NamedBackgroundWorker("ModExtractor");
            bw.DoWork += ExtractModsBackgroundThread;
            bw.RunWorkerCompleted += (a, b) =>
            {

            };
            bw.RunWorkerAsync(modsToExtract);
        }

        private void ExtractModsBackgroundThread(object sender, DoWorkEventArgs e)
        {
            List<CompressedMod> mods = (List<CompressedMod>)e.Argument;
            using (var archiveFile = new SevenZipExtractor(ArchiveFilePath))
            {
                foreach (var mod in mods)
                {
                    //Todo: Extract files
                }
            }
        }

        public ICommand ImportModsCommand { get; set; }
        public ICommand CancelCommand { get; set; }
        private void LoadCommands()
        {
            ImportModsCommand = new GenericCommand(BeginImportingMods, CanImportMods);
            CancelCommand = new GenericCommand(Cancel, CanCancel);
        }

        private void Cancel()
        {
            OnClosing(EventArgs.Empty);
        }

        private bool CanCancel()
        {
            return true;
        }

        private bool CanImportMods() => TaskRunning && CompressedMods.Any(x => x.SelectedForImport);


        private void BeginImportingMods()
        {
            //Todo: Import mods
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SelectedMod_Changed(object sender, SelectionChangedEventArgs e)
        {
            SelectedMod = CompressedMods_ListBox.SelectedItem as CompressedMod;
        }
    }
}
