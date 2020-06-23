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
        public ObservableCollectionExtended<GameTarget> ME2Targets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<GameTarget> ME3Targets { get; } = new ObservableCollectionExtended<GameTarget>();

        public KeybindsInjectorPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand CloseCommand { get; private set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty), CanClose);
        }

        private bool CanClose() => true;
        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {
            ME2Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME2));
            ME3Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME3));
            SelectedME2Target = ME2Targets.FirstOrDefault();
            SelectedME3Target = ME3Targets.FirstOrDefault();
        }
    }
}
