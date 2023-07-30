using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Dark.Net;
using IniParser;
using IniParser.Model;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for ME3CMMMigrationWindow.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class ME3CMMMigrationWindow : Window
    {
        public ObservableCollectionExtended<BasicUITask> Tasks { get; } = new ObservableCollectionExtended<BasicUITask>();
        BasicUITask MigratingModsTask = new BasicUITask(M3L.GetString(M3L.string_migratingMods));
        BasicUITask MigratingSettings = new BasicUITask(M3L.GetString(M3L.string_migratingSettings));
        BasicUITask CleaningUpTask = new BasicUITask(M3L.GetString(M3L.string_cleaningUp));
        public ME3CMMMigrationWindow()
        {
            InitializeComponent();
            this.ApplyDefaultDarkNetWindowStyle();
            Tasks.Add(MigratingModsTask);
            Tasks.Add(MigratingSettings);
            Tasks.Add(CleaningUpTask);
        }

        private void Migration_ContentRendered(object sender, EventArgs e)
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ME3CMMMigration");
            nbw.DoWork += (a, b) =>
            {
                bool cleanup = false;
                bool migrated = true;
                M3Log.Information(@">>>> ME3CMMMigration Thread");
                M3Log.Information(@"Validate ME3CMM folders and files");
                var exeDir = M3Utilities.GetMMExecutableDirectory();


                var modsDir = Path.Combine(exeDir, @"mods");
                var dataDir = Path.Combine(exeDir, @"data");
                try
                {
                    if (Directory.Exists(modsDir) && Directory.Exists(dataDir))
                    {
                        M3Log.Information(@"mods and data dir exist.");
                        // 1. MIGRATE MODS
                        M3Log.Information(@"Step 1: Migrate mods");
                        MigratingModsTask.SetInProgress();

                        var targetModLibrary = M3LoadedMods.GetCurrentModLibraryDirectory();

                        targetModLibrary = Path.Combine(targetModLibrary, @"ME3");
                        if (!Directory.Exists(targetModLibrary))
                        {
                            M3Log.Information(@"Creating target mod library directory: " + targetModLibrary);
                            Directory.CreateDirectory(targetModLibrary);
                        }

                        var sameRoot = Path.GetPathRoot(targetModLibrary) == Path.GetPathRoot(modsDir);

                        var directoriesInModsDir = Directory.GetDirectories(modsDir);

                        var numToMigrate = directoriesInModsDir.Count(x => File.Exists(Path.Combine(x, @"moddesc.ini")));
                        var numMigrated = 0;

                        foreach (var modDirToMove in directoriesInModsDir)
                        {
                            var moddesc = Path.Combine(modDirToMove, @"moddesc.ini");
                            if (File.Exists(moddesc))
                            {
                                numMigrated++;
                                MigratingModsTask.TaskText = M3L.GetString(M3L.string_interp_migratingModsXoFY, numMigrated, numToMigrate);
                                //Migrate this folder
                                var targetDir = Path.Combine(targetModLibrary, Path.GetFileName(modDirToMove));
                                M3Log.Information($@"Migrating mod into ME3 directory: {modDirToMove} -> {targetDir}");
                                if (!Directory.Exists(targetDir))
                                {
                                    if (sameRoot)
                                    {
                                        Directory.Move(modDirToMove, targetDir);
                                    }
                                    else
                                    {
                                        M3Log.Information(@" >> Copying existing mod directory");
                                        Directory.CreateDirectory(targetDir);
                                        CopyDir.CopyAll_ProgressBar(new DirectoryInfo(modDirToMove), new DirectoryInfo(targetDir));
                                        M3Log.Information(@" >> Deleting existing directory");
                                        M3Utilities.DeleteFilesAndFoldersRecursively(modDirToMove);
                                    }

                                    M3Log.Information($@"Migrated {modDirToMove}");
                                }
                                else
                                {
                                    M3Log.Warning(@"Target directory already exists! Not migrating this directory.");
                                }
                            }
                        }

                        MigratingModsTask.SetDone();
                        M3Log.Information(@"Step 1: Finished mod migration");

                        // 2. MIGRATE SETTINGS
                        MigratingSettings.SetInProgress();
                        M3Log.Information(@"Step 2: Begin settings migration");
                        var me3cmminif = Path.Combine(exeDir, @"me3cmm.ini");
                        if (File.Exists(me3cmminif))
                        {
                            M3Log.Information(@"Migrating me3cmm.ini settings");
                            IniData me3cmmini = new FileIniDataParser().ReadFile(me3cmminif);
                            var updaterServiceUsername = me3cmmini[@"UpdaterService"][@"username"];
                            if (string.IsNullOrWhiteSpace(Settings.UpdaterServiceUsername) && !string.IsNullOrWhiteSpace(updaterServiceUsername))
                            {
                                Settings.UpdaterServiceUsername = updaterServiceUsername;
                                M3Log.Information(@"Migrated Updater Service Username: " + updaterServiceUsername);
                            }

                            var manifestsPath = me3cmmini[@"UpdaterService"][@"manifestspath"];
                            if (string.IsNullOrWhiteSpace(Settings.UpdaterServiceManifestStoragePath) && !string.IsNullOrWhiteSpace(manifestsPath))
                            {
                                Settings.UpdaterServiceManifestStoragePath = manifestsPath;
                                M3Log.Information(@"Migrated Updater Service Manifests Path: " + manifestsPath);
                            }

                            var lzmaStoragePath = me3cmmini[@"UpdaterService"][@"lzmastoragepath"];
                            if (string.IsNullOrWhiteSpace(Settings.UpdaterServiceLZMAStoragePath) && !string.IsNullOrWhiteSpace(lzmaStoragePath))
                            {
                                Settings.UpdaterServiceLZMAStoragePath = lzmaStoragePath;
                                M3Log.Information(@"Migrated Updater Service LZMA Storage Path: " + lzmaStoragePath);
                            }

                            //Modmaker Auto Injections
                            var controllerModOption = me3cmmini[@"Settings"][@"controllermoduser"];
                            if (Settings.ModMakerControllerModOption == false && controllerModOption == "1")
                            {
                                Settings.ModMakerControllerModOption = true; //Set to true (default is false)
                                M3Log.Information(@"Migrated Auto install controller mixins for ModMaker (true)");
                            }

                            var keybindsInjectionOption = me3cmmini[@"Settings"][@"autoinjectkeybinds"];
                            if (Settings.ModMakerAutoInjectCustomKeybindsOption == false && keybindsInjectionOption == "1")
                            {
                                Settings.ModMakerAutoInjectCustomKeybindsOption = true; //Set to true (default is false)
                                M3Log.Information(@"Migrated Auto inject keybinds for ModMaker (true)");
                            }

                            //Settings.Save();
                        }

                        //Migrate BIOGAME_DIRECTORIES
                        var biogameDirsF = Path.Combine(dataDir, @"BIOGAME_DIRECTORIES");
                        if (File.Exists(biogameDirsF))
                        {
                            var biodirs = File.ReadAllLines(biogameDirsF);
                            foreach (var line in biodirs)
                            {
                                var gamepath = Directory.GetParent(line).FullName;
                                M3Log.Information(@"Validating ME3CMM target: " + gamepath);
                                GameTargetWPF t = new GameTargetWPF(MEGame.ME3, gamepath, false);
                                var failureReason = t.ValidateTarget();
                                if (failureReason == null)
                                {
                                    M3Utilities.AddCachedTarget(t);
                                }
                                else
                                {
                                    M3Log.Error($@"Not migrating invalid target {gamepath}: {failureReason}");
                                }
                            }
                        }

                        //Migrate ALOT Installer, if found
                        var alotInstallerDir = Path.Combine(dataDir, @"ALOTInstaller");
                        if (Directory.Exists(alotInstallerDir))
                        {
                            M3Log.Information(@"Migrating ALOTInstaller tool");
                            var externalToolsALOTInstaller = Path.Combine(dataDir, @"ExternalTools", @"ALOTInstaller");
                            Directory.CreateDirectory(Path.Combine(dataDir, @"ExternalTools"));
                            Directory.Move(alotInstallerDir, externalToolsALOTInstaller);
                            M3Log.Information(@"Migrated ALOTInstaller to ExternalTools");
                        }

                        //Migrate ME3Explorer, if found
                        // Disabled 06/09/2022 - Well beyond date of this being useful
                        //var me3explorerDir = Path.Combine(dataDir, @"ME3Explorer");
                        //if (Directory.Exists(me3explorerDir))
                        //{
                        //    M3Log.Information(@"Migrating ME3Explorer tool");
                        //    var externalToolsME3ExplorerDir = Path.Combine(dataDir, @"ExternalTools", @"ME3Explorer");
                        //    Directory.CreateDirectory(Path.Combine(dataDir, @"ExternalTools"));
                        //    Directory.Move(me3explorerDir, externalToolsME3ExplorerDir);
                        //    M3Log.Information(@"Migrated ME3Explorer to ExternalTools");
                        //}

                        //Migrate cached modmaker mods
                        var modmakerCacheDir = Path.Combine(dataDir, @"modmaker", @"cache");
                        if (Directory.Exists(modmakerCacheDir))
                        {
                            var modmakerXmls = Directory.GetFiles(modmakerCacheDir, @"*.xml");
                            if (modmakerXmls.Any())
                            {
                                var mmNewCacheDir = M3Filesystem.GetModmakerDefinitionsCache();
                                M3Log.Information(@"Migrating ME3Tweaks ModMaker cached files");
                                foreach (var f in modmakerXmls)
                                {
                                    var fname = Path.GetFileName(f);
                                    var destName = Path.Combine(mmNewCacheDir, fname);
                                    if (!File.Exists(destName))
                                    {
                                        M3Log.Information(@"Migrating modmaker mod delta definition file " + fname);
                                        File.Move(f, destName);
                                    }
                                }

                                M3Log.Information(@"Migrated ModMaker cached files");
                            }
                        }


                        //MIGRATE 7z.dll - this will only perform an interim fix (maybe network failure?) as we use 19.0 and ME3MM used 18.05
                        var me3mm7z = Path.Combine(dataDir, @"tools\ModManagerCommandLine\x64\7z.dll");
                        var target7z = M3Filesystem.Get7zDllPath();
                        if (File.Exists(me3mm7z) && !File.Exists(target7z))
                        {
                            M3Log.Information($@"Copying ME3MM 7z.dll to ME3Tweaks Mod Manager dll location: {me3mm7z} -> {target7z}");
                            File.Copy(me3mm7z, target7z, true);
                            M3Log.Information(@"Copied ME3MM 7z dll");
                        }

                        // Migrate DLC_AUTH_FAIL
                        var me3mmAuthFail = Path.Combine(dataDir, @"help\DLC_AUTH_FAIL.png");
                        var targetAuthFail = Path.Combine(M3Filesystem.GetLocalHelpResourcesDirectory(), @"DLC_AUTH_FAIL.png");
                        if (File.Exists(me3mmAuthFail) && !File.Exists(targetAuthFail))
                        {
                            M3Log.Information($@"Copying DLC_AUTH_FAIL help resource to ME3Tweaks Mod Manager help resources location: {me3mmAuthFail} -> {targetAuthFail}");
                            File.Copy(me3mmAuthFail, targetAuthFail, true);
                            M3Log.Information(@"Copied DLC_AUTH_FAIL");
                        }

                        //MIGRATE MOD GROUPS (batch install queues)
                        var modGroupsDir = Path.Combine(dataDir, @"modgroups");
                        if (Directory.Exists(modGroupsDir))
                        {
                            M3Log.Information(@"Migrating batch mod groups");
                            var queues = Directory.EnumerateFiles(modGroupsDir, @"*.txt").ToList();
                            foreach (var queue in queues)
                            {
                                var biqDest = Path.Combine(M3LoadedMods.GetBatchInstallGroupsDirectory(), Path.GetFileName(queue));
                                M3Log.Information($@"Migrating mod install group: {queue} -> {biqDest}");
                                File.Move(queue, biqDest, true);
                                M3Log.Information(@"Migrated " + Path.GetFileName(queue));
                            }
                        }


                        // MIGRATE override
                        var overrideDir = Path.Combine(dataDir, @"override");
                        if (Directory.Exists(overrideDir))
                        {
                            M3Log.Information(@"Migrating override");
                            var filesInKBDir = Directory.EnumerateFiles(overrideDir, @"*.xml").ToList();
                            foreach (var file in filesInKBDir)
                            {
                                var keybindDir = Path.Combine(M3Filesystem.GetKeybindsOverrideFolder(), @"me3-" + Path.GetFileName(file));
                                M3Log.Information($@"Migrating keybinds override: {file} -> {keybindDir}");
                                File.Move(file, keybindDir, true);
                                M3Log.Information(@"Migrated " + Path.GetFileName(file));
                            }
                        }

                        MigratingSettings.SetDone();

                        M3Log.Information(@"Step 2: Finished settings migration");
                        // 3. CLEANUP
                        App.Current.Dispatcher.Invoke(delegate { cleanup = M3L.ShowDialog(null, M3L.GetString(M3L.string_dialog_performMe3cmmCleanup), M3L.GetString(M3L.string_performCleanupQuestion), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes; });
                        if (cleanup)
                        {
                            M3Log.Information(@"Step 3: Cleaning up");
                            CleaningUpTask.SetInProgress();
                            var directoriesInDataDir = Directory.GetFileSystemEntries(dataDir);
                            foreach (var entry in directoriesInDataDir)
                            {
                                var name = Path.GetFileName(entry);
                                if (Directory.Exists(entry))
                                {
                                    switch (name.ToLower())
                                    {
                                        case @"deployed mods":
                                        case @"externaltools": // Created by M3 at this point
                                        case @"patch_001_extracted":
                                        case @"pccdumps": //guess these might be useful
                                            continue;
                                        default:
                                            try
                                            {
                                                M3Log.Information(@"Deleting directory: " + entry);
                                                M3Utilities.DeleteFilesAndFoldersRecursively(entry, true);
                                            }
                                            catch (Exception e)
                                            {
                                                M3Log.Error($@"Unable to delete item in data directory: {entry}, reason: {e.Message}");
                                            }

                                            break;
                                    }
                                }
                                else if (File.Exists(entry))
                                {
                                    try
                                    {
                                        M3Log.Information(@"Cleanup: Deleting file " + entry);
                                        File.Delete(entry);
                                    }
                                    catch (Exception e)
                                    {
                                        M3Log.Error($@"Unable to delete {entry}: {e.Message}");
                                    }
                                }
                            }

                            // Install redirect to ensure user shortcuts continue to work
                            var me3cmmPath = Path.Combine(exeDir, @"ME3CMM.exe");
                            M3Log.Information(@"Writing redirector to " + me3cmmPath);
                            M3Utilities.ExtractInternalFile(@"ME3TweaksModManager.updater.ME3CMM.exe", me3cmmPath, true);

                        }
                        else
                        {
                            M3Log.Information(@"Skipping step 3: cleanup due to user request.");
                        }

                        CleaningUpTask.SetDone();
                        M3Log.Information(@"Step 3: Cleaned up");
                        Thread.Sleep(3000);
                    }
                    else
                    {
                        migrated = false;
                        M3Log.Error(@"mods and/or data dir don't exist! We will not attempt migration.");
                    }
                }
                catch (Exception e)
                {
                    migrated = false;
                    M3Log.Error(@"Error in migration: " + e.Message);
                    Crashes.TrackError(e);
                }
                TelemetryInterposer.TrackEvent(@"ME3CMM Migration", new Dictionary<string, string>()
                {
                    {@"Migrated", migrated.ToString()},
                    {@"Cleaned up", cleanup.ToString()},
                });
                M3Log.Information(@"<<<< Exiting ME3CMMMigration Thread");
            };
            nbw.RunWorkerCompleted += (a, b) =>
                {
                    M3Log.Information(@"Migration has completed.");
                    M3L.ShowDialog(null, M3L.GetString(M3L.string_dialog_me3cmmMigrationCompleted));
                    Close();
                };
            nbw.RunWorkerAsync();
        }
    }
}
