using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using Microsoft.AppCenter.Analytics;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ImportInstalledDLCModPanel.xaml
    /// </summary>
    public partial class ImportInstalledDLCModPanel : MMBusyPanelBase
    {
        public GameTarget SelectedTarget { get; set; }
        public InstallationInformation.InstalledDLCMod SelectedDLCFolder { get; set; }
        public ObservableCollectionExtended<InstallationInformation.InstalledDLCMod> InstalledDLCModsForGame { get; } = new ObservableCollectionExtended<InstallationInformation.InstalledDLCMod>();
        public ObservableCollectionExtended<GameTarget> InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ImportInstalledDLCModPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }
        public string ModSiteText { get; set; }
        public string ModNameText { get; set; }

        public bool OperationInProgress { get; set; }
        public bool CurrentModInTPMI { get; set; } = true; // until an item is selected, don't show the uncataloged item
        public ICommand ImportSelectedDLCFolderCommand { get; set; }
        public ICommand CloseCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
            ImportSelectedDLCFolderCommand = new GenericCommand(ImportSelectedFolder, CanImportSelectedFolder);
        }

        private bool CanImportSelectedFolder() => SelectedDLCFolder != null && !string.IsNullOrWhiteSpace(ModNameText) && !SelectedTarget.TextureModded;

        private void ImportSelectedFolder()
        {
            //Check destination path
            var destinationName = Utilities.SanitizePath(ModNameText);
            if (string.IsNullOrWhiteSpace(destinationName))
            {
                //cannot use this name
                Log.Error(@"Invalid mod name: " + ModNameText);
                M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_dialog_invalidModNameWillResolveToNothing), M3L.GetString(M3L.string_invalidModName), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //Check free space.
            var sourceDir = Path.Combine(M3Directories.GetDLCPath(SelectedTarget), SelectedDLCFolder.DLCFolderName);
            var library = Utilities.GetModDirectoryForGame(SelectedTarget.Game);
            if (Utilities.DriveFreeBytes(library, out var freeBytes))
            {
                //Check enough space
                var sourceSize = Utilities.GetSizeOfDirectory(sourceDir);
                if (sourceSize > (long)freeBytes)
                {
                    //Not enough space
                    Log.Error($@"Not enough disk space to import mod. Required space: {FileSize.FormatSize(sourceSize)}, available space: {FileSize.FormatSize(freeBytes)}");
                    M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_insufficientDiskSpaceToImport, Path.GetPathRoot(library), FileSize.FormatSize(sourceSize), FileSize.FormatSize(freeBytes)), M3L.GetString(M3L.string_insufficientFreeDiskSpace), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            //Check directory doesn't exist already
            var outDir = Path.Combine(library, destinationName);
            if (Directory.Exists(outDir))
            {
                var okToDelete = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_dialog_importingWillDeleteExistingMod, outDir), M3L.GetString(M3L.string_sameNamedModInLibrary), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (okToDelete == MessageBoxResult.No)
                {
                    return; //cancel
                }

                try
                {
                    Utilities.DeleteFilesAndFoldersRecursively(outDir);
                }
                catch (Exception e)
                {
                    M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_couldNotDeleteExistingModDirectory, e.Message), M3L.GetString(M3L.string_errorDeletingModFolder), MessageBoxButton.OK, MessageBoxImage.Error);
                    return; //abort
                }
            }

            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"GameDLCModImporter");
            nbw.DoWork += ImportDLCFolder_BackgroundThread;
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                else
                {

                    if (b.Error == null && b.Result != null)
                    {
                        Analytics.TrackEvent(@"Imported a mod from game installation", new Dictionary<string, string>()
                        {
                            {@"Game", SelectedTarget.Game.ToString()},
                            {@"Folder", SelectedDLCFolder.DLCFolderName}
                        });
                    }

                    OperationInProgress = false;
                    if (b.Error == null && b.Result != null)
                    {
                        OnClosing(new DataEventArgs(b.Result)); //avoid accessing b.Result if error occurred
                    }
                }
            };
            nbw.RunWorkerAsync();
        }

        private async void ImportDLCFolder_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            OperationInProgress = true;
            var sourceDir = Path.Combine(M3Directories.GetDLCPath(SelectedTarget), SelectedDLCFolder.DLCFolderName);
            // Check for MEMI, we will not allow importing files with MEMI
            foreach (var file in Directory.GetFiles(sourceDir, @"*.*", SearchOption.AllDirectories))
            {
                if (file.RepresentsPackageFilePath() && Utilities.HasALOTMarker(file))
                {
                    Log.Error($@"Found a file marked as texture modded: {file}. These files cannot be imported into mod manager");
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_cannotImportModTextureMarkersFound), M3L.GetString(M3L.string_cannotImportMod), MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return;
                }
            }



            var library = Utilities.GetModDirectoryForGame(SelectedTarget.Game);
            var destinationName = Utilities.SanitizePath(ModNameText);
            var modFolder = Path.Combine(library, destinationName);
            var copyDestination = Path.Combine(modFolder, SelectedDLCFolder.DLCFolderName);
            var outInfo = Directory.CreateDirectory(copyDestination);
            Log.Information($@"Importing mod: {sourceDir} -> {copyDestination}");

            int numToDo = 0;
            int numDone = 0;

            void totalItemToCopyCallback(int total)
            {
                numToDo = total;
                ProgressBarMax = total;
            }

            void fileCopiedCallback()
            {
                numDone++;
                ProgressBarValue = numDone;
            }

            CopyDir.CopyAll_ProgressBar(new DirectoryInfo(sourceDir), outInfo, totalItemToCopyCallback, fileCopiedCallback);

            //Write a moddesc
            IniData ini = new IniData();
            ini[@"ModManager"][@"cmmver"] = App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture); //prevent commas
            ini[@"ModInfo"][@"game"] = SelectedTarget.Game.ToString();
            ini[@"ModInfo"][@"modname"] = ModNameText;
            ini[@"ModInfo"][@"moddev"] = M3L.GetString(M3L.string_importedFromGame);
            ini[@"ModInfo"][@"moddesc"] = M3L.GetString(M3L.string_defaultDescriptionForImportedMod, Utilities.GetGameName(SelectedTarget.Game), DateTime.Now);
            ini[@"ModInfo"][@"modver"] = M3L.GetString(M3L.string_unknown);
            ini[@"ModInfo"][@"unofficial"] = @"true";
            ini[@"ModInfo"][@"importedby"] = App.BuildNumber.ToString();
            ini[@"CUSTOMDLC"][@"sourcedirs"] = SelectedDLCFolder.DLCFolderName;
            ini[@"CUSTOMDLC"][@"destdirs"] = SelectedDLCFolder.DLCFolderName;


            var moddescPath = Path.Combine(modFolder, @"moddesc.ini");
            File.WriteAllText(moddescPath, ini.ToString());

            //Generate and load mod
            objects.mod.Mod m = new objects.mod.Mod(moddescPath, MEGame.ME3);
            e.Result = m;
            Log.Information(@"Mod import complete.");
            Analytics.TrackEvent(@"Imported already installed mod", new Dictionary<string, string>()
            {
                {@"Mod name", m.ModName},
                {@"Game", SelectedTarget.Game.ToString()},
                {@"Folder name", SelectedDLCFolder.DLCFolderName}
            });

            if (!CurrentModInTPMI)
            {
                //Submit telemetry to ME3Tweaks
                try
                {
                    TPMITelemetrySubmissionForm.TelemetryPackage tp = TPMITelemetrySubmissionForm.GetTelemetryPackageForDLC(SelectedTarget.Game,
                        M3Directories.GetDLCPath(SelectedTarget),
                        SelectedDLCFolder.DLCFolderName,
                        SelectedDLCFolder.DLCFolderName, //same as foldername as this is already installed
                        ModNameText,
                        @"N/A",
                        ModSiteText,
                        null
                    );

                    tp.SubmitPackage();
                }
                catch (Exception ex)
                {
                    Log.Error(@"Cannot submit telemetry: " + ex.Message);
                }
            }
        }

        public int ProgressBarValue { get; set; }

        public int ProgressBarMax { get; set; } = 100; //Default so it doesn't appear full on start

        public void OnSelectedDLCFolderChanged()
        {
            ModSiteText = "";
            if (SelectedDLCFolder != null && SelectedTarget != null && !SelectedTarget.TextureModded)
            {
                App.ThirdPartyIdentificationService[SelectedTarget.Game.ToString()].TryGetValue(SelectedDLCFolder.DLCFolderName, out var tpmi);
                CurrentModInTPMI = tpmi != null;
                if (CurrentModInTPMI)
                {
                    ModNameText = tpmi.modname;
                }
                else
                {
                    ModNameText = "";
                }

                //Check ALOT
            }
            else
            {
                CurrentModInTPMI = true; //Hide UI
            }
        }

        public void OnSelectedTargetChanged()
        {
            if (SelectedTarget != null)
            {
                SelectedTarget.PopulateDLCMods(false, modNamePrefersTPMI: true);
                InstalledDLCModsForGame.ReplaceAll(SelectedTarget.UIInstalledDLCMods.OrderBy(x => x.InstalledByManagedSolution));
            }
            else
            {
                InstalledDLCModsForGame.ClearEx();
            }
        }

        private bool CanClosePanel() => !OperationInProgress;

        private void ClosePanel() => OnClosing(DataEventArgs.Empty);

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClosePanel())
            {
                ClosePanel();
            }
        }

        public override void OnPanelVisible()
        {
            InstallationTargets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Selectable));
            SelectedTarget = InstallationTargets.FirstOrDefault();
        }
    }
}
