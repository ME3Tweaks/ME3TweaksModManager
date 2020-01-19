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
    /// Interaction logic for ImportInstalledDLCModPanel.xaml
    /// </summary>
    public partial class ImportInstalledDLCModPanel : MMBusyPanelBase
    {
        public ImportInstalledDLCModPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand ImportSelectedDLCFolderCommand { get; set; }
        public ICommand CloseCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
            ImportSelectedDLCFolderCommand = new GenericCommand(ImportSelectedFolder, CanImportSelectedFolder);
        }

        private bool CanImportSelectedFolder()
        {
            throw new NotImplementedException();
        }

        private void ImportSelectedFolder()
        {
            throw new NotImplementedException();
        }

        private bool CanClosePanel()
        {
            throw new NotImplementedException();
        }

        private void ClosePanel()
        {
            throw new NotImplementedException();
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
        }

        public override void OnPanelVisible()
        {
        }
    }
}
