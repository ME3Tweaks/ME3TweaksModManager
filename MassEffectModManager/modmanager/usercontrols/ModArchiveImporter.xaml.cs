using MassEffectModManager.modmanager.helpers;
using MassEffectModManager.modmanager.objects;
using MassEffectModManager.ui;
using Serilog;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MassEffectModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModArchiveImporter.xaml
    /// </summary>
    public partial class ModArchiveImporter : UserControl, INotifyPropertyChanged
    {
        public event EventHandler Close;
        protected virtual void OnClosing(EventArgs e)
        {
            EventHandler handler = Close;
            handler?.Invoke(this, e);
        }

        public CompressedMod SelectedMod { get; private set; }
        public string Action { get; set; } = "Scanning archive";
        public ObservableCollectionExtended<CompressedMod> CompressedMods { get; } = new ObservableCollectionExtended<CompressedMod>();
        public ModArchiveImporter()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public void InspectArchiveFile(string filepath)
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("ModArchiveInspector");
            bw.DoWork += InspectArchiveBackgroundThread;
            bw.RunWorkerCompleted += (a, b) =>
            {
                Action = $"Scanned {Path.GetFileName(filepath)}";
            };
            Action = $"Scanning {Path.GetFileName(filepath)}";

            bw.RunWorkerAsync(filepath);
        }

        private void InspectArchiveBackgroundThread(object sender, DoWorkEventArgs e)
        {
            string filepath = (string)e.Argument;
            using (ArchiveFile archiveFile = new ArchiveFile(filepath))
            {
                var moddesciniEntries = new List<Entry>();
                foreach (var entry in archiveFile.Entries)
                {
                    string fname = Path.GetFileName(entry.FileName);
                    if (fname.Equals("moddesc.ini", StringComparison.InvariantCultureIgnoreCase))
                    {
                        moddesciniEntries.Add(entry);
                    }
                }

                if (moddesciniEntries.Count > 0)
                {
                    foreach (var entry in moddesciniEntries)
                    {
                        MemoryStream ms = new MemoryStream();
                        entry.Extract(ms);
                        ms.Position = 0;
                        StreamReader reader = new StreamReader(ms);
                        try
                        {
                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                CompressedMods.Add(new CompressedMod(new Mod(ms, ignoreLoadErrors: true)));
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
                else
                {
                    //Todo: Run unofficially supported scan
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

        private bool CanImportMods()
        {
            //todo: hook this up
            return false;
        }

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
