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
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BackupNagSystem.xaml
    /// </summary>
    public partial class BackupNagSystem : MMBusyPanelBase
    {
        public BackupNagSystem()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            OpenBackupPanelCommand = new GenericCommand(OpenBackupPanel);
            CloseCommand = new GenericCommand(ClosePanel);
        }

        public ICommand CloseCommand { get; set; }

        public ICommand OpenBackupPanelCommand { get; set; }

        private void OpenBackupPanel()
        {
            OnClosing(new DataEventArgs(true));
        }

        private void ClosePanel()
        {
            OnClosing(new DataEventArgs(false));
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            
        }

        public override void OnPanelVisible()
        {
        }
    }
}
