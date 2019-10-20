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
    /// Interaction logic for AboutPanel.xaml
    /// </summary>
    public partial class AboutPanel : UserControl
    {
        public AboutPanel()
        {
            InitializeComponent();
        }

        public event EventHandler<EventArgs> Close;
        protected virtual void OnClosing(EventArgs e)
        {
            EventHandler<EventArgs> handler = Close;
            handler?.Invoke(this, e);
        }

        private void Image_ME3Tweaks_Click(object sender, MouseButtonEventArgs e)
        {
            Utilities.OpenWebpage("https://me3tweaks.com");
        }

        private void About_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            OnClosing(EventArgs.Empty);
        }
    }
}
