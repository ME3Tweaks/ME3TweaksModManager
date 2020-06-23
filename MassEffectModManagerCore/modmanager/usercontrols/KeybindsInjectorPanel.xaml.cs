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
        private void LoadCommands()
        {
            OpenKeybindsDirectoryCommand = new GenericCommand(() => Utilities.OpenExplorer(Utilities.GetKeybindsOverrideFolder()));
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty), CanClose);
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
