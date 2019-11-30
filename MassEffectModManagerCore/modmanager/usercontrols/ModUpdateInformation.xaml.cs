using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;


namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModUpdateInformation.xaml
    /// </summary>
    public partial class ModUpdateInformation : MMBusyPanelBase
    {
        private bool AnyModUpdated;

        public ObservableCollectionExtended<OnlineContent.ModUpdateInfo> UpdatableMods { get; } = new ObservableCollectionExtended<OnlineContent.ModUpdateInfo>();

        public ModUpdateInformation(List<OnlineContent.ModUpdateInfo> modsWithUpdates)
        {
            DataContext = this;
            modsWithUpdates.ForEach(x =>
            {
                x.ApplyUpdateCommand = new RelayCommand(ApplyUpdateToMod, CanApplyUpdateToMod);
                x.DownloadButtonText = M3L.GetString(M3L.string_downloadUpdate);
            });
            UpdatableMods.AddRange(modsWithUpdates);
            LoadCommands();
            InitializeComponent();
        }

        private bool CanApplyUpdateToMod(object obj)
        {
            if (obj is OnlineContent.ModUpdateInfo ui)
            {
                return !ui.UpdateInProgress && ui.CanUpdate;
            }
            return false;
        }

        private void ApplyUpdateToMod(object obj)
        {
            if (obj is OnlineContent.ModUpdateInfo ui)
            {
                NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ModUpdaterThread-" + ui.mod.ModName);
                bw.DoWork += (a, b) =>
                {
                    ui.UpdateInProgress = true;
                    ui.Indeterminate = false;
                    ui.DownloadButtonText = M3L.GetString(M3L.string_downloading);
                    //void updateProgressCallback(long bytesReceived, long totalBytes)
                    //{
                    //    ui.By
                    //}
                    bool errorShown = false;
                    void errorCallback(string message)
                    {
                        if (!errorShown)
                        {
                            errorShown = true;
                            Application.Current.Dispatcher.Invoke(delegate { Xceed.Wpf.Toolkit.MessageBox.Show(M3L.GetString(M3L.string_interp_errorOccuredWhileUpdatingXErrorMessage, ui.mod.ModName, message), M3L.GetString(M3L.string_interp_errorUpdatingX, ui.mod.ModName), MessageBoxButton.OK, MessageBoxImage.Error); }
                            );
                        }
                    }
                    var stagingDirectory = Directory.CreateDirectory(Path.Combine(Utilities.GetTempPath(), Path.GetFileName(ui.mod.ModPath))).FullName;
                    var modUpdated = OnlineContent.UpdateMod(ui, stagingDirectory, errorCallback);
                    ui.UpdateInProgress = false;
                    ui.CanUpdate = !modUpdated;
                    AnyModUpdated |= modUpdated;
                    ui.DownloadButtonText = ui.CanUpdate ? M3L.GetString(M3L.string_downloadUpdate) : M3L.GetString(M3L.string_updated);
                    Utilities.DeleteFilesAndFoldersRecursively(stagingDirectory);
                };
                bw.RunWorkerCompleted += (a, b) => { CommandManager.InvalidateRequerySuggested(); };
                bw.RunWorkerAsync();
            }
        }


        public ICommand CloseCommand { get; set; }
        private bool TaskNotRunning() => UpdatableMods.All(x => !x.UpdateInProgress);
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(CloseDialog, TaskNotRunning);
        }

        private void CloseDialog()
        {
            OnClosing(new DataEventArgs(AnyModUpdated));
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && TaskNotRunning())
            {
                e.Handled = true;
                CloseDialog();
            }
        }

        public override void OnPanelVisible()
        {

        }
    }
}
