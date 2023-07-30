using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.starterkit;
using System.Windows;
using LegendaryExplorerCore.Helpers;
using Microsoft.WindowsAPICodePack.COMNative.MediaDevices;
using Dark.Net;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for Bio2DAGeneratorSelector.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class Bio2DAGeneratorSelector : Window
    {
        public ObservableCollectionExtended<Bio2DAOption> Bio2DAOptions { get; } = new();
        public MEGame Game { get; private set; }
        public bool IsLoading { get; set; }
        public Bio2DAGeneratorSelector(MEGame game)
        {
            Game = game;
            InitializeComponent();
            this.ApplyDefaultDarkNetWindowStyle();
        }

        private List<Bio2DAOption> LoadOptions()
        {
            var bPath = BackupService.GetGameBackupPath(Game);
            string cookedPath = null;
            if (bPath != null)
            {
                cookedPath = MEDirectories.GetCookedPath(Game, bPath);
                if (!Directory.Exists(cookedPath))
                {
                    BackupNotAvailable();
                    return null;
                }
            }
            else
            {
                BackupNotAvailable();
                return null;
            }

            var twoDAs = new List<Bio2DAOption>();

            string[] searchFiles = { @"Engine.pcc", @"SFXGame.pcc", @"EntryMenu.pcc" };
            foreach (var twoDAF in searchFiles)
            {
                // We only unsafe load to speed up loading on slow backup paths
                using var p = MEPackageHandler.UnsafePartialLoad(Path.Combine(cookedPath, twoDAF), x => !x.IsDefaultObject && !x.IsDefaultObject && x.ObjectName.Name != @"Default2DA" && x.ClassName is @"Bio2DA" or @"Bio2DANumberedRows");
                foreach (var twoDA in p.Exports.Where(x => x.IsDataLoaded()))
                {
                    twoDAs.Add(new Bio2DAOption(twoDA.ObjectName, new LEXOpenable(twoDA)));
                }
            }
            return twoDAs;
        }

        private void BackupNotAvailable()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                M3L.ShowDialog(this, M3L.GetString(M3L.string_thereIsNoBackupAvailableToQuery2DAsFrom), M3L.GetString(M3L.string_backupUnavailable), MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            });
        }

        public void SetSelectedOptions(List<Bio2DAOption> optionsToSelect)
        {
            foreach (var v in Bio2DAOptions)
            {
                v.IsSelected = optionsToSelect.Any(x => x.Title == v.Title);
            }
        }


        public IEnumerable<Bio2DAOption> GetSelected2DAs()
        {
            return Bio2DAOptions.Where(x => x.IsSelected).ToList();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Bio2DAGeneratorSelector_OnContentRendered(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                IsLoading = true;
                return LoadOptions();
            }).ContinueWithOnUIThread(x =>
            {
                IsLoading = false;
                if (x.Exception == null)
                {
                    if (x.Result != null)
                    {
                        // Result is null if there was an error
                        Bio2DAOptions.AddRange(x.Result);
                        Bio2DAOptions.Sort(x => x.Title);
                    }
                }
                else
                {
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_2da_errorReading2DAs, x.Exception.Message), M3L.GetString(M3L.string_errorReadingTables), MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }
            });
        }
    }
}
