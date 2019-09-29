using System.Windows;

namespace MassEffectModManagerCore.modmanager.windows
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
            //CommonOpenFileDialog m = new CommonOpenFileDialog
            //{
            //    IsFolderPicker = true,
            //    EnsurePathExists = true,
            //    Title = "Select mod library folder"
            //};
            //if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
            //{
            //    Properties.Settings.Default.ModLibraryPath = m.FileName;
            //    //TODO: reload mods
            //}
        }

        private void OptionsWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Save();
        }
    }
}
