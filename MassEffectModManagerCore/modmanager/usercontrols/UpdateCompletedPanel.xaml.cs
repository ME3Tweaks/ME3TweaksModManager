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
    /// Interaction logic for UpdateCompletedPanel.xaml
    /// </summary>
    public partial class UpdateCompletedPanel : UserControl
    {

        public UpdateCompletedPanel(string message, string title)
        {
            DataContext = this;
            UpdateTitle = title;
            UpdateMessage = message;
            InitializeComponent();
        }

        public string UpdateTitle { get; }
        public string UpdateMessage { get; }

        public event EventHandler<EventArgs> Close;
        protected virtual void OnClosing(EventArgs e)
        {
            EventHandler<EventArgs> handler = Close;
            handler?.Invoke(this, e);
        }

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            OnClosing(EventArgs.Empty);
        }
    }
}
