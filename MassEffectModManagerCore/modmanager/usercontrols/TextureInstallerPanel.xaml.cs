using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Helpers.MEM;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
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
        private readonly BackgroundTask BGTask;
        public string ActionText { get; private set; }

        public int PercentDone { get; set; }

        /// <summary>
        /// The path where the MEM MFL file is written
        /// </summary>
        /// <returns></returns>
        private string GetMEMMFLPath() => Path.Combine(M3Filesystem.GetTempPath(), @"meminstalllist.mfl");

        private MEGame Game { get; }

        public TextureInstallerPanel(List<string> memFilesToInstall)
        {
            // Write out the MFL file
            ActionText = "Preparing to install textures";
            File.WriteAllLines(GetMEMMFLPath(), memFilesToInstall);

            int i = 0;
            while (Game == MEGame.Unknown && i < memFilesToInstall.Count)
            {
                Game = ModFileFormats.GetGameMEMFileIsFor(memFilesToInstall[i]);
                i++;
            }

            File.WriteAllLines(GetMEMMFLPath(), memFilesToInstall); // Write the MFL
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();

            if (Game == MEGame.Unknown)
            {
                M3Log.Information(@"TextureInstallerPanel: Could not determine the game for any mem mods, skipping install.");
                OnClosing(DataEventArgs.Empty);
                return;
            }

            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"TextureInstaller");
            nbw.DoWork += (a, b) =>
            {
                bool hasMem = MEMNoGuiUpdater.UpdateMEM(false, false, setPercentDone, failedToExtractMEM, currentTaskCallback);
                if (hasMem)
                {
                    // Todo: Figure out how to make MEM take a game path to support targets

                    MEMIPCHandler.InstallMEMFiles(Game, GetMEMMFLPath(), x => ActionText = x, x => PercentDone = x);
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
