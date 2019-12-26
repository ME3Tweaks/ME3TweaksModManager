using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModMakerPanel.xaml
    /// </summary>
    public partial class ModMakerPanel : MMBusyPanelBase
    {
        public string ModMakerCode { get; set; }
        public int CurrentTaskValue { get; private set; }
        public int CurrentTaskMaximum { get; private set; } = 100;
        public int CurrentTaskIndeterminate { get; private set; }
        public int OverallValue { get; private set; }
        public int OverallMaximum { get; private set; } = 100;
        public int OverallIndeterminate { get; private set; }
        public bool CompileInProgress { get; set; }
        public ModMakerPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }
        public ICommand DownloadCompileCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }
        private void LoadCommands()
        {
            DownloadCompileCommand = new GenericCommand(StartCompiler, CanStartCompiler);
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanClose() => !CompileInProgress;

        private void StartCompiler()
        {
            CompileInProgress = true;
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ModmakerCompiler");

            bw.DoWork += (a, b) =>
            {
                if (int.TryParse(ModMakerCode, out var code))
                {
                    var compiler = new ModMakerCompiler(code);
                    compiler.DownloadAndCompileMod();
                }
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                CompileInProgress = false;
            };
            bw.RunWorkerAsync();
        }

        private bool CanStartCompiler() => int.TryParse(ModMakerCode, out var _) && !CompileInProgress;

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {

        }
    }
}
