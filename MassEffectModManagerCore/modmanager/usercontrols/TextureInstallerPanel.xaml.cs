using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using AdonisUI.Controls;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Helpers.MEM;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;
using Microsoft.Win32;
using PropertyChanged;
using MEMIPCHandler = ME3TweaksCore.Helpers.MEM.MEMIPCHandler;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Quick running task that just needs a spinner
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class TextureInstallerPanel : MMBusyPanelBase
    {
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
        private string GetMEMMFLPath() => Path.Combine(M3Filesystem.GetTempPath(), @"meminstalllist.mfl");


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
            if (File.Exists(GetMEMMFLPath()))
                File.Delete(GetMEMMFLPath());

            // Write out the MFL file
            Target = target;
            ActionText = M3L.GetString(M3L.string_preparingToInstallTextures);

            MEMFilesToInstall = memFilesToInstall;

        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();

            if (!Target.Game.IsMEGame())
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

            if (!Target.TextureModded)
            {
                // Show the big scary warning
                if (!ShowTextureInstallWarning())
                {
                    M3Log.Information(@"User declined to install texture after warning");
                    OnClosing(DataEventArgs.Empty);
                    return;
                }
            }

            // Write MFL
            File.WriteAllLines(GetMEMMFLPath(), MEMFilesToInstall);

            // Perform the installation
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"TextureInstaller");
            nbw.DoWork += (a, b) =>
            {
                BGTask = BackgroundTaskEngine.SubmitBackgroundJob(@"TextureInstall", M3L.GetString(M3L.string_installingTextureMods), M3L.GetString(M3L.string_installedTextureMods));
                bool hasMem = MEMNoGuiUpdater.UpdateMEM(false, false, setPercentDone, failedToExtractMEM, currentTaskCallback);
                if (hasMem)
                {
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

                    var installResult = MEMIPCHandler.InstallMEMFiles(Target, GetMEMMFLPath(), x => ActionText = x, x => PercentDone = x, setGamePath: false);
                    if (installResult != null)
                    {
                        // If 'installation' occurred (e.g. it got past scan) we need to reload the game target to ensure consistency in the UI
                        Result.ReloadTargets = installResult.IsInstallSession;
                    }
                    b.Result = installResult;
                }
            };

            nbw.RunWorkerCompleted += (a, b) =>
            {
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

        private bool ShowTextureInstallWarning()
        {
            var result = M3L.ShowDialog(window,
                M3L.GetString(M3L.string_dialog_bigScaryTextureInstallWarning),
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
