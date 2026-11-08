using System.Windows.Input;
using System.Windows.Media.Animation;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.importer;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.batch;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.interfaces;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// UI for importing mods from archive
    /// </summary>
    public partial class ModArchiveImporterPanel : MMBusyPanelBase
    {
        /// <summary>
        /// Backend for import
        /// </summary>
        public ModArchiveImport MAI { get; init; }
        public string CancelButtonText { get; set; } = M3L.GetString(M3L.string_cancel);
        public string NoModSelectedText { get; set; } = M3L.GetString(M3L.string_selectAModOnTheLeftToViewItsDescription);
        public bool OTALOTTextureFilesImported { get; set; }

        // LE games do not even show this option
        public bool CanShowCompressPackages => MAI.CompressedMods.Any(x => x is Mod m && m.Game.IsOTGame());
        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanCancel())
            {
                OnClosing(DataEventArgs.Empty);
            }
        }


        public IImportableMod SelectedMod { get; set; }


        // Must be ME2 or ME3, cannot have a transform, we allow it, archive has been scanned, we haven't started an operation
        // Mods that use the updater service cannot be compressed to ensure the update checks are reliable
        // Excludes Legendary Edition games.
        public bool CanCompressPackages => MAI.CompressedMods.Any(x => x is Mod m && m.Game is MEGame.ME2 or MEGame.ME3)
                                           && MAI.CompressedMods.All(x => x is Mod m && m.ExeExtractionTransform == null && m.ModClassicUpdateCode == 0)
                                           && MAI.CurrentState == EModArchiveImportState.SCANCOMPLETED;
        /// <summary>
        /// List of mods listed in the importer panel
        /// </summary>
        public ModArchiveImporterPanel(string file, Stream archiveStream = null, NexusProtocolLink link = null)
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem($@"Mod Archive Importer ({Path.GetFileName(file)})", this);
            MAI = new ModArchiveImport()
            {
                ArchiveStream = archiveStream,
                SourceNXMLink = link,
                ArchiveFilePath = file,

                // Callbacks
                OnCompressedModAdded = CompressedModAdded,
                OnModFailedToLoad = ModFailedToLoad,
                GetPanelResult = () => Result,
                ShowDialogCallback = M3PromptCallbacks.GetUserChoiceCallback,
                ErrorCallback = M3PromptCallbacks.BlockingActionOccurred
            };
            MAI.ImportStateChanged += MAIOnImportStateChanged;
            LoadCommands();
        }
        private void ModFailedToLoad(Mod obj)
        {

        }

        private void MAIOnImportStateChanged(object sender, EventArgs e)
        {
            if (MAI.CurrentState == EModArchiveImportState.SCANCOMPLETED)
            {
                // Setup UI for scan results
                if (MAI.ScanFailureReason != null)
                {
                    NoModSelectedText = MAI.ScanFailureReason;
                }
                else if (MAI.CompressedMods.Count > 0)
                {
                    MAI.ActionText = M3L.GetString(M3L.string_selectModsToImportIntoModManagerLibrary);
                    if (MAI.CompressedMods.Count == 1)
                    {
                        CompressedMods_ListBox.SelectedIndex = 0; //Select the only item
                    }

                    // Todo: Change to link, maybe via NTFS streams?
                    // To support other providers


                    if (MAI.CompressedMods.Count == 1 && MAI.CompressedMods[0] is BatchLibraryInstallQueue)
                    {
                        ImportModsText = M3L.GetString(M3L.string_importInstallGroup);
                    }

                    TriggerPropertyChangedFor(nameof(CanCompressPackages));
                    TriggerPropertyChangedFor(nameof(CanShowCompressPackages));
                }
                else if (OTALOTTextureFilesImported)
                {
                    CancelButtonText = M3L.GetString(M3L.string_close);
                    NoModSelectedText = M3L.GetString(M3L.string_interp_dialogImportedALOTMainToTextureLibrary,
                        MAI.ScanningFile, M3Utilities.GetALOTInstallerTextureLibraryDirectory());
                    MAI.ActionText = M3L.GetString(M3L.string_importCompleted);
                }
                else
                {

                    MAI.ActionText = M3L.GetString(M3L.string_noCompatibleModsFoundInArchive);
                    if (MAI.ArchiveFilePath.EndsWith(@".exe"))
                    {
                        NoModSelectedText = M3L.GetString(M3L.string_executableModsMustBeValidatedByME3Tweaks);
                    }
                    else
                    {
                        if (Settings.GenerationSettingLE && !Settings.GenerationSettingOT)
                        {
                            // Show LE string
                            NoModSelectedText = M3L.GetString(M3L.string_noCompatibleModsFoundInArchiveLEExtended);
                        }
                        else if (!Settings.GenerationSettingLE && Settings.GenerationSettingOT)
                        {
                            // Show OT string
                            NoModSelectedText = M3L.GetString(M3L.string_noCompatibleModsFoundInArchiveOTExtended);
                        }
                        else
                        {
                            // Show combined string
                            NoModSelectedText = M3L.GetString(M3L.string_noCompatibleModsFoundInArchiveBothGensExtended);
                        }
                    }
                }
                CommandManager.InvalidateRequerySuggested();
            }

            if (MAI.CurrentState == EModArchiveImportState.COMPLETE)
            {
                OnClosing(DataEventArgs.Empty);
            }

        }

        private bool openedMultipanel = false;

        protected override void OnClosing(DataEventArgs args)
        {
            if (MAI.ArchiveStream is FileStream fs)
            {
                // Memorystream does not need disposed
                fs.Dispose();
            }
            base.OnClosing(args);
        }

        public ICommand ImportModsCommand { get; set; }
        public ICommand CancelCommand { get; set; }
        public ICommand InstallModCommand { get; set; }
        public ICommand SelectAllCommand { get; set; }
        public ICommand UnselectAllCommand { get; set; }

        public string InstallModText
        {
            get
            {
                if (SelectedMod is Mod m)
                {
                    if (m.ExeExtractionTransform != null)
                    {
                        return M3L.GetString(M3L.string_exeModsMustBeImportedBeforeInstall);
                    }
                    return M3L.GetString(M3L.string_interp_installX, SelectedMod.ModName);
                }

                return M3L.GetString(M3L.string_install);
            }
        }

        /// <summary>
        /// The text for the import mods button
        /// </summary>
        public string ImportModsText { get; set; } = M3L.GetString(M3L.string_importMods);


        private void LoadCommands()
        {
            ImportModsCommand = new GenericCommand(BeginImportingMods, CanImportMods);
            CancelCommand = new GenericCommand(Cancel, CanCancel);
            InstallModCommand = new GenericCommand(InstallCompressedMod, CanInstallCompressedMod);
            UnselectAllCommand = new GenericCommand(() => checkAll(false), CanCancel);
            SelectAllCommand = new GenericCommand(() => checkAll(true), CanCancel);
        }

        private void BeginImportingMods()
        {
            MAI.BeginImporting();
        }

        public void CompressedModAdded()
        {
            if (MAI.CompressedMods.Count > 1 && !openedMultipanel)
            {
                Storyboard sb = FindResource(@"OpenWebsitePanel") as Storyboard;
                if (sb.IsSealed)
                {
                    sb = sb.Clone();
                }
                Storyboard.SetTarget(sb, MultipleModsPopupPanel);
                sb.Begin();
                openedMultipanel = true;
            }
        }



        private bool CanInstallCompressedMod()
        {
            //This will have to pass some sort of validation code later.
            return IsPanelOpen && CompressedMods_ListBox != null
                               && CompressedMods_ListBox.SelectedItem is Mod cm
                               && cm.ExeExtractionTransform == null
                               && cm.ValidMod
                               && !cm.IsProjectMilan // Do not let users direct install Project Milan. It won't work.
                               && MAI.CurrentState == EModArchiveImportState.SCANCOMPLETED
                               && mainwindow != null // Might happen if app is closing or panel closed?
                               && mainwindow.InstallationTargets.Any(x => x.Game == cm.Game);
        }

        private void InstallCompressedMod()
        {
            OnClosing(new DataEventArgs((SelectedMod, MAI.CompressPackages)));
        }

        private void Cancel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanCancel() => MAI.CurrentState is EModArchiveImportState.SCANCOMPLETED or EModArchiveImportState.FAILED or EModArchiveImportState.COMPLETE;

        private bool CanImportMods() => MAI.CurrentState == EModArchiveImportState.SCANCOMPLETED && MAI.CompressedMods.Any(x => x.SelectedForImport && x.ValidMod);

        private void OnSelectedModChanged()
        {
            if (SelectedMod is Mod m)
            {
                if (m.Game > MEGame.ME1 && m.PreferCompressed)
                {
                    MAI.CompressPackages = true;
                }
                else if (m.Game == MEGame.ME1)
                {
                    MAI.CompressPackages = false;
                }
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            MAI.BeginScan();
        }

        private void checkAll(bool check)
        {
            foreach (var mod in MAI.CompressedMods)
            {
                mod.SelectedForImport = check;
            }
        }
    }
}
