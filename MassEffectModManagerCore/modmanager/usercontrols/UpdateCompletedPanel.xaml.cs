using System.Windows;
using System.Windows.Input;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
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
            InitializeComponent();
        }

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }
    }
}
