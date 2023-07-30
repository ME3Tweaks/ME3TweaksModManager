using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Dark.Net;
using IniParser.Model;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.exceptions;
using ME3TweaksModManager.modmanager.loaders;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.usercontrols.moddescinieditor;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Crashes;

namespace ME3TweaksModManager.modmanager.windows
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
            this.ApplyDefaultDarkNetWindowStyle();

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
            editorControls.Add(game1tlkmerge_editor_control);
            editorControls.Add(lelauncher_editor_control);
            editorControls.Add(textures_editor_control);
            editorControls.Add(headmorphs_editor_control);
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
                StatusMessage = M3L.GetString(M3L.string_copiedModdesciniContentsToClipboard);
                StatusForeground = Settings.DarkTheme ? Brushes.LightGreen : Brushes.DarkGreen;
            }
            catch (Exception e)
            {
                M3Log.Error($@"Failed to copy moddesc.ini text to clipboard: {e.Message}");
                StatusMessage = M3L.GetString(M3L.string_interp_failedToCopyModdescini, e.Message);
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
                    StatusMessage = M3L.GetString(M3L.string_moddescIniNotChanged);
                    return;
                }

                var buDest = Path.Combine(EditingMod.ModPath, $@"backup_moddesc_{DateTime.Now:yy-MM-dd h-mm-ss}.ini");
                File.Copy(EditingMod.ModDescPath, buDest, true);
                File.WriteAllText(EditingMod.ModDescPath, GeneratedIni);
                StatusMessage = M3L.GetString(M3L.string_interp_savedModdsecini, EditingMod.ModName);
            }
            catch (Exception e)
            {
                StatusMessage = M3L.GetString(M3L.string_interp_couldNotSaveModdescini, e.Message);
            }
        }

        private bool CanSaveModdesc() => LastSerializationSuccessful;

        private bool CanCopyModdesc() => !string.IsNullOrWhiteSpace(GeneratedIni);

        public GenericCommand SaveModdescToModCommand { get; set; }

        public GenericCommand CopyModdescIniTextCommand { get; set; }

        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }



        private string SerializeData()
        {
            string error = null;
            var ini = new IniData();
            foreach (var control in editorControls)
            {
                try
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
                    else if (control is TexturesEditorControl)
                    {
                        control.Serialize(ini);
                        continue;
                    }

                    if (!IsLocalizationMod)
                    {
                        control.Serialize(ini);
                    }
                }
                catch (ModDescSerializerException e)
                {
                    // Specially thrown and handled
                    M3Log.Exception(e, @"A handled error occurred serializing moddesc");
                    error = e.FlattenException();
                    break;
                }
                catch (Exception e)
                {
                    M3Log.Exception(e, @"Error occurred serializing moddesc");
                    Crashes.TrackError(e);
                    error = e.FlattenException();
                    break;
                }
            }

            return error ?? ini.ToString();
        }

        // this should move into the control
        private void SerializeData_Click(object sender, RoutedEventArgs e)
        {
            var ini = SerializeData();

#if DEBUG
            //Uncomment this to debug serializer
            // Clipboard.SetText(ini.ToString());
#endif
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
                StatusMessage = M3L.GetString(M3L.string_modLoadedSuccessfully);
                //if (Application.Current.MainWindow is MainWindow mw)
                //{
                // This should probably be improved.
                M3LoadedMods.Instance.VisibleFilteredMods.Add(m);
                M3LoadedMods.Instance.SelectModCallback?.Invoke(m);
                //}

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
            M3Utilities.OpenWebpage(M3OnlineContent.MODDESC_DOCUMENTATION_LINK);
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
