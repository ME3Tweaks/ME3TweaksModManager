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
using PropertyChanged;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for OptionsPanel.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class OptionsPanel : MMBusyPanelBase
    {
        public OptionsPanel()
        {
            LibraryDir = Utilities.GetModsDirectory();
            LoadCommands();
            InitializeComponent();
        }


        public string LibraryDir { get; set; }
        public ICommand CloseCommand { get; set; }
        public ICommand ChangeLibraryDirCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty));
            ChangeLibraryDirCommand = new GenericCommand(ChangeLibraryDir);
        }

        private void ChangeLibraryDir()
        {

            if (mainwindow.ChooseModLibraryPath(false))
            {
                LibraryDir = Settings.ModLibraryPath;
            }
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
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
