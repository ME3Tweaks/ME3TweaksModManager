using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod;
using Microsoft.AppCenter.Crashes;
using Microsoft.WindowsAPICodePack.Dialogs;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.loaders
{
    /// <summary>
    /// Class that holds the LoadedMods list and handles loading mod objects into this list, as well as filtering the list via a collection view.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class M3LoadedMods
    {
        /// <summary>
        /// Gets the singleton instance of the LoadedMods object.
        /// </summary>
        public static M3LoadedMods Instance { get; private set; }

        /// <summary>
        /// List of game filters that are applied to the VisibleFilteredMods collection view.
        /// </summary>
        public ObservableCollectionExtended<GameFilter> GameFilters { get; } = new();

        /// <summary>
        /// Callback to indicate a mod should be selected in the mod list
        /// </summary>
        public Action<Mod> SelectModCallback { get; set; }

        /// <summary>
        /// If mods are currently loading
        /// </summary>
        public bool IsLoadingMods { get; private set; }

        /// <summary>
        /// If the mods list has been loaded. Mod loading does not occur immediately on application boot.
        /// </summary>
        public bool ModsLoaded { get; private set; }

        /// <summary>
        /// Mods currently visible according to the GAmeFilters list
        /// </summary>
        public ObservableCollectionExtended<Mod> VisibleFilteredMods { get; } = new ObservableCollectionExtended<Mod>();

        /// <summary>
        /// All mods that successfully loaded.
        /// </summary>
        public ObservableCollectionExtended<Mod> AllLoadedMods { get; } = new ObservableCollectionExtended<Mod>();
        /// <summary>
        /// All mods that failed to load
        /// </summary>
        public ObservableCollectionExtended<Mod> FailedMods { get; } = new ObservableCollectionExtended<Mod>();

        /// <summary>
        /// Suppresses the logic of FilterMods(), used to prevent multiple invocations on global changes
        /// </summary>
        public bool SuppressFilterMods { get; set; }

        /// <summary>
        /// If the visiblility controls have nothing enabled
        /// </summary>
        public bool AllGamesHidden { get; set; }

        /// <summary>
        /// Private constructor to force accessor via ModLoader.
        /// </summary>
        private M3LoadedMods() { }

        /// <summary>
        /// Reference to the main window, which is used to center dialogs.
        /// </summary>
        private Window window { get; init; }


        public static void InitializeModLoader(MainWindow window, Action<Mod> selectedModCallback)
        {
            Instance = new M3LoadedMods() { window = window, SelectModCallback = selectedModCallback};
            Settings.StaticPropertyChanged += Instance.SettingChanged;
        }

        /// <summary>
        /// Called when a setting is changed so we can turn on and off game filters.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.GenerationSettingOT))
            {
                SuppressFilterMods = true;
                foreach (var gf in GameFilters.Where(x => !x.Game.IsEnabledGeneration()))
                {
                    gf.IsEnabled = false;
                }

                SuppressFilterMods = false;
                FilterMods();
            }
            else if (e.PropertyName == nameof(Settings.GenerationSettingLE))
            {
                SuppressFilterMods = true;
                foreach (var gf in GameFilters.Where(x => !x.Game.IsEnabledGeneration()))
                {
                    gf.IsEnabled = false;
                }
                SuppressFilterMods = false;
                FilterMods();
            }
        }

        /// <summary>
        /// Initiates a complete reload of the mod list.
        /// </summary>
        /// <param name="modToHighlight">Mod to automatically reselect when mod loading has completed</param>
        /// <param name="forceUpdateCheckOnCompletion">If an update check should be forced when mod loading has completed</param>
        /// <param name="scopedModsToCheckForUpdates">If an update check is forced, this list scopes which mods will be checked</param>
        public void LoadMods(Mod modToHighlight = null, bool forceUpdateCheckOnCompletion = false, List<Mod> scopedModsToCheckForUpdates = null)
        {
            LoadMods(modToHighlight?.ModPath, forceUpdateCheckOnCompletion, scopedModsToCheckForUpdates);
        }

        /// <summary>
        /// Reload mods. Highlight the specified mod that matches the path if any
        /// </summary>
        /// <param name="modpathToHighlight"></param>
        public void LoadMods(string modpathToHighlight, bool forceUpdateCheckOnCompletion = false, List<Mod> scopedModsToCheckForUpdates = null)
        {
            if (IsLoadingMods)
                return; // Do not accept another load in the middle of load

            try
            {
                M3Utilities.EnsureModDirectories();
            }
            catch (Exception e)
            {
                M3Log.Error(@"Unable to ensure mod directories: " + e.Message);
                Crashes.TrackError(e);
                M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialogUnableToCreateModLibraryNoPermissions, e.Message), M3L.GetString(M3L.string_errorCreatingModLibrary), MessageBoxButton.OK, MessageBoxImage.Error);
                var folderPicked = ChooseModLibraryPath(window, false);
                if (folderPicked)
                {
                    LoadMods();
                }
                else
                {
                    M3Log.Error(@"Unable to create mod library. Mod Manager will now exit.");
                    Crashes.TrackError(new Exception(@"Unable to create mod library"), new Dictionary<string, string>() { { @"Executable location", App.ExecutableLocation } });
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_unableToCreateModLibrary), M3L.GetString(M3L.string_errorCreatingModLibrary), MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1);
                }

                return;
            }

            VisibleFilteredMods.ClearEx();
            AllLoadedMods.ClearEx();
            FailedMods.ClearEx();
            IsLoadingMods = true;

            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ModLoaderThread");
            bw.WorkerReportsProgress = true;
            bw.DoWork += (a, args) =>
            {
                bool canAutoCheckForModUpdates = MOnlineContent.CanFetchContentThrottleCheck(); //This is here as it will fire before other threads can set this value used in this session.
                ModsLoaded = false;
                var uiTask = BackgroundTaskEngine.SubmitBackgroundJob(@"ModLoader", M3L.GetString(M3L.string_loadingMods), M3L.GetString(M3L.string_loadedMods));
                M3Log.Information(@"Loading mods from mod library: " + M3Utilities.GetModsDirectory());

                var le3modDescsToLoad = Directory.GetDirectories(M3Utilities.GetLE3ModsDirectory()).Select(x => (game: MEGame.LE3, path: Path.Combine(x, @"moddesc.ini"))).Where(x => File.Exists(x.path));
                var le2modDescsToLoad = Directory.GetDirectories(M3Utilities.GetLE2ModsDirectory()).Select(x => (game: MEGame.LE2, path: Path.Combine(x, @"moddesc.ini"))).Where(x => File.Exists(x.path));
                var le1modDescsToLoad = Directory.GetDirectories(M3Utilities.GetLE1ModsDirectory()).Select(x => (game: MEGame.LE1, path: Path.Combine(x, @"moddesc.ini"))).Where(x => File.Exists(x.path));
                var me3modDescsToLoad = Directory.GetDirectories(M3Utilities.GetME3ModsDirectory()).Select(x => (game: MEGame.ME3, path: Path.Combine(x, @"moddesc.ini"))).Where(x => File.Exists(x.path));
                var me2modDescsToLoad = Directory.GetDirectories(M3Utilities.GetME2ModsDirectory()).Select(x => (game: MEGame.ME2, path: Path.Combine(x, @"moddesc.ini"))).Where(x => File.Exists(x.path));
                var me1modDescsToLoad = Directory.GetDirectories(M3Utilities.GetME1ModsDirectory()).Select(x => (game: MEGame.ME1, path: Path.Combine(x, @"moddesc.ini"))).Where(x => File.Exists(x.path));

                // LE Launcher
                var leLaunchermodDescsToLoad = Directory.GetDirectories(M3Utilities.GetLELauncherModsDirectory()).Select(x => (game: MEGame.LELauncher, path: Path.Combine(x, @"moddesc.ini"))).Where(x => File.Exists(x.path));
                //var modDescsToLoad = leLaunchermodDescsToLoad.ToList();

                List<(MEGame game, string path)> modDescsToLoad = new();
                if (Settings.GenerationSettingOT)
                {
                    modDescsToLoad.AddRange(me1modDescsToLoad);
                    modDescsToLoad.AddRange(me2modDescsToLoad);
                    modDescsToLoad.AddRange(me3modDescsToLoad);
                }

                if (Settings.GenerationSettingLE)
                {
                    modDescsToLoad.AddRange(le1modDescsToLoad);
                    modDescsToLoad.AddRange(le2modDescsToLoad);
                    modDescsToLoad.AddRange(le3modDescsToLoad);
                    modDescsToLoad.AddRange(leLaunchermodDescsToLoad);
                }

                foreach (var moddesc in modDescsToLoad)
                {
                    var mod = new Mod(moddesc.path, moddesc.game);
                    if (mod.ValidMod)
                    {
                        AllLoadedMods.Add(mod);
                        if (GameFilters.FirstOrDefaultOut(x => x.Game == mod.Game, out var gf) && gf.IsEnabled)
                        {
                            VisibleFilteredMods.Add(mod);
                        }
                    }
                    else
                    {
                        FailedMods.Add(mod);
                    }
                }

                Application.Current.Dispatcher.Invoke(delegate { VisibleFilteredMods.Sort(x => x.ModName); });

                if (modpathToHighlight != null)
                {
                    args.Result = AllLoadedMods.FirstOrDefault(x => x.ModPath == modpathToHighlight);

                    //telemetry for importing issues
                    var targetMod = AllLoadedMods.FirstOrDefault(x => x.ModPath == modpathToHighlight);
                    if (File.Exists(modpathToHighlight) && targetMod == null)
                    {
                        //moddesc.ini exists but it did not load
                        M3Log.Error(@"Mod to highlight failed to load! Path: " + modpathToHighlight);
                        Crashes.TrackError(new Exception(@"Mod set to highlight but not in list of loaded mods"), new Dictionary<string, string>()
                        {
                            { @"Moddesc path", modpathToHighlight }
                        });
                    }
                }

                BackgroundTaskEngine.SubmitJobCompletion(uiTask);
                //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoModSelectedText)));

                ModsLoaded = true;
                if (canAutoCheckForModUpdates)
                {
                    ModUpdater.Instance.CheckAllModsForUpdates();
                }
                else if (forceUpdateCheckOnCompletion && args.Result is Mod highlightedMod && highlightedMod.IsUpdatable)
                {
                    ModUpdater.Instance.CheckModsForUpdates(new List<Mod>(new[] { highlightedMod }));
                }
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                ModsLoaded = true;
                if (b.Error != null)
                {
                    M3Log.Error(@"Exception occurred in ModLoader thread: " + b.Error.Message);
                }
                IsLoadingMods = false;
                if (b.Result is Mod m)
                {
                    SelectModCallback?.Invoke(m);
                }
            };
            bw.RunWorkerAsync();
        }

        public static bool ChooseModLibraryPath(Window centeringWindow, bool loadModsAfterSelecting)
        {
            CommonOpenFileDialog m = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = M3L.GetString(M3L.string_selectModLibraryFolder)
            };
            if (m.ShowDialog(centeringWindow) == CommonFileDialogResult.Ok)
            {
                Settings.ModLibraryPath = m.FileName;
                if (loadModsAfterSelecting)
                {
                    Instance.LoadMods();
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Removes a loaded mod from all lists
        /// </summary>
        /// <param name="selectedMod"></param>
        public void RemoveMod(Mod selectedMod)
        {
            AllLoadedMods.Remove(selectedMod);
            VisibleFilteredMods.Remove(selectedMod);
            FailedMods.Remove(selectedMod); //make sure to remove it from this in case it's failed mods panel calling this.
        }

        /// <summary>
        /// Updates the collection view of mods.
        /// </summary>
        public void FilterMods()
        {
            if (SuppressFilterMods)
                return;
            var allMods = M3LoadedMods.Instance.AllLoadedMods.ToList(); // Makes a clone of the list
            bool oneVisible = false;
            foreach (var gf in M3LoadedMods.Instance.GameFilters)
            {
                if (!gf.IsEnabled)
                {
                    allMods.RemoveAll(x => x.Game == gf.Game);
                }
                else
                {
                    oneVisible = true;
                }
            }

            AllGamesHidden = !oneVisible;
            VisibleFilteredMods.ReplaceAll(allMods);
            VisibleFilteredMods.Sort(x => x.ModName);
        }
    }
}
