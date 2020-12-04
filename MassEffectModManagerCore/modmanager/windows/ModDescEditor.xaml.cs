using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using IniParser.Model;
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
            editorControls.Add(metadataEditor_control);
            editorControls.Add(basegame_editor_control);
            editorControls.Add(officialdlc_editor_control);
            editorControls.Add(customdlcEditor_control);
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

            Debug.WriteLine(ini.ToString());
            ListDialog ld = new ListDialog(new List<string>(new[] { ini.ToString() }), "Moddesc.ini editor TEST OUTPUT", "Copy this data into your moddesc.ini.", this);
            ld.Show();

            // Load the moddesc.ini as if it was in the library at the original mod folder location
            var m = new Mod(ini.ToString(), EditingMod.ModPath, null);
            if (m.ValidMod)
            {
                // wow
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.VisibleFilteredMods.Add(m);
                    mw.SelectedMod = m;
                }
            }
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

        private void OpenModdescDocumenation_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenWebpage(@"https://github.com/ME3Tweaks/ME3TweaksModManager/tree/master/documentation");
        }
    }
}
