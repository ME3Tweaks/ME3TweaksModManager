using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for NexusModDownloader.xaml
    /// </summary>
    public partial class NexusModDownloader : MMBusyPanelBase
    {
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly string pendingLink;
        public ObservableCollectionExtended<ModDownload> Downloads { get; } = new ObservableCollectionExtended<ModDownload>();
        public NexusModDownloader(string initialNxmLink)
        {
            pendingLink = initialNxmLink;
            LoadCommands();
        }


        private void LoadCommands()
        {
            CancelDownloadCommand = new GenericCommand(CancelDownload);
        }

        private void CancelDownload()
        {
            cancellationTokenSource.Cancel();
        }

        public GenericCommand CancelDownloadCommand { get; set; }

        public string CancelButtonText { get; set; } = M3L.GetString(M3L.string_cancelDownload);

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            AddDownload(pendingLink);
        }

        public void AddDownload(string nxmLink)
        {
            M3Log.Information($@"Queueing nxmlink {nxmLink}");
            var dl = new ModDownload(nxmLink);
            dl.OnInitialized += ModInitialized;
            dl.OnModDownloaded += ModDownloaded;
            dl.OnModDownloadError += DownloadError;

            Downloads.Add(dl);
            dl.Initialize();
        }

        protected override void OnClosing(DataEventArgs dataEventArgs)
        {
            base.OnClosing(dataEventArgs);
            foreach (var md in Downloads)
            {
                md.OnInitialized -= ModInitialized;
                md.OnModDownloaded -= ModDownloaded;
                md.OnModDownloadError -= DownloadError;
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    md.DownloadedStream?.Close();
                }
            }
            Downloads.Clear(); // Ensure we have no references in event this window doesn't clean up for some reason (memory analyzer shows it is not reliable unless another window appears)
        }

        private void ModDownloaded(object sender, DataEventArgs e)
        {
            if (sender is ModDownload md)
            {
                md.OnModDownloaded -= ModDownloaded;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        // Canceled
                        OnClosing(DataEventArgs.Empty);
                    }
                    else
                    {
                        OnClosing(new DataEventArgs(new List<ModDownload>(new[] { md }))); //maybe someday i'll support download queue or something.
                    }
                });
            }
        }

        private void ModInitialized(object sender, EventArgs e)
        {
            if (sender is ModDownload initializedItem)
            {
                M3Log.Information($@"ModDownload has initialized: {initializedItem.ModFile.Name}");
                var nextDownload = Downloads.FirstOrDefault(x => !x.Downloaded);
                nextDownload?.StartDownload(cancellationTokenSource.Token);
            }
        }

        private void DownloadError(object sender, string e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                M3L.ShowDialog(window, e, M3L.GetString(M3L.string_downloadError), MessageBoxButton.OK, MessageBoxImage.Error);
                OnClosing(DataEventArgs.Empty);
            });
        }
    }
}
