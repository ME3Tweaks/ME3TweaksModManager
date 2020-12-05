using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
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

        /// <summary>
        /// The generated ini from the last serialization
        /// </summary>
        public string GeneratedIni { get; set; }
        public ModDescEditor(Mod selectedMod)
        {
            EditingMod = new Mod(selectedMod.ModDescPath, selectedMod.Game);
            InitializeComponent();

            // Tabs that can edit content
            editorControls.Add(metadataEditor_control);
            editorControls.Add(basegame_editor_control);
            editorControls.Add(officialdlc_editor_control);
            editorControls.Add(customdlcEditor_control);
            editorControls.Add(customdlc_alternateFileEditor_control);
            editorControls.Add(customdlc_alternateDlcEditor_control);

            editorControls.Add(me1config_editor_control);
            editorControls.Add(balancechanges_editor_control);
            editorControls.Add(localization_editor_control);
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
                if (control is LocalizationTaskEditorControl)
                {
                    if (IsLocalizationMod)
                    {
                        control.Serialize(ini);
                        continue;
                    }
                }
                else if (control is MetadataEditorControl)
                {
                    control.Serialize(ini);
                    continue;
                }

                if (!IsLocalizationMod)
                {
                    control.Serialize(ini);
                }

            }

            //Debug.WriteLine(ini.ToString());
            //ListDialog ld = new ListDialog(new List<string>(new[] { ini.ToString() }), "Moddesc.ini editor TEST OUTPUT", "Copy this data into your moddesc.ini.", this);
            //ld.Show();

            // Load the moddesc.ini as if it was in the library at the original mod folder location
            var m = new Mod(ini.ToString(), EditingMod.ModPath, null);

            StatusMessage = m.LoadFailedReason;
            if (StatusMessage == null)
            {
                if (Settings.DarkTheme)
                {
                    StatusForeground = Brushes.LightGreen;
                }
                else
                {
                    StatusForeground = Brushes.DarkGreen;
                }

            }
            else
            {
                StatusForeground = Brushes.Red;
            }

            if (m.ValidMod)
            {
                // wow
                StatusMessage = "Mod loaded successfully";
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.VisibleFilteredMods.Add(m);
                    mw.SelectedMod = m;
                }
            }

            GeneratedIni = ini.ToString();
            //var moddesc = EditingMod.SerializeModdesc();
            ////Mod m = new Mod(moddesc, EditingMod.ModPath, null);

            //Clipboard.SetText(moddesc);
        }

        public SolidColorBrush StatusForeground { get; set; }

        public string StatusMessage { get; set; }

        private void ModDescEditor_OnContentRendered(object? sender, EventArgs e)
        {

            //foreach (var control in editorControls)
            //{
            //    control.OnLoaded();
            //}
#if !DEBUG
            M3L.ShowDialog(this, M3L.GetString(M3L.string_toolUnderDevelopment), M3L.GetString(M3L.string_underDevelopment), MessageBoxButton.OK, MessageBoxImage.Warning);
#endif
        }

        private void OpenModdescDocumenation_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenWebpage(@"https://github.com/ME3Tweaks/ME3TweaksModManager/tree/master/documentation");
        }

        /// <summary>
        /// Tells the editor that the mod is being converted to localization mod
        /// and that other fields should be dumped
        /// </summary>
        public void ConvertModToLocalizationMod()
        {
            IsLocalizationMod = true;
        }

        public bool IsLocalizationMod { get; set; }
    }
}
