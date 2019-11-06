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
    /// Interaction logic for PreviewWelcomePanel.xaml
    /// </summary>
    public partial class PreviewWelcomePanel : MMBusyPanelBase
    {
        public PreviewWelcomePanel()
        {
            InitializeComponent();
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void Close()
        {
            OnClosing(DataEventArgs.Empty);
            Settings.ShowedPreviewPanel = true;
            Settings.Save();
        }

        public override void OnPanelVisible()
        {
        }

        private void JoinDiscord_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenWebpage(App.DISCORD_INVITE_LINK);
        }

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
