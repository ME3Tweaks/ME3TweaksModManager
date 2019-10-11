using MassEffectModManager;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ArchiveDeployment.xaml
    /// </summary>
    public partial class ArchiveDeployment : UserControl, INotifyPropertyChanged
    {
        private MainWindow mainWindow;

        public Mod ModBeingDeployed { get; }

        public ArchiveDeployment(Mod mod, MainWindow mainWindow)
        {
            DataContext = this;
            this.mainWindow = mainWindow;
            ModBeingDeployed = mod;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand DeployCommand { get; set; }
        private void LoadCommands()
        {
            DeployCommand = new GenericCommand(StartDeployment, CanDeploy);
        }

        private void StartDeployment()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("ModDeploymentThread");
            bw.DoWork += Deployment_BackgroundThread;
            bw.RunWorkerAsync();
            mainWindow.UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VISIBILITY, Visibility.Visible));
            mainWindow.UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_INDETERMINATE, false));
            mainWindow.UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_MAX, 100));
        }

        private void Deployment_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences();
            //Key is in-archive path, value is on disk path
            var archiveMapping = referencedFiles.ToDictionary(x => x, x => Path.Combine(ModBeingDeployed.ModPath, x));
            var compressor = new SevenZip.SevenZipCompressor();

            compressor.CustomParameters.Add("s", "on");
            compressor.Compressing += (a, b) =>
            {

                mainWindow.UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VALUE, (int)b.PercentDone));
            };
            compressor.CompressFileDictionary(archiveMapping, @"C:\users\public\deploytest.7z");
        }

        private bool CanDeploy()
        {
            return true;
        }



        #region Closing and INotify
        public event EventHandler<DataEventArgs> Close;
        public event PropertyChangedEventHandler PropertyChanged;
        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            OnClosing(new DataEventArgs());
        }
        protected virtual void OnClosing(DataEventArgs e)
        {
            EventHandler<DataEventArgs> handler = Close;
            handler?.Invoke(this, e);
        }
        #endregion
    }
}
