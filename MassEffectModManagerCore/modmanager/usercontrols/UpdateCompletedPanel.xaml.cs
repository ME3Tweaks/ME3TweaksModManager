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
    /// Interaction logic for UpdateCompletedPanel.xaml
    /// </summary>
    public partial class UpdateCompletedPanel : MMBusyPanelBase
    {

        public UpdateCompletedPanel(string title, string message)
        {
            DataContext = this;
            UpdateTitle = title;
            UpdateMessage = message;
            InitializeComponent();
        }

        public string UpdateTitle { get; }
        public string UpdateMessage { get; }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            //throw new NotImplementedException();
        }

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }
    }
}
