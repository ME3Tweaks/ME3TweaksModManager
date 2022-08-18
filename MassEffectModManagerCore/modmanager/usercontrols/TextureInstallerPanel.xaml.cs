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
        private Action runAndDoneDelegate;

        private readonly BackgroundTask BGTask;
        public string ActionText { get; private set; }

        public int PercentDone { get; set; }
        public TextureInstallerPanel()
        {

        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            OpenFileDialog m = new OpenFileDialog
            {
                Title = @"Select texture file (.mem)",
                Filter = M3L.GetString(M3L.string_massEffectModderFiles) + @"|*.mem"
            };

            var result = m.ShowDialog();
            if (result.HasValue && result.Value && File.Exists(m.FileName))
            {
                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"TextureInstaller");
                nbw.DoWork += (a, b) =>
                {
                    bool hasMem = MEMNoGuiUpdater.UpdateMEM(false, false, setPercentDone, failedToExtractMEM, currentTaskCallback);
                    if (hasMem)
                    {
                        var game = ModFileFormats.GetGameMEMFileIsFor(m.FileName);
                        
                        // Todo: Figure out how to make MEM take a game path


                        Debug.WriteLine(game);
                        MEMIPCHandler.InstallMEMFile(m.FileName, x => ActionText = x, x => PercentDone = x);
                        Result.ReloadTargets = true;
                    }
                    runAndDoneDelegate?.Invoke();
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
            else
            {
                OnClosing(DataEventArgs.Empty);
            }
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
