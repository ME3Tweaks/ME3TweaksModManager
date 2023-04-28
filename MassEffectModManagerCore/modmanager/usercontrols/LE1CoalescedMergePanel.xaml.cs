﻿using System.Windows.Input;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.GameFilesystem;
using ME3TweaksCore.Config;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Objects;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// In-Window content container for LE1 Coalesced Merge.
    /// </summary>
    public partial class LE1CoalescedMergePanel : MMBusyPanelBase
    {
        private GameTarget CoalescedMergeTarget;

        public LE1CoalescedMergePanel(GameTarget target)
        {
            if (target?.Game != MEGame.LE1)
            {
                throw new Exception(@"Cannot run coalesced merge panel on game that is not LE1");
            }
            this.CoalescedMergeTarget = target;
        }

        public static bool RunCoalescedMerge(GameTarget target)
        {
            M3Log.Information($@"Performing Coaleseced Merge for game: {target.TargetPath}");
            var coalescedStream = M3Utilities.ExtractInternalFileToStream(@"ME3TweaksModManager.modmanager.merge.coalesced.LE1.Coalesced_INT.bin");
            var configBundle = ConfigAssetBundle.FromSingleStream(MEGame.LE1, coalescedStream);

            var dlcMountsInOrder = MELoadedDLC.GetDLCNamesInMountOrder(target.Game, target.TargetPath);
            foreach (var dlc in dlcMountsInOrder)
            {
                var dlcCookedPath = Path.Combine(M3Directories.GetDLCPath(target), dlc, target.Game.CookedDirName());

                M3Log.Information($@"Looking for ConfigDelta-*.m3cd files in {dlcCookedPath}", Settings.LogModInstallation);
                var m3cds = Directory.GetFiles(dlcCookedPath, @"*" + ConfigMerge.CONFIG_MERGE_EXTENSION, SearchOption.TopDirectoryOnly)
                    .Where(x => Path.GetFileName(x).StartsWith(ConfigMerge.CONFIG_MERGE_PREFIX)).ToList(); // Find CoalescedMerge-*.m3cd files
                M3Log.Information($@"Found {m3cds.Count} m3cd files to apply", Settings.LogModInstallation);

                foreach (var m3cd in m3cds)
                {
                    M3Log.Information($@"Merging M3 Config Delta {m3cd}");
                    var m3cdasset = ConfigFileProxy.LoadIni(m3cd);
                    ConfigMerge.PerformMerge(configBundle, m3cdasset);
                }
            }

            // Serialize the assets
            var coalFile = Path.Combine(M3Directories.GetCookedPath(target), @"Coalesced_INT.bin");
            configBundle.CommitAssets(coalFile);

            // Copy the coalesced file to the other languages
            var languages = GameLanguage.GetLanguagesForGame(MEGame.LE1);
            foreach (var lang in languages)
            {
                if (lang.Localization != MELocalization.INT)
                {
                    var dest = Path.Combine(M3Directories.GetCookedPath(target), $@"Coalesced_{lang.FileCode}.bin");
                    if (!File.Exists(dest))
                        continue; // User may have removed this file in backup

                    M3Log.Information($@"Copying coalesced file {coalFile} -> {dest}");
                    File.Copy(coalFile, dest, true); // I'm pretty sure coalesced files only differ by their localized editor messages
                }
            }

            return true;
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"CoalescedMerge");
            nbw.DoWork += (a, b) =>
            {
                RunCoalescedMerge(CoalescedMergeTarget);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }
    }
}