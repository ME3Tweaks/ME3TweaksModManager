using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.loaders;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using MemoryAnalyzer = ME3TweaksModManager.modmanager.memoryanalyzer.MemoryAnalyzer;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BatchModLibrary.xaml
    /// </summary>
    public partial class BatchModLibrary : MMBusyPanelBase, ISizeAdjustable
    {
        public BatchLibraryInstallQueue SelectedBatchQueue { get; set; }
        public Mod SelectedModInGroup { get; set; }
        public ObservableCollectionExtended<BatchLibraryInstallQueue> AvailableBatchQueues { get; } = new ObservableCollectionExtended<BatchLibraryInstallQueue>();
        public ObservableCollectionExtended<GameTargetWPF> InstallationTargetsForGroup { get; } = new ObservableCollectionExtended<GameTargetWPF>();
        public BatchModLibrary()
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"Batch Mod Installer Panel", new WeakReference(this));
            LoadCommands();
        }
        public ICommand CloseCommand { get; private set; }
        public ICommand CreateNewGroupCommand { get; private set; }
        public ICommand InstallGroupCommand { get; private set; }
        public ICommand EditGroupCommand { get; private set; }
        public ICommand DeleteGroupCommand { get; private set; }
        public bool CanCompressPackages => SelectedBatchQueue != null && SelectedBatchQueue.Game is MEGame.ME2 or MEGame.ME3;

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel);
            CreateNewGroupCommand = new GenericCommand(CreateNewGroup);
            InstallGroupCommand = new GenericCommand(InstallGroup, CanInstallGroup);
            EditGroupCommand = new GenericCommand(EditGroup, BatchQueueSelected);
            DeleteGroupCommand = new GenericCommand(DeleteGroup, BatchQueueSelected);
        }

        private void DeleteGroup()
        {
            var result = M3L.ShowDialog(mainwindow, $"Delete the '{SelectedBatchQueue.QueueName}' install group?", "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                File.Delete(Path.Combine(M3Utilities.GetBatchInstallGroupsFolder(), SelectedBatchQueue.BackingFilename));
                AvailableBatchQueues.Remove(SelectedBatchQueue);
                SelectedBatchQueue = null;
            }
        }

        private void EditGroup()
        {
            var editGroupUI = new BatchModQueueEditor(M3LoadedMods.Instance.AllLoadedMods.ToList(), mainwindow, SelectedBatchQueue);
            editGroupUI.ShowDialog();
            var newPath = editGroupUI.SavedPath;
            if (newPath != null)
            {
                //file was saved, reload
                parseBatchFiles(newPath);
            }
        }

        private bool BatchQueueSelected() => SelectedBatchQueue != null;

        private void InstallGroup()
        {
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
            var editGroupUI = new BatchModQueueEditor(M3LoadedMods.Instance.AllLoadedMods.ToList(), mainwindow);
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
            InitializeComponent();
            parseBatchFiles();
        }

        private void parseBatchFiles(string pathToHighlight = null)
        {
            AvailableBatchQueues.ClearEx();
            var batchDir = M3Utilities.GetBatchInstallGroupsFolder();
            var files = Directory.GetFiles(batchDir);
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (extension == @".biq" || extension == @".txt")
                {
                    var queue = BatchLibraryInstallQueue.ParseInstallQueue(file, M3LoadedMods.Instance.AllLoadedMods.ToList());
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

        public GameTargetWPF SelectedGameTarget { get; set; }

        private void OnSelectedBatchQueueChanged()
        {
            GameTargetWPF currentTarget = SelectedGameTarget;
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

                if (SelectedBatchQueue.Game == MEGame.ME1) SelectedBatchQueue.InstallCompressed = false;
            }
            TriggerPropertyChangedFor(nameof(CanCompressPackages));
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

        // ISizeAdjustbale Interface
        public double MaxWindowWidthPercent { get; set; } = 0.85;
        public double MaxWindowHeightPercent { get; set; } = 0.85;
        public double MaxControlWidth { get; set; } = double.NaN;
        public double MaxControlHeight { get; set; } = double.NaN;
        public double MinControlWidth { get; set; } = 0;
        public double MinControlHeight { get; set; } = 550;
    }

    public class BatchLibraryInstallQueue : INotifyPropertyChanged
    {
        public bool InstallCompressed { get; set; }
        /// <summary>
        /// The name of the batch queue file. This does not include the path.
        /// </summary>
        public string BackingFilename { get; set; }
        public ObservableCollectionExtended<Mod> ModsToInstall { get; } = new ObservableCollectionExtended<Mod>();
        public ObservableCollectionExtended<string> ModsMissing { get; } = new ObservableCollectionExtended<string>();
        public MEGame Game { get; private set; }
        public string QueueName { get; private set; }
        public string QueueDescription { get; private set; }
        
        //Fody uses this property on weaving
        #pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
        #pragma warning restore
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
                var libraryRoot = M3Utilities.GetModDirectoryForGame(result.Game);
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

            result.InstallCompressed = result.Game >= MEGame.ME2 && Settings.PreferCompressingPackages;
            return result;
        }
    }
}