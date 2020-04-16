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
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ConflictDetectorPanel.xaml
    /// </summary>
    public partial class ConflictDetectorPanel : MMBusyPanelBase
    {
        public ObservableCollectionExtended<GameTarget> ConflictTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public GameTarget SelectedTarget { get; set; }
        public ConflictDetectorPanel()
        {
            DataContext = this;
            InitializeComponent();
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //throw new NotImplementedException();
        }

        public void OnSelectedTargetChanged()
        {
            if (SelectedTarget == null)
            {

            }
            else
            {
                SelectedTarget.Sup
            }
        }

        public override void OnPanelVisible()
        {
            ConflictTargets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Selectable));
        }
    }
}
