using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    /// Interaction logic for OptionsPanel.xaml
    /// </summary>
    public partial class OptionsPanel : MMBusyPanelBase
    {
        public OptionsPanel()
        {
            LoadCommands();
            InitializeComponent();
        }

        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty));
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {

        }

        private void ChangeTheme_Dark_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeTheme(true);
        }

        private void ChangeTheme_Light_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeTheme(false);
        }

        private void ChangeTheme(bool dark)
        {
            if (Settings.DarkTheme ^ dark)
            {
                Settings.DarkTheme = !Settings.DarkTheme;
                //Settings.Save();
                mainwindow.SetTheme();
            }
        }
    }
}
