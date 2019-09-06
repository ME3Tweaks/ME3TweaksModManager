using Microsoft.WindowsAPICodePack.Dialogs;
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
using System.Windows.Shapes;

namespace MassEffectModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        public OptionsWindow()
        {
            InitializeComponent();
        }

        private void ChangeLibraryLocation_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog m = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select mod library folder"
            };
            if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                Properties.Settings.Default.ModLibraryPath = m.FileName;
                Properties.Settings.Default.Save();
                //TODO: reload mods
            }
        }
    }
}
