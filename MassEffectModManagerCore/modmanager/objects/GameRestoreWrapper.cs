using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore;
using MassEffectModManagerCore.modmanager.localizations;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCore.Services.Restore;
using ME3TweaksCore.Targets;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.UI;
using Microsoft.WindowsAPICodePack.Dialogs;
using Pathoschild.FluentNexus.Models;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// M3 wrapper class that handles the UI flow of a game restore
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class GameRestoreWrapper
    {
        /// <summary>
        /// Window to center dialogs onto
        /// </summary>
        private MainWindow window;

        /// <summary>
        /// The main restore controller that handles the actual restore operation.
        /// </summary>
        public GameRestore RestoreController { get; init; }

        /// <summary>
        /// The status object for the backup
        /// </summary>
        public GameBackupStatus BackupStatus { get; init; }

        /// <summary>
        /// The list of targets in the dropdown.
        /// </summary>
        public ObservableCollectionExtended<GameTargetWPF> AvailableRestoreTargets { get; } = new();

        /// <summary>
        /// The current selected target in the dropdown
        /// </summary>
        public GameTargetWPF RestoreTarget { get; set; }

        /// <summary>
        /// User displayable name of the game.
        /// </summary>
        public string GameTitle => RestoreController.Game.ToGameName();

        /// <summary>
        /// If the progress bar shown should be indeterminate
        /// </summary>
        public bool ProgressIndeterminate { get; private set; }

        /// <summary>
        /// If restoring the game will restore Texture LOD settings
        /// </summary>
        [AlsoNotifyFor(nameof(RestoreTarget))]
        public bool WillRestoreTextureLODs
        {
            get
            {
                if (RestoreTarget == null || RestoreTarget.Game.IsLEGame() || RestoreTarget.Game == MEGame.LELauncher) return false;
                return RestoreTarget.TextureModded;
            }
        }

        /// <summary>
        /// If the user can select anything in the dropdown
        /// </summary>
        public bool CanOpenDropdown => !RestoreController.RestoreInProgress && BackupStatus.BackedUp;

        public string RestoreButtonText
        {
            get
            {
                if (RestoreTarget != null && BackupStatus.BackedUp) return M3L.GetString(M3L.string_restoreThisTarget);
                if (RestoreTarget == null && BackupStatus.BackedUp) return M3L.GetString(M3L.string_selectTarget);
                if (!BackupStatus.BackedUp) return M3L.GetString(M3L.string_noBackup);
                return M3L.GetString(M3L.string_error);
            }
        }

        public GenericCommand RestoreButtonCommand { get; init; }

        private object syncObj = new object();

        public GameRestoreWrapper(MEGame game, IEnumerable<GameTargetWPF> availableTargets, MainWindow window)
        {
            this.window = window;
            RestoreButtonCommand = new GenericCommand(BeginRestore, () => RestoreTarget != null);
            BackupStatus = BackupService.GetBackupStatus(game);
            RestoreController = new GameRestore(game)
            {
                BlockingErrorCallback = (message, title) =>
                {
                    M3L.ShowDialog(window, title, message, MessageBoxButton.OK, MessageBoxImage.Error);
                },
                ConfirmationCallback = (message, title) =>
                {
                    bool response = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        response = M3L.ShowDialog(window, title, message, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
                        //lock (syncObj)
                        //{
                        //    Monitor.Pulse(syncObj);
                        //}
                    });
                    //lock (syncObj)
                    //{
                    //    Monitor.Wait(syncObj);
                    //}

                    return response;
                },
                SelectDestinationDirectoryCallback = (title, message) =>
                {
                    string selectedPath = null;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Not sure if this has to be synced
                        CommonOpenFileDialog ofd = new CommonOpenFileDialog()
                        {
                            Title = M3L.GetString(M3L.string_selectNewRestoreDestination),
                            IsFolderPicker = true,
                            EnsurePathExists = true
                        };
                        if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
                        {
                            selectedPath = ofd.FileName;
                        }
                    });
                    return selectedPath;
                },
                RestoreErrorCallback = (title, message) =>
                {
                    M3L.ShowDialog(window, title, message, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            AvailableRestoreTargets.AddRange(availableTargets);
            AvailableRestoreTargets.Add(new GameTargetWPF(game, M3L.GetString(M3L.string_restoreToCustomLocation), false, true));
            //RestoreTarget = AvailableRestoreTargets.FirstOrDefault(); // Leave it so it's blank default otherwise we get the 'Restoring from backup will reset LODs' thing.
        }

        private void BeginRestore()
        {
            Task.Run(() =>
            {
                RestoreController.PerformRestore(RestoreTarget, RestoreTarget.TargetPath);
            });
        }
    }
}
