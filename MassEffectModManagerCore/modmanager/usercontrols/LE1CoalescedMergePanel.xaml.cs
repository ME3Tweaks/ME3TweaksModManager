using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Config;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Localization;
using ME3TweaksCore.Objects;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksModManager.me3tweakscoreextended;
using ME3TweaksModManager.modmanager.localizations;
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

            // For BGFIS
            bool mergedAny = false;
            string recordedMergeName = @"";
            void recordMerge(string displayName)
            {
                mergedAny = true;
                recordedMergeName += displayName + "\n"; // do not localize
            }


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
                    recordMerge($@"{dlc}-{Path.GetFileName(m3cd)}");
                }
            }

            var consolem3Cd = MakeConsoleM3CD();
            if (consolem3Cd != null)
            {
                M3Log.Information(@"Merging M3 Config Delta for user chosen keybinds");
                ConfigMerge.PerformMerge(configBundle, consolem3Cd);
                recordMerge(M3L.GetString(M3L.string_m3ConsoleKeybinds)); // we do want this localized
            }

            var records = new List<BasegameFileRecord>();
            var coalFile = Path.Combine(M3Directories.GetCookedPath(target), @"Coalesced_INT.bin");
            // Set the BGFIS record name
            if (mergedAny)
            {
                // Serialize the assets
                configBundle.CommitAssets(coalFile);

                // Submit to BGFIS
                records.Add(new M3BasegameFileRecord(coalFile, target, recordedMergeName.Trim()));
                BasegameFileIdentificationService.AddLocalBasegameIdentificationEntries(records);
            }
            else
            {
                coalescedStream.WriteToFile(coalFile);
            }


            // LE1 - It doesn't actually use non-INT Coalesced files, that's only for LE2...
            /*
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
                    if (mergedAny)
                    {
                        records.Add(new M3BasegameFileRecord(dest, target, recordedMergeName));
                    }
                }
            }

            if (mergedAny)
            {
                BasegameFileIdentificationService.AddLocalBasegameIdentificationEntries(records);
            }*/
            return true;
        }

        /// <summary>
        /// Generates a dynamic M3CD based on user's LE1 console keybinds they last chose
        /// </summary>
        /// <returns></returns>
        private static CoalesceAsset MakeConsoleM3CD()
        {
            if (!Settings.IsLE1ConsoleKeySet && !Settings.IsLE1MiniConsoleKeySet)
                return null;

            DuplicatingIni m3cdIni = new DuplicatingIni();
            var bioInput = m3cdIni.GetOrAddSection(@"BIOInput.ini Engine.Console");
            if (Settings.IsLE1ConsoleKeySet)
            {
                bioInput.SetSingleEntry(@">ConsoleKey", Settings.LE1ConsoleKey);
            }
            if (Settings.IsLE1MiniConsoleKeySet)
            {
                bioInput.SetSingleEntry(@">TypeKey", Settings.LE1MiniConsoleKey);
            }

            return ConfigFileProxy.ParseIni(m3cdIni.ToString());
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            if (Settings.OneTimeMessage_LE1CoalescedOverwriteWarning)
            {
                M3Log.Information(@"Showing first le1 coalesced merge dialog");
                var result = M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_firstCoalescedMerge), M3L.GetString(M3L.string_information), MessageBoxButton.OKCancel, MessageBoxImage.Information);

                if (result == MessageBoxResult.OK)
                {
                    M3Log.Information(@"User accepted LE1 coalesced merge feature");
                    Settings.OneTimeMessage_LE1CoalescedOverwriteWarning = false;
                }
                else
                {
                    M3Log.Warning(@"User declined first LE1 coalesced merge");
                    OnClosing(DataEventArgs.Empty);
                    return;
                }
            }

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
