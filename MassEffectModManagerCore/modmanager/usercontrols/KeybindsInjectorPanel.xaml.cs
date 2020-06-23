using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using ByteSizeLib;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for KeybindsInjector.xaml
    /// </summary>
    public partial class KeybindsInjectorPanel : MMBusyPanelBase
    {
        public GameTarget SelectedME2Target { get; set; }
        public GameTarget SelectedME3Target { get; set; }

        public ObservableCollectionExtended<KeybindsFile> ME1Keybinds { get; } = new ObservableCollectionExtended<KeybindsFile>();
        public ObservableCollectionExtended<KeybindsFile> ME2Keybinds { get; } = new ObservableCollectionExtended<KeybindsFile>();
        public ObservableCollectionExtended<KeybindsFile> ME3Keybinds { get; } = new ObservableCollectionExtended<KeybindsFile>();

        public KeybindsFile SelectedME1Keybinds { get; set; }
        public KeybindsFile SelectedME2Keybinds { get; set; }
        public KeybindsFile SelectedME3Keybinds { get; set; }

        public ObservableCollectionExtended<GameTarget> ME2Targets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<GameTarget> ME3Targets { get; } = new ObservableCollectionExtended<GameTarget>();

        public KeybindsInjectorPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand CloseCommand { get; private set; }
        public ICommand OpenKeybindsDirectoryCommand { get; private set; }

        public ICommand ResetKeybindsME3Command { get; set; }
        public ICommand FixTalonMercME3Keybinds { get; set; }
        public ICommand InstallSelectedKeybindsME3Command { get; set; }
        private void LoadCommands()
        {
            OpenKeybindsDirectoryCommand = new GenericCommand(() => Utilities.OpenExplorer(Utilities.GetKeybindsOverrideFolder()));
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty), CanClose);

            ResetKeybindsME3Command = new GenericCommand(ResetME3Keybinds, CanRestoreKeybinds);
            //FixTalonMercME3Keybinds = new GenericCommand(() => OnClosing(DataEventArgs.Empty), CanClose);
            InstallSelectedKeybindsME3Command = new GenericCommand(InstallME3Keybinds, CanInstallME3Keybinds);

        }

        private bool CanInstallME3Keybinds()
        {
            return SelectedME3Keybinds != null && SelectedME3Target != null;
        }

        private void InstallME3Keybinds()
        {
            var xmlText = File.ReadAllText(SelectedME3Keybinds.filepath);
            internalInstallME3Keybinds(xmlText, SelectedME3Target);
        }

        private void internalInstallME3Keybinds(string bioInputXml, GameTarget target)
        {
            var coalPath = Path.Combine(target.TargetPath, @"BioGame", @"CookedPCConsole", @"Coalesced.bin");
            if (File.Exists(coalPath))
            {
                using FileStream fs = new FileStream(coalPath, FileMode.Open);
                var coalescedFilemapping = MassEffect3.Coalesce.Converter.DecompileToMemory(fs);
                fs.Close(); //release
                coalescedFilemapping["BioInput.xml"] = bioInputXml;
                var outStream = MassEffect3.Coalesce.Converter.CompileFromMemory(coalescedFilemapping);
                outStream.WriteToFile(coalPath);
            }
        }

        private void ResetME3Keybinds()
        {
            var coalPath = Path.Combine(Utilities.GetGameBackupPath(Mod.MEGame.ME3), @"BioGame", @"CookedPCConsole", @"Coalesced.bin");
            if (File.Exists(coalPath))
            {
                using FileStream fs = new FileStream(coalPath, FileMode.Open);
                var coalescedFilemapping = MassEffect3.Coalesce.Converter.DecompileToMemory(fs);
                internalInstallME3Keybinds(coalescedFilemapping["BioInput.xml"], SelectedME3Target);
            }
        }

        private bool CanRestoreKeybinds()
        {
            return Utilities.GetGameBackupPath(Mod.MEGame.ME3) != null && SelectedME3Target != null;
        }

        private bool CanClose() => true;
        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {
            var kbFiles = Directory.GetFiles(Utilities.GetKeybindsOverrideFolder(), @"*.*");
            foreach (var v in kbFiles)
            {
                var fname = Path.GetFileName(v);
                var extension = Path.GetExtension(v);
                if (fname.StartsWith(@"me1-") && extension == @".ini") ME1Keybinds.Add(new KeybindsFile(v));
                if (fname.StartsWith(@"me2-") && extension == @".ini") ME2Keybinds.Add(new KeybindsFile(v));
                if (fname.StartsWith(@"me3-") && extension == @".xml") ME3Keybinds.Add(new KeybindsFile(v));
            }


            ME2Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME2));
            ME3Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME3));
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
    }
}
