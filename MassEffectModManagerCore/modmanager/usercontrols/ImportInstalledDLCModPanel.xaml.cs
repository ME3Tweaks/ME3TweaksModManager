using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using ByteSizeLib;
using Flurl;
using Flurl.Http;
using IniParser.Model;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Pathoschild.FluentNexus.Models;
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

        private bool CanImportSelectedFolder() => SelectedDLCFolder != null && !string.IsNullOrWhiteSpace(ModNameText) && !SelectedTarget.ALOTInstalled;

        private void ImportSelectedFolder()
        {
            //Check destination path
            var destinationName = Utilities.SanitizePath(ModNameText);
            if (string.IsNullOrWhiteSpace(destinationName))
            {
                //cannot use this name
                Log.Error("Invalid mod name: " + ModNameText);
                Xceed.Wpf.Toolkit.MessageBox.Show(mainwindow, "The specified mod name cannot be used as it will result in an invalid foldername on the filesystem. Please choose a different name.", "Invalid mod name", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //Check free space.
            var sourceDir = Path.Combine(MEDirectories.DLCPath(SelectedTarget), SelectedDLCFolder.DLCFolderName);
            var library = Utilities.GetModDirectoryForGame(SelectedTarget.Game);
            if (Utilities.DriveFreeBytes(library, out var freeBytes))
            {
                //Check enough space
                var sourceSize = Utilities.GetSizeOfDirectory(sourceDir);
                if (sourceSize > (long)freeBytes)
                {
                    //Not enough space
                    Log.Error($@"Not enough disk space to import mod. Required space: {ByteSize.FromBytes(sourceSize)}, available space: {ByteSize.FromBytes(freeBytes)}");
                    Xceed.Wpf.Toolkit.MessageBox.Show(mainwindow, $"There is not enough space on {Path.GetPathRoot(library)} to import this mod.\n\nRequired space: {ByteSize.FromBytes(sourceSize)}\nFree space: {ByteSize.FromBytes(freeBytes)}", "Insufficient free disk space", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            //Check directory doesn't exist already
            var outDir = Path.Combine(library, destinationName);
            if (Directory.Exists(outDir))
            {
                var okToDelete = Xceed.Wpf.Toolkit.MessageBox.Show(mainwindow, $"There is already an existing mod at the following location:\n{outDir}\n\nImporting this mod with this name will delete and overwrite this mod in your library. If this is not intentional, please choose a different name.\n\nDelete the existing mod and import the installed one?", "Same name mod in library", MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
                    Xceed.Wpf.Toolkit.MessageBox.Show(mainwindow, $"Could not delete existing mod directory: {e.Message}", "Error deleting mod folder", MessageBoxButton.OK, MessageBoxImage.Error);
                    return; //abort
                }
            }

            NamedBackgroundWorker bw = new NamedBackgroundWorker("GameDLCModImporter");
            bw.DoWork += ImportDLCFolder_BackgroundThread;
            bw.RunWorkerCompleted += (a, b) =>
            {
                Analytics.TrackEvent("Imported a mod from game installation", new Dictionary<string, string>()
                {
                    {"Game", SelectedTarget.Game.ToString()},
                    {"Folder", SelectedDLCFolder.DLCFolderName}
                });
                OperationInProgress = false;
                OnClosing(new DataEventArgs(b.Result));
            };
            bw.RunWorkerAsync();
        }

        private async void ImportDLCFolder_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            OperationInProgress = true;
            var sourceDir = Path.Combine(MEDirectories.DLCPath(SelectedTarget), SelectedDLCFolder.DLCFolderName);
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
            ini[@"ModInfo"][@"moddev"] = "Imported from game";
            ini[@"ModInfo"][@"moddesc"] = $"This mod was imported from an installation of {Utilities.GetGameName(SelectedTarget.Game)} at {DateTime.Now}.";
            ini[@"ModInfo"][@"modver"] = "Unknown";

            ini[@"CUSTOMDLC"][@"sourcedirs"] = SelectedDLCFolder.DLCFolderName;
            ini[@"CUSTOMDLC"][@"destdirs"] = SelectedDLCFolder.DLCFolderName;


            var moddescPath = Path.Combine(modFolder, @"moddesc.ini");
            File.WriteAllText(moddescPath, ini.ToString());

            //Generate and load mod
            Mod m = new Mod(moddescPath, Mod.MEGame.ME3);
            e.Result = m;
            Log.Information("Mod import complete.");

            if (!CurrentModInTPMI)
            {
                //Submit telemetry to ME3Tweaks
                try
                {
                    TPMITelemetrySubmissionForm.TelemetryPackage tp = TPMITelemetrySubmissionForm.GetTelemetryPackageForDLC(SelectedTarget.Game,
                        MEDirectories.DLCPath(SelectedTarget),
                        SelectedDLCFolder.DLCFolderName,
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
            if (SelectedDLCFolder != null && !SelectedTarget.ALOTInstalled)
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
            InstallationTargets.ReplaceAll(mainwindow.InstallationTargets);
            SelectedTarget = InstallationTargets.FirstOrDefault();
        }
    }
}
