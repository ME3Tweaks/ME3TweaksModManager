using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FontAwesome.WPF;
using IniParser;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.usercontrols;
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
        BasicUITask MigratingModsTask = new BasicUITask("Migrating mods");
        BasicUITask MigratingSettings = new BasicUITask("Migrating settings");
        BasicUITask CleaningUpTask = new BasicUITask("Cleaning up");
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
                            MigratingModsTask.TaskText = $"Migrating mods [{numMigrated}/{numToMigrate}]";
                            //Migrate this folder
                            var targetDir = Path.Combine(modsDir, @"ME3", Path.GetFileName(modDirToMove));
                            Log.Information($@"Migrating mod into ME3 directory: {modDirToMove} -> {targetDir}");
                            if (!Directory.Exists(targetDir))
                            {
                                if (sameRoot)
                                {
                                    Directory.Move(modDirToMove, targetDir);
                                }
                                else
                                {
                                    Log.Information(@"Copying existing mod directory");
                                    Directory.CreateDirectory(targetDir);
                                    CopyDir.CopyAll_ProgressBar(new DirectoryInfo(modDirToMove), new DirectoryInfo(targetDir));
                                    Log.Information(@"Deleting existing directory");
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
                            Settings.UpdaterServiceLZMAStoragePath = updaterServiceUsername;
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

                    MigratingSettings.SetDone();

                    Log.Information(@"Step 2: Finished settings migration");
                    // 3. CLEANUP
                    Log.Information(@"Step 3: Cleaning up");
                    CleaningUpTask.SetInProgress();
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
                        Log.Information(@"Migration has completed.");
                        Close();
                    };
            nbw.RunWorkerAsync();
        }
    }
}
