using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.starterkit;
using System.Windows;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for Bio2DAGeneratorSelector.xaml
    /// </summary>
    public partial class Bio2DAGeneratorSelector : Window
    {
        public ObservableCollectionExtended<Bio2DAOption> Bio2DAOptions { get; } = new();
        public MEGame Game { get; private set; }

        public Bio2DAGeneratorSelector(MEGame game)
        {
            Game = game;
            InitializeComponent();
            LoadOptions();
        }

        private void LoadOptions()
        {
            var bPath = BackupService.GetGameBackupPath(Game);
            string cookedPath = null;
            if (bPath != null)
            {
                cookedPath = MEDirectories.GetCookedPath(Game, bPath);
                if (!Directory.Exists(cookedPath))
                {
                    BackupNotAvailable();
                    return;
                }
            }
            else
            {
                BackupNotAvailable();
                return;
            }

            string[] searchFiles = { @"Engine.pcc", @"SFXGame.pcc", @"EntryMenu.pcc" };
            foreach (var twoDAF in searchFiles)
            {
                // We only unsafe load to speed up loading on slow backup paths
                using var p = MEPackageHandler.UnsafePartialLoad(Path.Combine(cookedPath, twoDAF), x => !x.IsDefaultObject && x.ClassName is @"Bio2DA" or @"Bio2DANumberedRows");
                foreach (var twoDA in p.Exports.Where(x => x.IsDataLoaded()))
                {
                    Bio2DAOptions.Add(new Bio2DAOption(twoDA.ObjectName, new LEXOpenable(twoDA)));
                }
            }
            Bio2DAOptions.Sort(x => x.Title);
        }

        private void BackupNotAvailable()
        {
            M3L.ShowDialog(this, "There is no backup available to query 2DAs from.", "Backup unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
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
    }
}
