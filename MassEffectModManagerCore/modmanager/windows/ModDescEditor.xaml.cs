using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MassEffectModManagerCore.modmanager.localizations;
using Microsoft.WindowsAPICodePack.Shell.Interop;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for ModDescEditor.xaml
    /// </summary>
    public partial class ModDescEditor : Window, INotifyPropertyChanged
    {
        public objects.mod.Mod EditingMod { get; private set; }

        public ModDescEditor(objects.mod.Mod selectedMod)
        {
            DataContext = this;
            EditingMod = new objects.mod.Mod(selectedMod.ModDescPath, selectedMod.Game); //RELOAD MOD TO CREATE NEW OBJECT
            InitializeComponent();
            metadataEditor_control.EditingMod = EditingMod;
            customdlcEditor_control.EditingMod = EditingMod;
            customdlc_alternateDlcEditor_control.EditingMod = EditingMod;
            customdlc_alternateFileEditor_control.EditingMod = EditingMod;
            customdlc_alternateFileEditor_control.Job = EditingMod.GetJob(ModJob.JobHeader.CUSTOMDLC);

        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        // this should move into the control
        private void SerializeData_Click(object sender, RoutedEventArgs e)
        {
            var moddesc = EditingMod.SerializeModdesc();
            objects.mod.Mod m = new objects.mod.Mod(moddesc, EditingMod.ModPath, null);

            Clipboard.SetText(moddesc);
        }

        private void ModDescEditor_OnContentRendered(object? sender, EventArgs e)
        {
            M3L.ShowDialog(this, M3L.GetString(M3L.string_toolUnderDevelopment), M3L.GetString(M3L.string_underDevelopment), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
