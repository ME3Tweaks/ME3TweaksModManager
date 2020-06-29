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
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Pathoschild.FluentNexus.Models;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for OIGODisabler.xaml
    /// </summary>
    public partial class OIGODisabler : MMBusyPanelBase
    {
        public OIGODisabler()
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"Origin in-game overlay disabler panel", new WeakReference(this));

            DataContext = this;
            InitializeComponent();
        }

        public ObservableCollectionExtended<OIGGame> Games { get; } = new ObservableCollectionExtended<OIGGame>();

        public class OIGGame
        {
            public Mod.MEGame Game { get; private set; }
            public ObservableCollectionExtended<GameTarget> Targets { get; } = new ObservableCollectionExtended<GameTarget>();

            public OIGGame(Mod.MEGame game, IEnumerable<GameTarget> targets)
            {
                this.Game = game;
                this.Targets.ReplaceAll(targets);
            }
        }

        public ICommand CloseCommand { get; set; }

        public void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
        }

        public void ClosePanel() => OnClosing(DataEventArgs.Empty);

        private bool CanClose()
        {
            return true;
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            Games.Add(new OIGGame(Mod.MEGame.ME1, mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME1 && !x.IsCustomOption && x.GameSource != null && x.GameSource.Contains("Origin"))));
            Games.Add(new OIGGame(Mod.MEGame.ME2, mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME2 && !x.IsCustomOption && x.GameSource != null && x.GameSource.Contains("Origin"))));
            Games.Add(new OIGGame(Mod.MEGame.ME3, mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME3 && !x.IsCustomOption && x.GameSource != null && x.GameSource.Contains("Origin"))));
        }
    }
}
