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
using System.Windows.Shapes;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.objects.launcher;
using Microsoft.WindowsAPICodePack.PortableDevices.EventSystem;
using Pathoschild.FluentNexus.Models;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for LaunchOptionSelectorDialog.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class LaunchOptionSelectorDialog : Window
    {
        public MEGame Game { get; set; }
        public LaunchOptionsPackage ChosenOption { get; set; }

        public ObservableCollectionExtended<LaunchOptionsPackage> AvailableLaunchOptionsPackages { get; } = new();

        public LaunchOptionSelectorDialog(Window owner, MEGame game)
        {
            Owner = owner;
            Game = game;
            PopulatePackages();
            LoadCommands();
            InitializeComponent();
        }

        private void PopulatePackages()
        {
            // Reload the launch options.
            M3LoadedMods.Instance.LoadLaunchOptions();

            // Set our list
            AvailableLaunchOptionsPackages.ClearEx();
            AvailableLaunchOptionsPackages.Add(M3LoadedMods.GetDefaultLaunchOptionsPackage(Game));
            AvailableLaunchOptionsPackages.AddRange(M3LoadedMods.Instance.AllLaunchOptions.Where(x => x.Game == Game));
        }

        private void LoadCommands()
        {
            SelectLaunchOptionCommand = new GenericCommand(SaveAndClose);
            EditSelectedLaunchOptionCommand = new GenericCommand(EditOption, () => ChosenOption != null && !ChosenOption.IsCustomOption);
            CreateNewLaunchOptionCommand = new GenericCommand(CreateNewLaunchOption);
        }

        private void EditOption()
        {
            LaunchParametersDialog lpd = new LaunchParametersDialog(this, Game, ChosenOption);
            lpd.ShowDialog();

            // Reload packages to locate the item by Guid
            PopulatePackages();
            if (lpd.LaunchPackage != null)
            {
                ChosenOption = AvailableLaunchOptionsPackages.FirstOrDefault(x => x.PackageGuid == lpd.LaunchPackage.PackageGuid);
            }
            else
            {
                ChosenOption = AvailableLaunchOptionsPackages.FirstOrDefault(); // This will be 'Start Game', which is always available.
            }
        }

        private void CreateNewLaunchOption()
        {
            LaunchParametersDialog lpd = new LaunchParametersDialog(this, Game, null);
            lpd.ShowDialog();

            var option = lpd.LaunchPackage;
            PopulatePackages();
            if (option != null)
            {
                App.SubmitAnalyticTelemetryEvent("Created launch option", new Dictionary<string, string>()
                {
                    {@"Option name", ChosenOption?.PackageTitle},
                    {@"Option lang", ChosenOption?.ChosenLanguage},
                    {@"Subtitle size", ChosenOption?.SubtitleSize.ToString()},
                    {@"Autoresume", ChosenOption?.AutoResumeSave.ToString()},
                    {@"Disable Force Feedback", ChosenOption?.NoForceFeedback.ToString()},
                    {@"Custom args", ChosenOption?.CustomExtraArgs}
                });
                ChosenOption = AvailableLaunchOptionsPackages.FirstOrDefault(x => x.PackageGuid == option.PackageGuid);
            }
            else
            {
                ChosenOption = AvailableLaunchOptionsPackages.FirstOrDefault();
            }
        }

        public ICommand EditSelectedLaunchOptionCommand { get; set; }

        public ICommand CreateNewLaunchOptionCommand { get; set; }

        public ICommand SelectLaunchOptionCommand { get; set; }

        private void SaveAndClose()
        {
            if (ChosenOption != null)
            {
                switch (Game)
                {
                    case MEGame.LE1:
                        Settings.SelectedLE1LaunchOption = ChosenOption.PackageGuid;
                        break;
                    case MEGame.LE2:
                        Settings.SelectedLE2LaunchOption = ChosenOption.PackageGuid;
                        break;
                    case MEGame.LE3:
                        Settings.SelectedLE3LaunchOption = ChosenOption.PackageGuid;
                        break;
                }
            }

            Close();
        }
    }
}
