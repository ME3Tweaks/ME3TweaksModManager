using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FontAwesome.WPF;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;
using Serilog;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for ME3CMMMigrationWindow.xaml
    /// </summary>
    public partial class ME3CMMMigrationWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<BasicUITask> Tasks { get; }= new ObservableCollectionExtended<BasicUITask>();
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
                exeDir = @"E:\ME3CMM";

                var modsDir = Path.Combine(exeDir, @"mods");
                var dataDir = Path.Combine(exeDir, @"data");
                if (Directory.Exists(modsDir) && Directory.Exists(dataDir))
                {
                    Log.Information(@"mods and data dir exist.");
                    // 1. MIGRATE MODS
                    Log.Information(@"Step 1: Migrate mods");

                    var targetModLibrary = Utilities.GetModsDirectory();
                    MigratingModsTask.Icon = FontAwesomeIcon.Spinner;
                    MigratingModsTask.Spin = true;
                    MigratingModsTask.Foreground = Brushes.CadetBlue;
                    var directoriesInModsDir = Directory.GetDirectories(modsDir);
                    foreach(var modDirToMove in directoriesInModsDir)
                    {
                        var moddesc = Path.Combine(modDirToMove, @"moddesc.ini");
                        if (File.Exists(moddesc))
                        {
                            //Migrate this folder
                            var targetDir = Path.Combine(modsDir, @"ME3", Path.GetDirectoryName(modDirToMove));
                            Log.Information($@"Migrating mod into ME3 directory: {modDirToMove} -> {targetDir}");
                            Directory.Move(modDirToMove, targetDir);
                            Log.Information($@"Migrated {modDirToMove}");

                        }
                    }
                    MigratingModsTask.Icon = FontAwesomeIcon.CheckCircle;
                    MigratingModsTask.Spin = false;
                    MigratingModsTask.Foreground = Brushes.ForestGreen;
                    Log.Information(@"Step 1: Finished mod migration");


                    // 2. MIGRATE SETTINGS
                    Log.Information(@"Step 2: Begin settings migration");
                    Log.Information(@"Step 2: Finished settings migration");
                    // 3. CLEANUP
                    Log.Information(@"Step 3: Cleaning up");
                    Log.Information(@"Step 3: Cleaned up");



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
                //Close();
            };
        }
    }
}
