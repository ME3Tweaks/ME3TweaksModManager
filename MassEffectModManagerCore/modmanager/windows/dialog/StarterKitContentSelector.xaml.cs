using System.Diagnostics;
using System.Windows;
using IniParser;
using IniParser.Model;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.starterkit;
using ME3TweaksModManager.modmanager.starterkit;
using ME3TweaksModManager.modmanager.usercontrols.moddescinieditor;

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
        public StarterKitContentSelector(Window owner, Mod selectedMod)
        {
            Owner = owner;
            SelectedMod = selectedMod;
            InitializeComponent();


            AvailableFeatures.Add(new StarterKitAddinFeature("Add startup file", AddStartupFile, validGames: new[] { MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3 }));
            AvailableFeatures.Add(new StarterKitAddinFeature("Add PlotManager data", AddPlotManagerData, validGames: new[] { MEGame.ME1, MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3 }));

            string[] game3Hench = new[] { @"Ashley", @"EDI", @"Garrus", @"Kaidan", @"Marine", @"Prothean", @"Liara", @"Tali" };
            foreach (var hench in game3Hench)
            {
                AvailableFeatures.Add(new StarterKitAddinFeature($"Add Squadmate Outfit Merge: {GetUIHenchName(hench)}", () => AddSquadmateMergeOutfit(hench), validGames: new[] { MEGame.ME3, MEGame.LE3 }));
            }

            AvailableFeatures.Add(new StarterKitAddinFeature($"Add mod settings menu stub", AddModSettingsStub, validGames: new[] { /*MEGame.LE1,*/ MEGame.LE3 }));
        }

        private void AddModSettingsStub()
        {
            var dlcFolderPath = GetDLCFolderPath();
            if (dlcFolderPath == null) return; // Abort

            List<Action<IniData>> moddescAddinDelegates = new List<Action<IniData>>();
            StarterKitAddins.AddModSettingsMenu(SelectedMod, SelectedMod.Game, Path.Combine(SelectedMod.ModPath, dlcFolderPath), moddescAddinDelegates);

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
        }

        private void AddSquadmateMergeOutfit(string hench)
        {
        }

        private void AddPlotManagerData()
        {
            var dlcFolderPath = GetDLCFolderPath();
            if (dlcFolderPath == null) return; // Abort
            StarterKitAddins.GeneratePlotData(SelectedMod.Game, Path.Combine(SelectedMod.ModPath, dlcFolderPath));
        }

        private string GetUIHenchName(string hench)
        {
            if (hench == @"Prothean") return "Javik";
            if (hench == @"Marine") return "James";
            return hench;
        }

        private void AddStartupFile()
        {
            var dlcFolderPath = GetDLCFolderPath();
            if (dlcFolderPath == null) return; // Abort
            StarterKitAddins.AddStartupFile(SelectedMod.Game, Path.Combine(SelectedMod.ModPath, dlcFolderPath));
        }

        private string GetDLCFolderPath()
        {
            var dlcJob = SelectedMod.GetJob(ModJob.JobHeader.CUSTOMDLC);
            if (dlcJob == null) return null; // Not found

            var sourceDirs = dlcJob.CustomDLCFolderMapping;

            if (sourceDirs.Count > 1)
            {
                // We have to select
                var response = DropdownSelectorDialog.GetSelection<string>(this, "Select DLC mod", dlcJob.CustomDLCFolderMapping.Keys.ToList(), "Select a DLC folder to add a startup file to.", "I don't know what this is for.");
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
