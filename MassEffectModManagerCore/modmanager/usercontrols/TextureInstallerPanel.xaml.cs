using System.Timers;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Helpers.MEM;
using ME3TweaksCore.Services.Shared.BasegameFileIdentification;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;


namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Quick running task that just needs a spinner
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class TextureInstallerPanel : MMBusyPanelBase
    {
        /// <summary>
        /// If installation should be aborted before it should begin.
        /// </summary>
        private bool AbortInstall;

        /// <summary>
        /// Background task for this installation
        /// </summary>
        private BackgroundTask BGTask;

        /// <summary>
        /// The text shown in the panel
        /// </summary>
        public string ActionText { get; private set; }

        /// <summary>
        /// The percentage reported by MEM
        /// </summary>
        public int PercentDone { get; set; }

        /// <summary>
        /// The path where the MEM MFL file is written
        /// </summary>
        /// <returns></returns>
        private string GetMEMMFLPath() => Path.Combine(MCoreFilesystem.GetTempDirectory(), @"meminstalllist.mfl");

        /// <summary>
        /// If the dialog saying do not install afterwards should be shown.
        /// </summary>
        public bool ShowTextureWarning { get; init; } = true;

        /// <summary>
        /// Target we are installing textures to
        /// </summary>
        public GameTarget Target { get; set; }

        /// <summary>
        /// List of files to install
        /// </summary>
        private readonly List<string> MEMFilesToInstall;

        public TextureInstallerPanel(GameTarget target, List<string> memFilesToInstall)
        {
            // 01/01/2025 - Check if MEM is running as MFL file may be locked
            if (MEMProcessHandler.IsMEMRunning())
            {
                M3Log.Warning(@"MEM appears to be running... It shouldn't be though - as we are about to write the MFL");
                MEMProcessHandler.TerminateAll(); // Might be better to ask user what to do
            }

            if (File.Exists(GetMEMMFLPath()))
            {
                try
                {
                    File.Delete(GetMEMMFLPath()); // Dies here
                }
                catch (Exception e)
                {
                    M3Log.Exception(e, @"Error deleting MFL file for MEM: ");
                    AbortInstall = true;
                    M3L.ShowDialog(Window.GetWindow(this), $"Unable to set the list of files for MEM to install: {e.Message}. Aborting installation of textures.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Write out the MFL file
            Target = target;
            ActionText = M3L.GetString(M3L.string_preparingToInstallTextures);

            MEMFilesToInstall = memFilesToInstall;
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        private const string BGFIS_TEXTURE_MODDED_SUFFIX = @"(Texture modded)";

        private Timer _keepAwakeTimer = new Timer(30 * 1000); // 30s interval

        public override void OnPanelVisible()
        {
            InitializeComponent();

            if (AbortInstall)
            {
                OnClosing(DataEventArgs.Empty);
                return;
            }

            if (Target == null || !Target.Game.IsMEGame())
            {
                M3Log.Information(@"TextureInstallerPanel: Could not determine the game for any mem mods, skipping install.");
                OnClosing(DataEventArgs.Empty);
                return;
            }

            // Validate installation
            bool canInstall = true;
            foreach (var memFile in MEMFilesToInstall)
            {
                var game = ModFileFormats.GetGameMEMFileIsFor(memFile);
                if (game != Target.Game)
                {
                    // Abort here
                    canInstall = false;
                    M3Log.Error($@"Cannot install {memFile} to {Target.Game}, it is for {game}");
                }
            }

            if (!canInstall)
            {
                M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_cannotInstallMemsNotAllSameGame, Target.Game));
                OnClosing(DataEventArgs.Empty);
                return;
            }

            if (ShowTextureWarning && !Target.TextureModded)
            {
                // Show the big scary warning
                if (!ShowTextureInstallWarning(window, false))
                {
                    M3Log.Information(@"User declined to install texture after warning");
                    OnClosing(DataEventArgs.Empty);
                    return;
                }
            }

            // Write MFL
            File.WriteAllLines(GetMEMMFLPath(), MEMFilesToInstall);

            // Perform the installation
            if (_keepAwakeTimer != null)
            {
                _keepAwakeTimer.Elapsed += keepSystemAwake;
                _keepAwakeTimer.Start();
            }

            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"TextureInstaller");
            nbw.DoWork += (a, b) =>
            {
                BGTask = BackgroundTaskEngine.SubmitBackgroundJob(@"TextureInstall", M3L.GetString(M3L.string_installingTextureMods), M3L.GetString(M3L.string_installedTextureMods));

                SetNextStep(M3L.GetString(M3L.string_checkingMassEffectModder));
                bool hasMem = MEMNoGuiUpdater.UpdateMEM(false, false, setPercentDone, failedToExtractMEM, currentTaskCallback);
                if (hasMem)
                {
                    SetNextStep(M3L.GetString(M3L.string_configuringMassEffectModder));
                    MEMIPCHandler.SetGamePath(Target);

                    // Precheck: Texture map consistency (only on already texture modded game)
                    if (Settings.EnableTextureSafetyChecks)
                    {
                        var conistencyResult = MEMIPCHandler.CheckTextureMapConsistencyAddedRemoved(Target,
                            x => ActionText = x, x => PercentDone = x, setGamePath: false);
                        if (conistencyResult != null)
                        {
                            if (conistencyResult.HasAnyErrors())
                            {
                                M3Log.Error(
                                    $@"{conistencyResult.GetErrors().Count} files have changed since the texture scan took place. You cannot modify game files outside of using Mass Effect Modder after installing textures.");
                                if (Settings.LogModInstallation || conistencyResult.GetErrors().Count < 30)
                                {
                                    foreach (var file in conistencyResult.GetErrors())
                                    {
                                        M3Log.Error($@" - {file}");
                                    }
                                }
                                else
                                {
                                    M3Log.Error(@"Turn on mod install logging in the options to log them.");
                                }

                                conistencyResult.AddFirstError(
                                    M3L.GetString(M3L.string_dialog_textureMapDesync));
                                b.Result = conistencyResult;
                                return;
                            }
                        }



                        // Check for markers
                        SetNextStep(M3L.GetString(M3L.string_preparingForTextureInstall));
                        var markerResult = MEMIPCHandler.CheckForMarkers(Target, x => ActionText = x,
                            x => PercentDone = x, setGamePath: false);
                        if (markerResult != null)
                        {
                            if (markerResult.HasAnyErrors())
                            {
                                M3Log.Error(
                                    $@"{markerResult.GetErrors().Count} leftover texture-modded files were found from a previous texture installation. These files must be removed or reverted to vanilla in order to continue installation.");

                                if (Settings.LogModInstallation || markerResult.GetErrors().Count < 30)
                                {
                                    foreach (var file in markerResult.GetErrors())
                                    {
                                        M3Log.Error($@" - {file}");
                                    }
                                }
                                else
                                {
                                    M3Log.Error(@"Turn on mod install logging in the options to log them.");
                                }

                                // Todo: Backup service specific strings.
                                markerResult.AddFirstError(
                                    M3L.GetString(M3L.string_dialog_leftoverTextureFilesFound));
                                b.Result = markerResult;
                                return;
                            }
                        }
                    }
                    else
                    {
                        M3Log.Warning(@"Texture safety checks are disabled! Do not trust the results of this installation");
                    }

                    SystemSleepManager.PreventSleep(@"TextureInstaller");

                    // Mod Manager 9: Inventory all basegame changes so we have a definitive source of files.
                    SetNextStep(M3L.GetString(M3L.string_inventoryingGameStateBeforeTextureInstall));
                    Target.PopulateModifiedBasegameFiles();
                    int numDone = 0;
                    var trackedFileToOriginalMD5Map = new CaseInsensitiveDictionary<string>(); // Map pre-texture modded -> pre-texture modded MD5
                    foreach (var f in Target.ModifiedBasegameFiles.Where(x => x.FilePath.RepresentsPackageFilePath()))
                    {
                        var path = Path.Combine(Target.TargetPath, f.FilePath);
                        var hash = MUtilities.CalculateHash(path);
                        var existingInfo = BasegameFileIdentificationService.GetBasegameFileSource(Target, path, hash);
                        if (existingInfo != null && !existingInfo.source.EndsWith(BGFIS_TEXTURE_MODDED_SUFFIX))
                        {
                            trackedFileToOriginalMD5Map[f.FilePath] = hash;
                        }

                        numDone++;
                        PercentDone = (int)(numDone * 100.0 / Target.ModifiedBasegameFiles.Count);
                    }

                    // Install the textures.
                    SetNextStep(M3L.GetString(M3L.string_preparingForTextureInstall)); // same message
                    var installResult = MEMIPCHandler.InstallMEMFiles(Target, GetMEMMFLPath(), x => ActionText = x, x => PercentDone = x, setGamePath: false);
                    TelemetryInterposer.TrackEvent(@"Installed texture mods", new Dictionary<string, string>()
                    {
                        {@"Result", installResult != null ? (installResult.ExitCode == 0 ? @"OK" : $@"Error code {installResult.ExitCode}") : @"Unknown"}
                    });
                    if (installResult != null)
                    {
                        // If 'installation' occurred (e.g. it got past scan) we need to reload the game target to ensure consistency in the UI
                        Result.ReloadTargets = installResult.IsInstallSession;
                    }

                    // Mod Manager 9: Update basegame texture hashes.
                    SetNextStep(M3L.GetString(M3L.string_inventoryingGameStateAfterTextureInstall));
                    var basegameFileDbUpdates = new List<BasegameFileRecord>();
                    numDone = 0;
                    foreach (var f in trackedFileToOriginalMD5Map.Where(x => x.Key.RepresentsPackageFilePath()))
                    {
                        var path = Path.Combine(Target.TargetPath, f.Key);
                        var existingInfo = BasegameFileIdentificationService.GetBasegameFileSource(Target, path, f.Value);
                        if (existingInfo != null) // This should never be null but we will check here anyways.
                        {
                            var newMd5 = MUtilities.CalculateHash(path);
                            var newName = existingInfo.source += $@" - {BGFIS_TEXTURE_MODDED_SUFFIX}";
                            var mm = new BasegameFileRecord(f.Key, (int)new FileInfo(path).Length, Target.Game, newName, newMd5);
                            basegameFileDbUpdates.Add(mm);
                        }
                        numDone++;
                        PercentDone = (int)(numDone * 100.0 / Target.ModifiedBasegameFiles.Count);
                    }

                    BasegameFileIdentificationService.AddLocalBasegameIdentificationEntries(basegameFileDbUpdates);
                    b.Result = installResult;
                }
                else
                {
                    // We cannot install textures!
                    throw new Exception(M3L.GetString(M3L.string_dialog_cannotDownloadMEMCannotProceed));
                }
            };

            nbw.RunWorkerCompleted += (a, b) =>
            {
                SystemSleepManager.AllowSleep();
                if (b.Error != null)
                {
                    // Logging is handled in nbw
                    BGTask.FinishedUIText = M3L.GetString(M3L.string_errorOccurredInTextureInstaller);
                    Result.Error = b.Error;
                }
                else if (b.Result is MEMSessionResult mir)
                {
                    if (mir.ExitCode != 0)
                    {
                        // This is kind of technical, but will also catch some strange edge cases user may face if MEM unexpectedly dies.
                        var exitCodeString = mir.ExitCode?.ToString() ?? M3L.GetString(M3L.string_noExitCodeBrackets);
                        mir.AddFirstError(M3L.GetString(M3L.string_dialog_memNonZeroExitCode, exitCodeString));
                    }

                    var errors = mir.GetErrors();
                    if (errors.Any() || mir.ExitCode != 0)
                    {
                        if (BGTask != null)
                        {
                            BGTask.FinishedUIText = M3L.GetString(M3L.string_textureInstallationFailed);
                        }

                        ListDialog ld = null;
                        if (mir.IsInstallSession)
                        {
                            // Messages are different.
                            ld = new ListDialog(errors.ToList(), M3L.GetString(M3L.string_textureInstallationErrors), M3L.GetString(M3L.string_dialog_textureInstallErrorsOccurred), window, width: 800);
                        }
                        else
                        {
                            // Game has not been modified
                            ld = new ListDialog(errors.ToList(), M3L.GetString(M3L.string_cannotInstallTextures), M3L.GetString(M3L.string_dialog_textureModPrecheckIssues), window, width: 800);
                        }
                        ld.ShowDialog();
                    }
                    else if (mir.ExitCode != 0)
                    {
                        // Handle here
                    }
                }

                if (BGTask != null)
                {
                    BackgroundTaskEngine.SubmitJobCompletion(BGTask);
                }

                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }

        private void keepSystemAwake(object sender, ElapsedEventArgs e)
        {
            SystemSleepManager.PreventSleep(@"TextureInstaller");
        }

        protected override void OnClosing(DataEventArgs e)
        {
            base.OnClosing(e);

            // Allow sleep when panel closes
            _keepAwakeTimer.Stop();
            _keepAwakeTimer.Elapsed -= keepSystemAwake;
            SystemSleepManager.AllowSleep();
        }

        private void SetNextStep(string nextStepText)
        {
            ActionText = nextStepText;
            PercentDone = 0;
        }

        /// <summary>
        /// Shows the scary DO NOT INSTALL AFTER THIS prompt. This must run on a UI thread!
        /// </summary>
        /// <param name="window"></param>
        /// <returns>True if user accepted, false otherwise</returns>
        public static bool ShowTextureInstallWarning(Window window, bool isBatch)
        {
            var result = M3L.ShowDialog(window,
                isBatch ? M3L.GetString(M3L.string_dialog_bigScaryTextureInstallWarningBatch)
                : M3L.GetString(M3L.string_dialog_bigScaryTextureInstallWarning),
                M3L.GetString(M3L.string_textureInstallationWarning), MessageBoxButton.YesNo, MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            return result == MessageBoxResult.Yes;
        }

        private void currentTaskCallback(string text)
        {
            ActionText = text;
        }

        private void failedToExtractMEM(Exception exception)
        {
            M3Log.Exception(exception, @"Failed to extract MassEffectModderNoGui:");
            // Should probably have a more useful message than this.
        }

        private void setPercentDone(long done, long total)
        {
            if (total > 0)
            {
                PercentDone = (int)(done * 100.0f / total);
            }
        }

        public override bool DisableM3AutoSizer { get; set; } = true;
    }
}
