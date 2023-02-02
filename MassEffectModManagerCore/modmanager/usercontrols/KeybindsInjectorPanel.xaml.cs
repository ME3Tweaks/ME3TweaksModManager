using System.IO;
using System.Linq;
using System.Windows.Input;
using AdonisUI.Controls;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.ui;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for KeybindsInjector.xaml
    /// </summary>
    public partial class KeybindsInjectorPanel : MMBusyPanelBase
    {
        public GameTargetWPF SelectedME2Target { get; set; }
        public GameTargetWPF SelectedME3Target { get; set; }

        public ObservableCollectionExtended<KeybindsFile> ME1Keybinds { get; } = new ObservableCollectionExtended<KeybindsFile>();
        public ObservableCollectionExtended<KeybindsFile> ME2Keybinds { get; } = new ObservableCollectionExtended<KeybindsFile>();
        public ObservableCollectionExtended<KeybindsFile> ME3Keybinds { get; } = new ObservableCollectionExtended<KeybindsFile>();

        public KeybindsFile SelectedME1Keybinds { get; set; }
        public KeybindsFile SelectedME2Keybinds { get; set; }
        public KeybindsFile SelectedME3Keybinds { get; set; }

        public ObservableCollectionExtended<GameTargetWPF> ME2Targets { get; } = new ObservableCollectionExtended<GameTargetWPF>();
        public ObservableCollectionExtended<GameTargetWPF> ME3Targets { get; } = new ObservableCollectionExtended<GameTargetWPF>();

        public bool KeybindsInstallingME1 { get; set; }
        public bool KeybindsInstallingME2 { get; set; }
        public bool KeybindsInstallingME3 { get; set; }

        public KeybindsInjectorPanel()
        {
            LoadCommands();
        }

        public ICommand CloseCommand { get; private set; }
        public ICommand OpenKeybindsDirectoryCommand { get; private set; }

        public ICommand ResetKeybindsME3Command { get; set; }
        public ICommand FixTalonMercME3Keybinds { get; set; }
        public ICommand InstallSelectedKeybindsME3Command { get; set; }
        private void LoadCommands()
        {
            OpenKeybindsDirectoryCommand = new GenericCommand(() => M3Utilities.OpenExplorer(M3Filesystem.GetKeybindsOverrideFolder()), CanClose);
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty), CanClose);

            ResetKeybindsME3Command = new GenericCommand(ResetME3Keybinds, CanRestoreKeybinds);
            //FixTalonMercME3Keybinds = new GenericCommand(() => OnClosing(DataEventArgs.Empty), CanClose);
            InstallSelectedKeybindsME3Command = new GenericCommand(InternalInstallME3Keybinds, CanInstallME3Keybinds);

        }

        private bool CanInstallME3Keybinds()
        {
            return SelectedME3Keybinds != null && SelectedME3Target != null && !KeybindsInstallingME3;
        }

        private void InternalInstallME3Keybinds()
        {
            if (!BackupService.GetBackupStatus(MEGame.ME3).BackedUp)
            {
                var result = M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_noBackupMessage, MEGame.ME3), M3L.GetString(M3L.string_backupWarning), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No;
                if (!result)
                    return; // Don't proceed
            }

            KeybindsInstallingME3 = true;
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ME3KeybindsInstaller");
            nbw.DoWork += (await, b) =>
            {
                var xmlText = File.ReadAllText(SelectedME3Keybinds.filepath);
                InstallME3Keybinds(xmlText, SelectedME3Target);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    M3Log.Error(@"Error setting ME3 keybinds: " + b.Error.Message);
                }
                KeybindsInstallingME3 = false;
                CommandManager.InvalidateRequerySuggested();
            };
            nbw.RunWorkerAsync();
        }

        /// <summary>
        /// Installs the specified text as the BioInput file for the target's Coalesced.bin file. This call is synchronous and should be run on a background thread.
        /// </summary>
        /// <param name="bioInputXml">TEXT of the bioinput.xml file</param>
        /// <param name="target">Target to update</param>
        public static void InstallME3Keybinds(string bioInputXml, GameTargetWPF target)
        {
            var coalPath = Path.Combine(target.TargetPath, @"BioGame", @"CookedPCConsole", @"Coalesced.bin");
            InstallME3Keybinds(bioInputXml, coalPath);
        }

        /// <summary>
        /// Installs the specified text as the BioInput file to the specified Coalesced.bin file. This call is synchronous and should be run on a background thread.
        /// </summary>
        /// <param name="bioInputXml">TEXT of the bioinput.xml file</param>
        /// <param name="target">Target to update</param>
        public static void InstallME3Keybinds(string bioInputXml, string coalPath)
        {
            if (File.Exists(coalPath))
            {
                using FileStream fs = new FileStream(coalPath, FileMode.Open);
                var coalescedFilemapping = CoalescedConverter.DecompileGame3ToMemory(fs);
                fs.Close(); //release
                coalescedFilemapping[@"BioInput.xml"] = bioInputXml;
                var outStream = CoalescedConverter.CompileFromMemory(coalescedFilemapping);
                outStream.WriteToFile(coalPath);
            }
        }

        private void ResetME3Keybinds()
        {
            if (!BackupService.GetBackupStatus(MEGame.ME3).BackedUp)
            {
                var result = M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_noBackupMessage, MEGame.ME3), M3L.GetString(M3L.string_backupWarning), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No;
                if (!result)
                    return; // Don't proceed
            }

            KeybindsInstallingME3 = true;
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ME3KeybindsInstaller");
            nbw.DoWork += (await, b) =>
            {
                var coalPath = Path.Combine(BackupService.GetGameBackupPath(MEGame.ME3), @"BioGame", @"CookedPCConsole", @"Coalesced.bin");
                if (File.Exists(coalPath))
                {
                    using FileStream fs = new FileStream(coalPath, FileMode.Open);
                    var coalescedFilemapping = CoalescedConverter.DecompileGame3ToMemory(fs);
                    InstallME3Keybinds(coalescedFilemapping[@"BioInput.xml"], SelectedME3Target);
                }
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    M3Log.Error(@"Error resetting ME3 keybinds: " + b.Error.Message);
                }
                KeybindsInstallingME3 = false;
                CommandManager.InvalidateRequerySuggested();
            };
            nbw.RunWorkerAsync();
        }

        private bool CanRestoreKeybinds()
        {
            return BackupService.GetGameBackupPath(MEGame.ME3) != null && SelectedME3Target != null && !KeybindsInstallingME3;
        }

        private bool CanClose() => !KeybindsInstallingME3 && !KeybindsInstallingME2 && !KeybindsInstallingME1;
        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            var kbFiles = Directory.GetFiles(M3Filesystem.GetKeybindsOverrideFolder(), @"*.*");
            foreach (var v in kbFiles)
            {
                var fname = Path.GetFileName(v);
                var extension = Path.GetExtension(v);
                if (fname.StartsWith(@"me1-") && extension == @".ini") ME1Keybinds.Add(new KeybindsFile(v));
                if (fname.StartsWith(@"me2-") && extension == @".ini") ME2Keybinds.Add(new KeybindsFile(v));
                if (fname.StartsWith(@"me3-") && extension == @".xml") ME3Keybinds.Add(new KeybindsFile(v));
            }


            ME2Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == MEGame.ME2));
            ME3Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == MEGame.ME3));
            SelectedME2Target = ME2Targets.FirstOrDefault();
            SelectedME3Target = ME3Targets.FirstOrDefault();
        }

        public class KeybindsFile
        {
            public bool Selectable { get; set; } = true;
            public string filepath;
            public KeybindsFile(string filepath)
            {
                this.filepath = filepath;
            }

            public override string ToString() => !Selectable ? filepath : Path.GetFileName(filepath);
        }

        public static string GetDefaultKeybindsOverride(MEGame game)
        {
            var path = M3Filesystem.GetKeybindsOverrideFolder();
            if (game == MEGame.ME1) return Path.Combine(path, @"me1-bioinput.ini");
            if (game == MEGame.ME2) return Path.Combine(path, @"me2-bioinput.ini");
            if (game == MEGame.ME3) return Path.Combine(path, @"me3-bioinput.xml");
            if (game == MEGame.LE1) return Path.Combine(path, @"le1-bioinput.ini");
            if (game == MEGame.LE2) return Path.Combine(path, @"le2-bioinput.ini");
            if (game == MEGame.LE3) return Path.Combine(path, @"le3-bioinput.xml");
            return null;
        }

        public override bool DisableM3AutoSizer { get; set; } = true;
    }
}
