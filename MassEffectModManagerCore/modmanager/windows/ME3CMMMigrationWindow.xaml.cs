using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using IniParser;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Serilog;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for ME3CMMMigrationWindow.xaml
    /// </summary>
    public partial class ME3CMMMigrationWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<BasicUITask> Tasks { get; } = new ObservableCollectionExtended<BasicUITask>();
        BasicUITask MigratingModsTask = new BasicUITask(M3L.GetString(M3L.string_migratingMods));
        BasicUITask MigratingSettings = new BasicUITask(M3L.GetString(M3L.string_migratingSettings));
        BasicUITask CleaningUpTask = new BasicUITask(M3L.GetString(M3L.string_cleaningUp));
        public ME3CMMMigrationWindow()
        {
            DataContext = this;
            InitializeComponent();
            Tasks.Add(MigratingModsTask);
            Tasks.Add(MigratingSettings);
            Tasks.Add(CleaningUpTask);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Migration_ContentRendered(object sender, EventArgs e)
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ME3CMMMigration");
            nbw.DoWork += (a, b) =>
            {
                Log.Information(@">>>> ME3CMMMigration Thread");
                Log.Information(@"Validate ME3CMM folders and files");
                var exeDir = Utilities.GetMMExecutableDirectory();
                //DEBUG ONLY

                //exeDir = @"E:\ME3CMM";

                var modsDir = Path.Combine(exeDir, @"mods");
                var dataDir = Path.Combine(exeDir, @"data");
                if (Directory.Exists(modsDir) && Directory.Exists(dataDir))
                {
                    Log.Information(@"mods and data dir exist.");
                    // 1. MIGRATE MODS
                    Log.Information(@"Step 1: Migrate mods");
                    MigratingModsTask.SetInProgress();

                    var targetModLibrary = Utilities.GetModsDirectory();
                    //DEBUG ONLY
                    //targetModLibrary = @"E:\ME3CMM\mods";

                    targetModLibrary = Path.Combine(targetModLibrary, @"ME3");
                    if (!Directory.Exists(targetModLibrary))
                    {
                        Log.Information(@"Creating target mod library directory: " + targetModLibrary);
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
                            var targetDir = Path.Combine(targetModLibrary, @"ME3", Path.GetFileName(modDirToMove));
                            Log.Information($@"Migrating mod into ME3 directory: {modDirToMove} -> {targetDir}");
                            if (!Directory.Exists(targetDir))
                            {
                                if (sameRoot)
                                {
                                    Directory.Move(modDirToMove, targetDir);
                                }
                                else
                                {
                                    Log.Information(@" >> Copying existing mod directory");
                                    Directory.CreateDirectory(targetDir);
                                    CopyDir.CopyAll_ProgressBar(new DirectoryInfo(modDirToMove), new DirectoryInfo(targetDir));
                                    Log.Information(@" >> Deleting existing directory");
                                    Utilities.DeleteFilesAndFoldersRecursively(modDirToMove);
                                }

                                Log.Information($@"Migrated {modDirToMove}");
                                //Thread.Sleep(200);
                            }
                            else
                            {
                                Log.Warning(@"Target directory already exists! Not migrating this directory.");
                            }
                        }
                    }

                    MigratingModsTask.SetDone();
                    Log.Information(@"Step 1: Finished mod migration");

                    // 2. MIGRATE SETTINGS
                    MigratingSettings.SetInProgress();
                    Log.Information(@"Step 2: Begin settings migration");
                    var me3cmminif = Path.Combine(exeDir, @"me3cmm.ini");
                    if (File.Exists(me3cmminif))
                    {
                        Log.Information(@"Migrating me3cmm.ini settings");
                        IniData me3cmmini = new FileIniDataParser().ReadFile(me3cmminif);
                        var updaterServiceUsername = me3cmmini[@"UpdaterService"][@"username"];
                        if (string.IsNullOrWhiteSpace(Settings.UpdaterServiceUsername) && !string.IsNullOrWhiteSpace(updaterServiceUsername))
                        {
                            Settings.UpdaterServiceUsername = updaterServiceUsername;
                            Log.Information(@"Migrated Updater Service Username: " + updaterServiceUsername);
                        }

                        var manifestsPath = me3cmmini[@"UpdaterService"][@"manifestspath"];
                        if (string.IsNullOrWhiteSpace(Settings.UpdaterServiceManifestStoragePath) && !string.IsNullOrWhiteSpace(manifestsPath))
                        {
                            Settings.UpdaterServiceManifestStoragePath = manifestsPath;
                            Log.Information(@"Migrated Updater Service Manifests Path: " + manifestsPath);
                        }

                        var lzmaStoragePath = me3cmmini[@"UpdaterService"][@"lzmastoragepath"];
                        if (string.IsNullOrWhiteSpace(Settings.UpdaterServiceLZMAStoragePath) && !string.IsNullOrWhiteSpace(lzmaStoragePath))
                        {
                            Settings.UpdaterServiceLZMAStoragePath = lzmaStoragePath;
                            Log.Information(@"Migrated Updater Service LZMA Storage Path: " + lzmaStoragePath);
                        }

                        //TODO: MODMAKER CONTROLLER AUTO-ADDINS

                        Settings.Save();
                    }

                    //Migrate BIOGAME_DIRECTORIES
                    var biogameDirsF = Path.Combine(dataDir, @"BIOGAME_DIRECTORIES");
                    if (File.Exists(biogameDirsF))
                    {
                        var biodirs = File.ReadAllLines(biogameDirsF);
                        foreach (var line in biodirs)
                        {
                            var gamepath = Directory.GetParent(line).FullName;
                            Log.Information(@"Validating ME3CMM target: " + gamepath);
                            GameTarget t = new GameTarget(Mod.MEGame.ME3, gamepath, false);
                            var failureReason = t.ValidateTarget();
                            if (failureReason == null)
                            {
                                Utilities.AddCachedTarget(t);
                            }
                            else
                            {
                                Log.Error($@"Not migrating invalid target {gamepath}: {failureReason}");
                            }
                        }
                    }

                    //Migrate ALOT Installer, if found
                    var alotInstallerDir = Path.Combine(dataDir, @"ALOTInstaller");
                    if (Directory.Exists(alotInstallerDir))
                    {
                        Log.Information(@"Migrating ALOTInstaller tool");
                        var externalToolsALOTInstaller = Path.Combine(dataDir, @"ExternalTools", @"ALOTInstaller");
                        Directory.CreateDirectory(Path.Combine(dataDir, @"ExternalTools"));
                        Directory.Move(alotInstallerDir, externalToolsALOTInstaller);
                        Log.Information(@"Migrated ALOTInstaller to ExternalTools");
                    }

                    //Migrate ME3Explorer, if found
                    var me3explorerDir = Path.Combine(dataDir, @"ME3Explorer");
                    if (Directory.Exists(me3explorerDir))
                    {
                        Log.Information(@"Migrating ME3Explorer tool");
                        var externalToolsME3ExplorerDir = Path.Combine(dataDir, @"ExternalTools", @"ME3Explorer");
                        Directory.CreateDirectory(Path.Combine(dataDir, @"ExternalTools"));
                        Directory.Move(me3explorerDir, externalToolsME3ExplorerDir);
                        Log.Information(@"Migrated ME3Explorer to ExternalTools");
                    }

                    //MIGRATE 7z.dll - this will only perform an interim fix as we use 19.0 and ME3MM used 18.05
                    var me3mm7z = Path.Combine(dataDir, @"tools\ModManagerCommandLine\x64\7z.dll");
                    var target7z = Utilities.Get7zDllPath();
                    if (File.Exists(me3mm7z) && !File.Exists(target7z))
                    {
                        Log.Information($@"Copying ME3CMM 7z.dll to ME3Tweaks Mod Manager dll location: {me3mm7z} -> {target7z}");
                        File.Copy(me3mm7z, target7z, true);
                        Log.Information(@"Copied ME3MM 7z dll");
                    }

                    //MIGRATE MOD GROUPS (batch install queues)/
                    //Migrate ME3Explorer, if found
                    var modGroupsDir = Path.Combine(dataDir, @"modgroups");
                    if (Directory.Exists(modGroupsDir))
                    {
                        Log.Information(@"Migrating batch mod groups");
                        var queues = Directory.EnumerateFiles(modGroupsDir, @"*.txt").ToList();
                        foreach (var queue in queues)
                        {
                            var biqDest = Path.Combine(Utilities.GetBatchInstallGroupsFolder(), Path.GetFileName(queue));
                            Log.Information($@"Migrating mod install group: {queue} -> {biqDest}");
                            File.Move(queue, biqDest, true);
                            Log.Information(@"Migrated " + Path.GetFileName(queue));
                        }
                    }

                    // MIGRATE override
                    var overrideDir = Path.Combine(dataDir, @"override");
                    if (Directory.Exists(overrideDir))
                    {
                        Log.Information(@"Migrating override");
                        var filesInKBDir = Directory.EnumerateFiles(modGroupsDir, @"*.xml").ToList();
                        foreach (var file in filesInKBDir)
                        {
                            var keybindDir = Path.Combine(Utilities.GetKeybindsOverrideFolder(), @"me3-" + Path.GetFileName(file));
                            Log.Information($@"Migrating keybinds override: {file} -> {keybindDir}");
                            File.Move(file, keybindDir, true);
                            Log.Information(@"Migrated " + Path.GetFileName(file));
                        }
                    }

                    MigratingSettings.SetDone();

                    Log.Information(@"Step 2: Finished settings migration");
                    // 3. CLEANUP
                    bool cleanup = false;
                    App.Current.Dispatcher.Invoke(delegate
                    {
                        cleanup = M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_performMe3cmmCleanup), M3L.GetString(M3L.string_performCleanupQuestion), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes;
                    });
                    if (cleanup)
                    {
                        Log.Information(@"Step 3: Cleaning up");
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
                                    case @"patch_001_extracted":
                                        continue;
                                    default:
                                        try
                                        {
                                            Log.Information(@"Deleting directory: " + entry);
                                            Utilities.DeleteFilesAndFoldersRecursively(entry, true);
                                        }
                                        catch (Exception e)
                                        {
                                            Log.Error($@"Unable to delete item in data directory: {entry}, reason: {e.Message}");
                                        }

                                        break;
                                }
                            }
                            else if (File.Exists(entry))
                            {
                                try
                                {
                                    Log.Information(@"Cleanup: Deleting file " + entry);
                                    File.Delete(entry);
                                }
                                catch (Exception e)
                                {
                                    Log.Error($@"Unable to delete {entry}: {e.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        Log.Information(@"Skipping step 3: cleanup due to user request.");
                    }

                    CleaningUpTask.SetDone();
                    Log.Information(@"Step 3: Cleaned up");
                    Thread.Sleep(3000);
                }
                else
                {
                    Log.Error(@"mods and/or data dir don't exist! We will not attempt migration.");
                }

                Log.Information(@"<<<< Exiting ME3CMMMigration Thread");
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occured in {nbw.Name} thread: {b.Error.Message}");
                }
                Log.Information(@"Migration has completed.");
                M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_me3cmmMigrationCompleted));
                Close();
            };
            nbw.RunWorkerAsync();
        }
    }
}
