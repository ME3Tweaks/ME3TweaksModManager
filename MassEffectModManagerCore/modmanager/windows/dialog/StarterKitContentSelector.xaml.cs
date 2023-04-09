using System.Windows;
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

        public StarterKitContentSelector(Window owner, Mod selectedMod)
        {
            Owner = owner;
            SelectedMod = selectedMod;
            InitializeComponent();

            var game = selectedMod.Game;
            if (game != MEGame.ME1)
            {
                AvailableFeatures.Add(new StarterKitAddinFeature("Add startup file", AddStartupFile));
            }
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
