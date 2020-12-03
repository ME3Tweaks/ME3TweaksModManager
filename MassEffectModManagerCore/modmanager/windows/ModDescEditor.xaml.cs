using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using IniParser.Model;
using IniParser.Parser;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for ModDescEditor.xaml
    /// </summary>
    public partial class ModDescEditor : Window, INotifyPropertyChanged
    {
        public Mod EditingMod { get; private set; }
        private List<ModdescEditorControlBase> editorControls = new List<ModdescEditorControlBase>();

        public ModDescEditor(Mod selectedMod)
        {
            DataContext = this;
            EditingMod = new Mod(selectedMod.ModDescPath, selectedMod.Game); //RELOAD MOD TO CREATE NEW OBJECT
            InitializeComponent();

            // Tabs
            editorControls.Add(customdlc_alternateFileEditor_control);
            editorControls.Add(customdlc_alternateDlcEditor_control);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        // this should move into the control
        private void SerializeData_Click(object sender, RoutedEventArgs e)
        {
            var ini = new IniData();
            foreach (var control in editorControls)
            {
                control.Serialize(ini);
            }

            ListDialog ld = new ListDialog(new List<string>(new[] { ini.ToString() }), "Moddesc.ini editor TEST OUTPUT", "Copy this data into your moddesc.ini.", this);
            ld.Show();

            //var moddesc = EditingMod.SerializeModdesc();
            ////Mod m = new Mod(moddesc, EditingMod.ModPath, null);

            //Clipboard.SetText(moddesc);
        }

        private void ModDescEditor_OnContentRendered(object? sender, EventArgs e)
        {
            foreach (var control in editorControls)
            {
                control.OnEditingModChanged(EditingMod);
            }
#if !DEBUG
            M3L.ShowDialog(this, M3L.GetString(M3L.string_toolUnderDevelopment), M3L.GetString(M3L.string_underDevelopment), MessageBoxButton.OK, MessageBoxImage.Warning);
#endif
        }
    }
}
