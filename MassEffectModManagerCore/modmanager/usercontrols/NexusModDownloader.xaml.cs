using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for NexusModDownloader.xaml
    /// </summary>
    public partial class NexusModDownloader : MMBusyPanelBase
    {
        public ObservableCollectionExtended<ModDownload> Downloads { get; } = new ObservableCollectionExtended<ModDownload>();
        public NexusModDownloader(string initialNxmLink)
        {
            AddDownload(initialNxmLink);
            LoadCommands();
            InitializeComponent();
        }


        private void LoadCommands()
        {
            CancelDownloadCommand = new GenericCommand(CancelDownload);
        }

        private void CancelDownload()
        {

        }

        public GenericCommand CancelDownloadCommand { get; set; }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {

        }

        public void AddDownload(string nxmLink)
        {
            Log.Information($"Queueing nxmlink {nxmLink}");
            var dl = new ModDownload(nxmLink);
            dl.OnInitialized += ModInitialized;
            dl.OnModDownloaded += ModDownloaded;
            Downloads.Add(dl);
            dl.Initialize();
        }

        private void ModDownloaded(object? sender, DataEventArgs e)
        {
            if (sender is ModDownload md)
            {
                md.OnModDownloaded -= ModDownloaded;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OnClosing(new DataEventArgs(new List<ModDownload>(new[] { md })));
                });
            }
        }

        private void ModInitialized(object? sender, EventArgs e)
        {
            if (sender is ModDownload initializedItem)
            {
                Log.Information($"Mod has initialized: {initializedItem.ModFile.Name}");
                var nextDownload = Downloads.FirstOrDefault(x => !x.Downloaded);
                if (nextDownload != null)
                {
                    nextDownload.StartDownload();
                }
            }
        }
    }
}
