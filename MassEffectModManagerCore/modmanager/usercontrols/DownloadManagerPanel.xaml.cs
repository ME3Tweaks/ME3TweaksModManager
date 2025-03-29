using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for DownloadManagerPanel.xaml - Formerly NexusModsDownloadPanel
    /// </summary>
    public partial class DownloadManagerPanel : MMBusyPanelBase
    {
        public ICommand CloseCommand { get; set; }

        public string DownloadLocationText { get; set; }

        public ObservableCollectionExtended<ModDownload> Downloads { get; } = new ObservableCollectionExtended<ModDownload>();
        public DownloadManagerPanel()
        {
            LoadCommands();
            AttachDownloadManagerListeners();

            // Setup location text.
            DownloadLocationText = Settings.ModDownloadCacheFolder == null
                ? "Downloads will automatically be deleted when Mod Manager closes (mods imported into the library will be unaffected). You can change this in the application settings."
                : $"Downloaded files are saved to the following location, which can be changed in the settings:\n{Settings.ModDownloadCacheFolder}";

        }

        private void AttachDownloadManagerListeners()
        {
            DownloadManager.OnDownloadAdded += OnDownloadAdded;
            DownloadManager.OnDownloadRemoved += OnDownloadRemoved;
            DownloadManager.OnDownloadCompleted += OnModDownloaded;
        }

        private void DetachDownloadManagerListeners()
        {
            DownloadManager.OnDownloadAdded -= OnDownloadAdded;
            DownloadManager.OnDownloadRemoved -= OnDownloadRemoved;
            DownloadManager.OnDownloadCompleted -= OnModDownloaded;
        }


        private void OnDownloadRemoved(object sender, EventArgs e)
        {
            ReloadDownloads();
        }

        private void ReloadDownloads()
        {
            Downloads.ReplaceAll(DownloadManager.GetDownloads().Values);
            TriggerResizeNextFrame();
        }

        private void OnDownloadAdded(object sender, EventArgs e)
        {
            ReloadDownloads();
        }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(CloseWrapper);
        }

        private void CloseWrapper()
        {
            OnClosing(DataEventArgs.Empty);
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            ReloadDownloads();
        }

        /// <summary>
        /// When panel closes we need to scrub all references to downloads as they will continue without us.
        /// </summary>
        /// <param name="dataEventArgs"></param>
        protected override void OnClosing(DataEventArgs dataEventArgs)
        {
            DetachDownloadManagerListeners();

            foreach (var md in Downloads)
            {
                md.OnModDownloaded -= OnModDownloaded;
                md.OnModDownloadError -= DownloadError;
            }

            // Clear out canceled downloads from the manager.
            DownloadManager.ClearAbortedDownloads();

            Downloads.Clear(); // Ensure we have no references in event this window doesn't clean up for some reason (memory analyzer shows it is not reliable unless another window appears)
            base.OnClosing(dataEventArgs);
        }

        /// <summary>
        /// Invoked when a download completes and we are visible
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnModDownloaded(object sender, EventArgs e)
        {
            if (sender is ModDownload md)
            {
                // Remove handler so this call can't happen again
                md.OnModDownloaded -= OnModDownloaded;

                if (md.DownloadState == EModDownloadState.DOWNLOADCOMPLETE)
                {
                    if (md.AutoImport)
                    {
                        // Auto import code is going to be invoked, we should not operate on this.
                        return;
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OnClosing(new DataEventArgs(new List<ModDownload>(new[] { md })));
                    });
                }
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
