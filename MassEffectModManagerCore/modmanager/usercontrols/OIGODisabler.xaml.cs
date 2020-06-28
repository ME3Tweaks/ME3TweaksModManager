using System;
using System.Collections.Generic;
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

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for OIGODisabler.xaml
    /// </summary>
    public partial class OIGODisabler : MMBusyPanelBase
    {
        public OIGODisabler()
        {
            InitializeComponent();
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
                
        }

        public override void OnPanelVisible()
        {
        }
    }
}
