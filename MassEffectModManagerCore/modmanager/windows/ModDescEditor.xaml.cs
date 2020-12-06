using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor;
using MassEffectModManagerCore.ui;
using Serilog;

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
            LoadCommands();
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

        private void LoadCommands()
        {
            CopyModdescIniTextCommand = new GenericCommand(CopyModdesc, CanCopyModdesc);
            SaveModdescToModCommand = new GenericCommand(SaveModdesc, CanSaveModdesc);
        }

        private void CopyModdesc()
        {
            try
            {
                Clipboard.SetText(GeneratedIni);
                StatusMessage = "Copied moddesc.ini contents to clipboard";
                StatusForeground = Settings.DarkTheme ? Brushes.LightGreen : Brushes.DarkGreen;
            }
            catch (Exception e)
            {
                Log.Error($@"Failed to copy moddesc.ini text to clipboard: {e.Message}");
                StatusMessage = $"Failed to copy moddesc.ini text to clipboard: {e.Message}";
                StatusForeground = Brushes.Red;
            }
        }

        public bool LastSerializationSuccessful { get; private set; }

        private void SaveModdesc()
        {
            // Make a backup of the existing file
            try
            {
                // See if it's different
                var existinText = File.ReadAllText(EditingMod.ModDescPath);
                if (existinText.Equals(GeneratedIni))
                {
                    StatusMessage = "Existing moddesc.ini is same as generated one";
                    return;
                }

                var buDest = Path.Combine(EditingMod.ModPath, $@"backup_moddesc_{DateTime.Now:yy-MM-dd h-mm-ss}.ini");
                File.Copy(EditingMod.ModDescPath, buDest, true);
                File.WriteAllText(EditingMod.ModDescPath, GeneratedIni);
                StatusMessage = $"moddesc.ini has been updated for {EditingMod.ModName}. Reload the library for changes to take effect.";
            }
            catch (Exception e)
            {
                StatusMessage = $"Could not save moddesc.ini: {e.Message}";
            }
        }

        private bool CanSaveModdesc() => LastSerializationSuccessful;

        private bool CanCopyModdesc() => !string.IsNullOrWhiteSpace(GeneratedIni);

        public GenericCommand SaveModdescToModCommand { get; set; }

        public GenericCommand CopyModdescIniTextCommand { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }



        private string SerializeData()
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

            return ini.ToString();
        }

        // this should move into the control
        private void SerializeData_Click(object sender, RoutedEventArgs e)
        {
            var ini = SerializeData();
            // Load the moddesc.ini as if it was in the library at the original mod folder location
            var m = new Mod(ini, EditingMod.ModPath, null);

            StatusMessage = m.LoadFailedReason;
            if (StatusMessage == null)
            {
                StatusForeground = Settings.DarkTheme ? Brushes.LightGreen : Brushes.DarkGreen;
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

                editor_tabcontrol.SelectedItem = results_tab;
            }

            // Set generated page
            GeneratedIni = ini;
            LastSerializationSuccessful = m.ValidMod;
            //var moddesc = EditingMod.SerializeModdesc();
            ////Mod m = new Mod(moddesc, EditingMod.ModPath, null);

            //Clipboard.SetText(moddesc);
        }

        public SolidColorBrush StatusForeground { get; set; }

        public string StatusMessage { get; set; }

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
