using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.windows;
using ME3ExplorerCore.Packages;
using Microsoft.AppCenter.Analytics;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BatchModLibrary.xaml
    /// </summary>
    public partial class BatchModLibrary : MMBusyPanelBase
    {
        public BatchLibraryInstallQueue SelectedBatchQueue { get; set; }
        public Mod SelectedModInGroup { get; set; }
        public ObservableCollectionExtended<BatchLibraryInstallQueue> AvailableBatchQueues { get; } = new ObservableCollectionExtended<BatchLibraryInstallQueue>();
        public ObservableCollectionExtended<GameTarget> InstallationTargetsForGroup { get; } = new ObservableCollectionExtended<GameTarget>();
        public BatchModLibrary()
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"Batch Mod Installer Panel", new WeakReference(this));
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }
        public ICommand CloseCommand { get; private set; }
        public ICommand CreateNewGroupCommand { get; private set; }
        public ICommand InstallGroupCommand { get; private set; }
        public ICommand EditGroupCommand { get; private set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel);
            CreateNewGroupCommand = new GenericCommand(CreateNewGroup);
            InstallGroupCommand = new GenericCommand(InstallGroup, CanInstallGroup);
            EditGroupCommand = new GenericCommand(EditGroup, CanEditGroup);
        }

        private void EditGroup()
        {
            var editGroupUI = new BatchModQueueEditor(mainwindow.AllLoadedMods.ToList(), mainwindow, SelectedBatchQueue);
            editGroupUI.ShowDialog();
            var newPath = editGroupUI.SavedPath;
            if (newPath != null)
            {
                //file was saved, reload
                parseBatchFiles(newPath);
            }
        }

        private bool CanEditGroup() => SelectedBatchQueue != null;

        private void InstallGroup()
        {
            SelectedBatchQueue.Target = SelectedGameTarget;
            Analytics.TrackEvent(@"Installing Batch Group", new Dictionary<string, string>()
            {
                {@"Group name", SelectedBatchQueue.QueueName},
                {@"Group size", SelectedBatchQueue.ModsToInstall.Count.ToString()},
                {@"Game", SelectedBatchQueue.Game.ToString()}
            });
            OnClosing(new DataEventArgs(SelectedBatchQueue));
        }

        private bool CanInstallGroup()
        {
            return SelectedGameTarget != null && SelectedBatchQueue != null;
        }

        private void CreateNewGroup()
        {
            var editGroupUI = new BatchModQueueEditor(mainwindow.AllLoadedMods.ToList(), mainwindow);
            editGroupUI.ShowDialog();
            var newPath = editGroupUI.SavedPath;
            if (newPath != null)
            {
                //file was saved, reload
                parseBatchFiles(newPath);
            }
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            parseBatchFiles();
        }

        private void parseBatchFiles(string pathToHighlight = null)
        {
            AvailableBatchQueues.ClearEx();
            var batchDir = Utilities.GetBatchInstallGroupsFolder();
            var files = Directory.GetFiles(batchDir);
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (extension == @".biq" || extension == @".txt")
                {
                    var queue = BatchLibraryInstallQueue.ParseInstallQueue(file, mainwindow.AllLoadedMods.ToList());
                    if (queue != null)
                    {
                        AvailableBatchQueues.Add(queue);
                        if (file == pathToHighlight)
                        {
                            SelectedBatchQueue = queue;
                        }
                    }
                }
            }
        }

        public GameTarget SelectedGameTarget { get; set; }

        private void OnSelectedBatchQueueChanged()
        {
            GameTarget currentTarget = SelectedGameTarget;
            SelectedGameTarget = null;
            InstallationTargetsForGroup.ClearEx();
            if (SelectedBatchQueue != null)
            {
                InstallationTargetsForGroup.AddRange(mainwindow.InstallationTargets.Where(x => x.Game == SelectedBatchQueue.Game));
                if (InstallationTargetsForGroup.Contains(currentTarget))
                {
                    SelectedGameTarget = currentTarget;
                }
                else
                {
                    SelectedGameTarget = InstallationTargetsForGroup.FirstOrDefault();
                }

                if (SelectedBatchQueue.ModsToInstall.Any())
                {
                    SelectedModInGroup = SelectedBatchQueue.ModsToInstall.First();
                }
            }
        }

        public string ModDescriptionText { get; set; }

        public void OnSelectedModInGroupChanged()
        {
            if (SelectedModInGroup == null)
            {
                ModDescriptionText = "";
            }
            else
            {
                ModDescriptionText = SelectedModInGroup.DisplayedModDescription;
            }
        }
    }

    public class BatchLibraryInstallQueue : INotifyPropertyChanged
    {
        /// <summary>
        /// Target for installation. Only used when installation commences.
        /// </summary>
        public GameTarget Target;

        public string BackingFilename { get; set; }
        public ObservableCollectionExtended<Mod> ModsToInstall { get; } = new ObservableCollectionExtended<Mod>();
        public ObservableCollectionExtended<string> ModsMissing { get; } = new ObservableCollectionExtended<string>();

        public MEGame Game { get; private set; }
        public string QueueName { get; private set; }
        public string QueueDescription { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void SaveQueue()
        {

        }
        public static BatchLibraryInstallQueue ParseInstallQueue(string queueFile, List<Mod> allLoadedMods)
        {
            if (!File.Exists(queueFile)) return null;
            BatchLibraryInstallQueue result = new BatchLibraryInstallQueue();
            result.BackingFilename = Path.GetFileName(queueFile);
            string[] lines = File.ReadAllLines(queueFile);
            int line = 0;
            if (Path.GetExtension(queueFile) == @".biq")
            {
                //New Mod Manager 6 format
                if (Enum.TryParse<MEGame>(lines[line], out var game))
                {
                    result.Game = game;
                    line++;
                }
            }
            else
            {
                //Old Mod Manager 5 format. This code is only used for transition purposes
                result.Game = MEGame.ME3;
            }

            result.QueueName = lines[line];
            line++;
            result.QueueDescription = lines[line];
            line++;
            while (line < lines.Length)
            {
                string moddescPath = lines[line];
                var libraryRoot = Utilities.GetModDirectoryForGame(result.Game);
                //workaround for 103/104 to 105: moddesc path's in biq were stored as full paths instead of relative. me3cmm is relative paths
                var fullModdescPath = File.Exists(moddescPath) ? moddescPath : Path.Combine(libraryRoot, moddescPath);

                Mod m = allLoadedMods.FirstOrDefault(x => x.ModDescPath.Equals(fullModdescPath, StringComparison.InvariantCultureIgnoreCase));
                if (m != null)
                {
                    result.ModsToInstall.Add(m);
                }
                else
                {
                    result.ModsMissing.Add(moddescPath);
                }
                line++;
            }

            return result;
        }
    }
}