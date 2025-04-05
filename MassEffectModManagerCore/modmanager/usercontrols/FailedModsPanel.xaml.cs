using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for FailedModsPanel.xaml
    /// </summary>
    public partial class FailedModsPanel : MMBusyPanelBase
    {
        public FailedModsPanel(List<Mod> FailedMods)
        {
            DataContext = this;
            this.FailedMods.ReplaceAll(FailedMods);
            LoadCommands();
        }

        public Mod SelectedMod { get; set; }

        private bool ShowingModLink;

        public void OnSelectedModChanged(object oldO, object newO)
        {
            var show = SelectedMod != null && SelectedMod.ModWebsite != null;
            if (show != ShowingModLink)
            {
                ClipperHelper.ShowHideVerticalContent(VisitWebsitePanel, show);
                ShowingModLink = show;
            }
        }

        public ICommand RestoreSelectedModCommand { get; set; }
        public ICommand ReloadCommand { get; set; }
        public ICommand DeleteModCommand { get; set; }
        public ICommand VisitWebsiteCommand { get; set; }
        private void LoadCommands()
        {
            RestoreSelectedModCommand = new GenericCommand(CloseToRestoreMod, CanRestoreMod);
            ReloadCommand = new GenericCommand(AttemptModReload, CanReload);
            DeleteModCommand = new GenericCommand(DeleteMod, ModIsSelected);
            VisitWebsiteCommand = new GenericCommand(VisitWebsite, CanVisitWebsite);
        }

        private bool ModIsSelected()
        {
            return SelectedMod != null;
        }

        private void VisitWebsite()
        {
            M3Utilities.OpenWebpage(SelectedMod.ModWebsite);
        }

        private void DeleteMod()
        {
            if (mainwindow.DeleteModFromLibrary(SelectedMod))
            {
                FailedMods.Remove(SelectedMod);
            }
        }

        private bool CanVisitWebsite() => SelectedMod != null && SelectedMod.ModWebsite != Mod.DefaultWebsite;

        private void AttemptModReload()
        {
            var position = FailedMods.IndexOf(SelectedMod);
            var selectedmod = SelectedMod;
            FailedMods.RemoveAt(position);

            Mod m = new Mod(selectedmod.ModDescPath, MEGame.Unknown);
            FailedMods.Insert(position, m);
            SelectedMod = m;

            if (m.ValidMod)
            {
                Result.ReloadMods = true;
            }
        }

        private bool CanRestoreMod()
        {
            return SelectedMod != null && SelectedMod.IsUpdatable;
        }

        private bool CanReload()
        {
            return SelectedMod != null;
        }

        private void CloseToRestoreMod()
        {
            OnClosing(new DataEventArgs(SelectedMod));
        }

        public ObservableCollectionExtended<Mod> FailedMods { get; } = new ObservableCollectionExtended<Mod>();

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            SelectedMod = FailedMods.FirstOrDefault();
        }

        private void RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            M3Utilities.OpenWebpage(e.Uri.AbsoluteUri);
        }

        private void EditModdesc(object sender, RoutedEventArgs e)
        {
            if (SelectedMod != null)
            {
                var result = M3Utilities.ShellOpenFile(SelectedMod.ModDescPath);
                if (result != null)
                {
                    // Issue opening the file.
                    M3L.ShowDialog(window, $"Error opening moddesc.ini file: {result}", M3L.GetString(M3L.string_error), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenModFolder(object sender, RoutedEventArgs e)
        {
            if (SelectedMod != null)
                M3Utilities.OpenExplorer(SelectedMod.ModPath);
        }
    }
}
