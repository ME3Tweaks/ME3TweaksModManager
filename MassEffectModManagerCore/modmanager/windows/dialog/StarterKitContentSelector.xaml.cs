using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using BCnEncoder.Shared;
using IniParser;
using IniParser.Model;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.starterkit;
using ME3TweaksModManager.modmanager.starterkit;
using ME3TweaksModManager.modmanager.usercontrols.moddescinieditor;
using TaskExtensions = LegendaryExplorerCore.Helpers.TaskExtensions;

namespace ME3TweaksModManager.modmanager.windows.dialog
{
    /// <summary>
    /// Interaction logic for StarterKitContentSelector.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class StarterKitContentSelector : Window
    {
        /// <summary>
        /// The mod we are operating on
        /// </summary>
        public Mod SelectedMod { get; set; }

        public ObservableCollectionExtended<StarterKitAddinFeature> AvailableFeatures { get; } = new();

        /// <summary>
        /// If the selected mod should be reloaded when the window closes
        /// </summary>
        public bool ReloadMod { get; private set; }

        /// <summary>
        /// Bottom left text to display
        /// </summary>
        public string OperationText { get; set; } = "Select an operation";

        public StarterKitContentSelector(Window owner, Mod selectedMod)
        {
            Owner = owner;
            SelectedMod = selectedMod;
            InitializeComponent();


            AvailableFeatures.Add(new StarterKitAddinFeature(M3L.GetString(M3L.string_addStartupFile), AddStartupFile, validGames: new[] { MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3 }));
            AvailableFeatures.Add(new StarterKitAddinFeature(M3L.GetString(M3L.string_addPlotManagerData), AddPlotManagerData, validGames: new[] { MEGame.ME1, MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3 }));

            string[] game3Hench = new[] { @"Ashley", @"EDI", @"Garrus", @"Kaidan", @"Marine", @"Prothean", @"Liara", @"Tali" };
            foreach (var hench in game3Hench)
            {
                AvailableFeatures.Add(new StarterKitAddinFeature(M3L.GetString(M3L.string_interp_addSquadmateOutfitMergeX, GetUIHenchName(hench)), () => AddSquadmateMergeOutfit(hench), validGames: new[] { MEGame.ME3, MEGame.LE3 }));
            }

            AvailableFeatures.Add(new StarterKitAddinFeature(M3L.GetString(M3L.string_interp_addModSettingsMenuStub), AddModSettingsStub, validGames: new[] { /*MEGame.LE1,*/ MEGame.LE3 }));
        }

        private void AddModSettingsStub()
        {
            var dlcFolderPath = GetDLCFolderPath();
            if (dlcFolderPath == null) return; // Abort

            OperationText = "Adding mod settings menu data...";
            Task.Run(() =>
            {
                OperationInProgress = true;
                List<Action<IniData>> moddescAddinDelegates = new List<Action<IniData>>();
                StarterKitAddins.AddModSettingsMenu(SelectedMod, SelectedMod.Game,
                    Path.Combine(SelectedMod.ModPath, dlcFolderPath), moddescAddinDelegates);

                if (moddescAddinDelegates.Any())
                {
                    var iniParser = new FileIniDataParser();
                    var iniData = iniParser.ReadFile(SelectedMod.ModDescPath);
                    foreach (var del in moddescAddinDelegates)
                    {
                        del(iniData);
                    }

                    File.WriteAllText(SelectedMod.ModDescPath, iniData.ToString());
                    ReloadMod = true;
                }
            }).ContinueWithOnUIThread(x =>
            {
                OperationInProgress = false;

                if (x.Exception == null)
                {
                    OperationText = "Added mod settings menu data";
                }
                else
                {
                    OperationText = $"Failed to add mod settings menu data: {x.Exception.Message}";
                }
            });


        }

        /// <summary>
        /// If an operation is currently in progress
        /// </summary>
        public bool OperationInProgress { get; set; }

        private void AddSquadmateMergeOutfit(string hench)
        {
            // Test backup

            if (!BackupService.GetBackupStatus(SelectedMod.Game).BackedUp)
            {
                M3L.ShowDialog(this,
                    "Adding squadmate merge files to a mod requires a backup of the game to pull files from.",
                    M3L.GetString(M3L.string_noBackupAvailable), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dlcFolderPath = GetDLCFolderPath();
            if (dlcFolderPath == null) return; // Abort

            OperationText = $"Adding squadmate outfit merge files for {hench}...";
            Task.Run(() =>
            {
                OperationInProgress = true;
                StarterKitAddins.GenerateSquadmateMergeFiles(SelectedMod.Game, hench, dlcFolderPath,
                    new List<Dictionary<string, object>>());
            }).ContinueWithOnUIThread(x =>
            {
                OperationInProgress = false;
                if (x.Exception == null)
                {
                    OperationText = $"Added squadmate outfit merge files for {hench}";
                }
                else
                {
                    OperationText = $"Failed to add squadmate outfit merge files for {hench}: {x.Exception.Message}";
                }
            });
        }

        private void AddPlotManagerData()
        {
            var dlcFolderPath = GetDLCFolderPath();
            if (dlcFolderPath == null) return; // Abort

            OperationText = $"Adding plot manager data...";
            Task.Run(() =>
            {
                OperationInProgress = true;
                StarterKitAddins.GeneratePlotData(SelectedMod.Game, Path.Combine(SelectedMod.ModPath, dlcFolderPath));
            }).ContinueWithOnUIThread(x =>
            {
                OperationInProgress = false;
                if (x.Exception == null)
                {
                    OperationText = $"Added plot manager data";
                }
                else
                {
                    OperationText = $"Failed to add plot manager data: {x.Exception.Message}";
                }
            });
        }

        private string GetUIHenchName(string hench)
        {
            if (hench == @"Prothean") return @"Javik"; // Not sure these need localized
            if (hench == @"Marine") return @"James";
            return hench;
        }

        private void AddStartupFile()
        {
            var dlcFolderPath = GetDLCFolderPath();
            if (dlcFolderPath == null) return; // Abort
            OperationText = $"Adding startup file...";
            Task.Run(() =>
            {
                OperationInProgress = true;
                StarterKitAddins.AddStartupFile(SelectedMod.Game, Path.Combine(SelectedMod.ModPath, dlcFolderPath));
            }).ContinueWithOnUIThread(x =>
            {
                OperationInProgress = false;
                if (x.Exception == null)
                {
                    OperationText = $"Added startup file";
                }
                else
                {
                    OperationText = $"Failed to add startup file: {x.Exception.Message}";
                }
            });
        }

        private string GetDLCFolderPath()
        {
            var dlcJob = SelectedMod.GetJob(ModJob.JobHeader.CUSTOMDLC);
            if (dlcJob == null) return null; // Not found

            var sourceDirs = dlcJob.CustomDLCFolderMapping;

            if (sourceDirs.Count > 1)
            {
                // We have to select
                var response = DropdownSelectorDialog.GetSelection<string>(this, M3L.GetString(M3L.string_selectDLCMod), dlcJob.CustomDLCFolderMapping.Keys.ToList(), M3L.GetString(M3L.string_selectADLCFolderToAddAStartupFileTo), @"");
                if (response is string str)
                {
                    return str;
                }

                return null;
            }

            return sourceDirs.Keys.FirstOrDefault();
        }
    }
}
