using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
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
            ActionText = "Preparing to install textures";

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
                M3L.ShowDialog(window, $"Cannot install texture mods:\nNot all .mem files selected for install are for {Target.Game}.");
                OnClosing(DataEventArgs.Empty);
                return;
            }

            // Write MFL
            File.WriteAllLines(GetMEMMFLPath(), MEMFilesToInstall);

            // Perform the installation
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"TextureInstaller");
            nbw.DoWork += (a, b) =>
            {
                BGTask = BackgroundTaskEngine.SubmitBackgroundJob("TextureInstall", "Installing texture mods", "Installed texture mods");
                bool hasMem = MEMNoGuiUpdater.UpdateMEM(false, false, setPercentDone, failedToExtractMEM, currentTaskCallback);
                if (hasMem)
                {
                    // Check for markers
                    var markerResult = MEMIPCHandler.CheckForMarkers(Target, x => ActionText = x, x => PercentDone = x);
                    if (markerResult != null)
                    {
                        if (markerResult.HasAnyErrors())
                        {
                            M3Log.Error($@"{markerResult.GetErrors().Count} leftover texture-modded files were found from a previous texture installation. These files must be removed or reverted to vanilla in order to continue installation.");

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
                            markerResult.AddFirstError("The following files are leftover from a different texture installation. This is not supported; reset your game to vanilla, reinstall your non-texture mods, then install textures again.");
                            b.Result = markerResult;
                            return;
                        }
                    }

                    b.Result = MEMIPCHandler.InstallMEMFiles(Target, GetMEMMFLPath(), x => ActionText = x, x => PercentDone = x, setGamePath: false);
                    Result.ReloadTargets = true;
                }
            };

            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    // Logging is handled in nbw
                    Result.Error = b.Error;
                }
                else if (b.Result is MEMSessionResult mir)
                {
                    var errors = mir.GetErrors();
                    if (errors.Any())
                    {
                        if (BGTask != null)
                        {
                            BGTask.FinishedUIText = "Texture installation failed";
                        }

                        ListDialog ld = new ListDialog(errors.ToList(), "Texture install errors", "The following errors were reported when Mass Effect Modder installed textures. More information can be found in Mod Manager's application log.\nMass Effect Modder is not developed by ME3Tweaks.", window);
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

        private void currentTaskCallback(string text)
        {
            ActionText = text;
        }

        private void failedToExtractMEM(Exception obj)
        {

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
