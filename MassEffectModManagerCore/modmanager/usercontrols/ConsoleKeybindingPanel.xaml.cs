using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using MassEffectModManagerCore.modmanager.gameini;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Path = System.IO.Path;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ConsoleKeybindingPanel.xaml
    /// </summary>
    public partial class ConsoleKeybindingPanel : MMBusyPanelBase
    {
        public bool IsListeningForKey { get; set; }

        #region Key texts
        public string ME1FullConsoleKeyText { get; set; }
        public string ME1MiniConsoleKeyText { get; set; }
        public string ME2FullConsoleKeyText { get; set; }
        public string ME2MiniConsoleKeyText { get; set; }
        public string ME3FullConsoleKeyText { get; set; }
        public string ME3MiniConsoleKeyText { get; set; }
        #endregion
        public ConsoleKeybindingPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand ME1SetFullKeyCommand { get; set; }
        public ICommand ME1SetMiniKeyCommand { get; set; }
        private void LoadCommands()
        {
            ME1SetFullKeyCommand = new GenericCommand(SetME1FullKey, CanSetME1FullKey);
            ME1SetMiniKeyCommand = new GenericCommand(SetME1MiniKey, CanSetME1MiniKey);
        }

        private void SetME1MiniKey()
        {
            throw new NotImplementedException();
        }

        private void SetME1FullKey()
        {
            throw new NotImplementedException();
        }

        private bool CanSetME1MiniKey()
        {
            throw new NotImplementedException();
        }

        private bool CanSetME1FullKey()
        {
            throw new NotImplementedException();
        }

        public Action<Key> OnKeyPressed;

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (IsListeningForKey)
            {
                IsListeningForKey = false;
                if (e.Key == Key.Escape)
                {
                    return;
                }

                OnKeyPressed?.Invoke(e.Key);
                OnKeyPressed = null;
            }
            else if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public GameTarget SelectedME2Target { get; set; }
        public GameTarget SelectedME3Target { get; set; }
        public ObservableCollectionExtended<GameTarget> ME2Targets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<GameTarget> ME3Targets { get; } = new ObservableCollectionExtended<GameTarget>();

        public override void OnPanelVisible()
        {
            ME2Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME2));
            ME3Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME3));
            SelectedME2Target = ME2Targets.FirstOrDefault();
            SelectedME3Target = ME3Targets.FirstOrDefault();
            LoadME1Keys();
        }

        public bool HasME1Install { get; set; } = true;
        private void LoadME1Keys()
        {
            if (mainwindow.InstallationTargets.Any())
            {
                var iniFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", @"Mass Effect", @"Config", @"BIOInput.ini");
                var ini = DuplicatingIni.LoadIni(iniFile);
                var engineConsole = ini.Sections.FirstOrDefault(x => x.Header == @"Engine.Console");
                if (engineConsole != null)
                {
                    var consoleKey = engineConsole.Entries.FirstOrDefault(x => x.Key == @"ConsoleKey");
                    if (consoleKey == null)
                    {
                        ME1FullConsoleKeyText = "Full console not keybindined";
                    }
                    else
                    {
                        ME1FullConsoleKeyText = $"Full console available on {consoleKey.Value}";
                    }

                    var typeKey = engineConsole.Entries.FirstOrDefault(x => x.Key == @"TypeKey");
                    if (typeKey == null)
                    {
                        ME1MiniConsoleKeyText = "Mini console not keybindined";
                    }
                    else
                    {
                        ME1MiniConsoleKeyText = $"Mini console available on {typeKey.Value}";
                    }
                }
            }
            else
            {
                HasME1Install = false;
                ME1FullConsoleKeyText = "Game not managed by Mod Manager";
                ME1MiniConsoleKeyText = "";
            }
            HasME1Install = false;

        }
    }
}
