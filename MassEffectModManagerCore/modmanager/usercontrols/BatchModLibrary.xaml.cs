using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.loaders;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.batch;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using MemoryAnalyzer = ME3TweaksModManager.modmanager.memoryanalyzer.MemoryAnalyzer;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BatchModLibrary.xaml
    /// </summary>
    public partial class BatchModLibrary : MMBusyPanelBase
    {
        public BatchLibraryInstallQueue SelectedBatchQueue { get; set; }
        public object SelectedModInGroup { get; set; }
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
        public ICommand DuplicateGroupCommand { get; private set; }
        public ICommand DeleteGroupCommand { get; private set; }
        public bool CanCompressPackages => SelectedBatchQueue != null && SelectedBatchQueue.Game is MEGame.ME2 or MEGame.ME3;

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel);
            CreateNewGroupCommand = new GenericCommand(CreateNewGroup);
            InstallGroupCommand = new GenericCommand(InstallGroup, CanInstallGroup);
            EditGroupCommand = new GenericCommand(EditGroup, BatchQueueSelected);
            DeleteGroupCommand = new GenericCommand(DeleteGroup, BatchQueueSelected);
            DuplicateGroupCommand = new GenericCommand(DuplicateGroup, BatchQueueSelected);
        }

        private void DuplicateGroup()
        {
            if (SelectedBatchQueue == null) return;

            var result = PromptDialog.Prompt(window, "Enter a new name for the duplicated install group.", "Enter new name",
                            $"{SelectedBatchQueue.QueueName} - Duplicate", true);
            if (!string.IsNullOrWhiteSpace(result))
            {
                var originalQueue = SelectedBatchQueue; // Cache in event we lose reference to this after possible reload. We don't want to set name on wrong object.
                var originalName = SelectedBatchQueue.QueueName;
                try
                {
                    var destPath = Path.Combine(M3LoadedMods.GetBatchInstallGroupsDirectory(),
                        result + Path.GetExtension(SelectedBatchQueue.BackingFilename));
                    if (!File.Exists(destPath))
                    {
                        SelectedBatchQueue.QueueName = result;
                        SelectedBatchQueue.Save(false, destPath);
                        parseBatchFiles(destPath);
                    }
                    else
                    {
                        M3L.ShowDialog(window, "An install group with this name already exists.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception e)
                {
                    M3Log.Exception(e, @"Error duplicating batch queue: ");
                    M3L.ShowDialog(window, $"Error duplicating install group: {e.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // If we reload list this variable may become null.
                    originalQueue.QueueName = originalName; // Restore if we had an error
                }
            }

        }

        private void DeleteGroup()
        {
            var result = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_deleteTheSelectedBatchQueue, SelectedBatchQueue.QueueName), M3L.GetString(M3L.string_confirmDeletion), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                File.Delete(Path.Combine(M3LoadedMods.GetBatchInstallGroupsDirectory(), SelectedBatchQueue.BackingFilename));
                AvailableBatchQueues.Remove(SelectedBatchQueue);
                SelectedBatchQueue = null;
            }
        }

        private void EditGroup()
        {
            var editGroupUI = new BatchModQueueEditor(mainwindow, SelectedBatchQueue);
            // Original code.
            editGroupUI.ShowDialog();
            var newPath = editGroupUI.SavedPath;
            if (newPath != null)
            {
                //file was saved, reload
                parseBatchFiles(newPath);
            }


#if DEBUG
            // Debug code. Requires commenting out the above.
            //editGroupUI.Show();
#endif
        }

        private bool BatchQueueSelected() => SelectedBatchQueue != null;

        private void InstallGroup()
        {
            // Has user saved options before?
            if (SelectedBatchQueue.ModsToInstall.Any(x => x.HasChosenOptions))
            {

                if (SelectedBatchQueue.ModsToInstall.Any(x => x.ChosenOptionsDesync || !x.HasChosenOptions))
                {
                    M3L.ShowDialog(window,
                        M3L.GetString(M3L.string_tooltip_batchQueueDesync),
                        M3L.GetString(M3L.string_batchQueueDesync), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    SelectedBatchQueue.UseSavedOptions = M3L.ShowDialog(window, M3L.GetString(M3L.string_usePreviouslySavedModOptionsQuestion), M3L.GetString(M3L.string_savedOptionsFound),
                                            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                }
            }

            TelemetryInterposer.TrackEvent(@"Installing Batch Group", new Dictionary<string, string>()
            {
                {@"Group name", SelectedBatchQueue.QueueName},
                {@"Group size", SelectedBatchQueue.ModsToInstall.Count.ToString()},
                {@"Game", SelectedBatchQueue.Game.ToString()},
                {@"TargetPath", SelectedGameTarget?.TargetPath}
            });
            OnClosing(new DataEventArgs(SelectedBatchQueue));
        }

        private bool CanInstallGroup()
        {
            return SelectedGameTarget != null && SelectedBatchQueue != null;
        }

        private void CreateNewGroup()
        {
            var editGroupUI = new BatchModQueueEditor(mainwindow);
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
            #region MIGRATION
            // Mod Manager 8.0.1 moved these to the mod library
            var batchDirOld = M3LoadedMods.GetBatchInstallGroupsDirectoryPre801();
            if (Directory.Exists(batchDirOld))
            {
                var oldFiles = Directory.GetFiles(batchDirOld);
                foreach (var f in oldFiles)
                {
                    M3Log.Information($@"Migrating batch queue {f} to library");
                    try
                    {
                        File.Move(f, Path.Combine(M3LoadedMods.GetBatchInstallGroupsDirectory(), Path.GetFileName(f)),
                            true);
                    }
                    catch (Exception ex)
                    {
                        M3Log.Exception(ex, @"Failed to migrate:");
                    }
                }
            }
            #endregion


            AvailableBatchQueues.ClearEx();
            var batchDir = M3LoadedMods.GetBatchInstallGroupsDirectory();
            var files = Directory.GetFiles(batchDir).ToList();

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (extension is @".biq2" or @".biq" or @".txt")
                {
                    try
                    {

                        var queue = BatchLibraryInstallQueue.ParseInstallQueue(file);
                        if (queue != null && queue.Game.IsEnabledGeneration())
                        {
                            AvailableBatchQueues.Add(queue);
                            if (file == pathToHighlight)
                            {
                                SelectedBatchQueue = queue;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        M3Log.Exception(e, @"Error occurred parsing batch queue file:");
                        Crashes.TrackError(new Exception(@"Error parsing batch queue file", e), new Dictionary<string, string>()
                        {
                            {@"Filename", file}
                        });
                        App.SubmitAnalyticTelemetryEvent("");
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
                if (SelectedModInGroup is BatchMod bm)
                {
                    ModDescriptionText = bm.Mod?.DisplayedModDescription ?? M3L.GetString(M3L.string_modNotAvailableForInstall);

                }
                else if (SelectedModInGroup is BatchASIMod bam)
                {
                    ModDescriptionText = bam.AssociatedMod?.Description ?? M3L.GetString(M3L.string_modNotAvailableForInstall);
                }
                else if (SelectedModInGroup is MEMMod mm)
                {
                    ModDescriptionText = $"This texture mod modifies the following textures:\n{string.Join('\n', mm.GetModifiedExportNames())}";
                }
                else
                {
                    ModDescriptionText = @"This batch mod type is not yet implemented"; // This doesn't need localized right now
                }
            }
        }

        // ISizeAdjustbale Interface
        public override double MaxWindowWidthPercent { get; set; } = 0.85;
        public override double MaxWindowHeightPercent { get; set; } = 0.85;
    }

}
