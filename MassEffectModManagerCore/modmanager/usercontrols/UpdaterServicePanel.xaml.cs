using MassEffectModManagerCore.ui;
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
    /// Interaction logic for UpdaterServicePanel.xaml
    /// </summary>
    public partial class UpdaterServicePanel : MMBusyPanelBase
    {
        public UpdaterServicePanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public bool OperationInProgress { get; set; }
        public ICommand CloseCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
        }

        private bool CanClosePanel() => !OperationInProgress;

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
        }

        public override void OnPanelVisible()
        {
        }
    }
}
