using System;
using System.Collections.Generic;
using System.IO.Packaging;
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
using IniParser.Model;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.editor;
using ME3TweaksModManager.modmanager.objects.mod.headmorph;
using ME3TweaksModManager.modmanager.usercontrols.moddescinieditor.alternates;
using ME3TweaksModManager.modmanager.windows;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Moddesc.ini editor control for [TEXTUREMODS] task header
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class HeadmorphsEditorControl : ModdescEditorControlBase
    {
        #region Commands
        public ICommand AddHeadmorphJobCommand { get; set; }
        public ICommand RemoveHeadmorphsJobCommand { get; set; }
        public ICommand AddHeadmorphCommand { get; set; }
        public ICommand DeleteEntryCommand { get; set; }
        public ICommand MoveEntryDownCommand { get; set; }
        public ICommand MoveEntryUpCommand { get; set; }
        public ICommand AutopopulateFilesCommand { get; set; }
        #endregion


        /// <summary>
        /// ModJob this editor control is associated iwth
        /// </summary>
        public ModJob HeadmorphsJob { get; set; }

        /// <summary>
        /// List of Headmorph objects that can be edited. These are not updated directly into the job; they are bound when the moddesc is reserialized.
        /// </summary>
        public ObservableCollectionExtended<M3Headmorph> Headmorphs { get; set; } = new();

        public HeadmorphsEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddHeadmorphJobCommand = new GenericCommand(AddHeadmorphJob, () => HeadmorphsJob == null);
            RemoveHeadmorphsJobCommand = new GenericCommand(RemoveHeadmorphJob, () => HeadmorphsJob != null);
            AddHeadmorphCommand = new GenericCommand(AddHeadmorph, () => HeadmorphsJob != null);
            DeleteEntryCommand = new RelayCommand(RemoveEntry);
            MoveEntryDownCommand = new RelayCommand(MoveEntryDown, CanMoveEntryDown);
            MoveEntryUpCommand = new RelayCommand(MoveEntryUp, CanMoveEntryUp);
            AutopopulateFilesCommand = new GenericCommand(AutopopulateFiles, () => HeadmorphsJob != null);
        }

        private void MoveEntryUp(object obj)
        {
            if (obj is M3Headmorph option)
            {
                var startingIndex = Headmorphs.IndexOf(option);
                Headmorphs.RemoveAt(startingIndex); // e.g. Remove from position 3
                Headmorphs.Insert(startingIndex - 1, option);
            }
        }

        private void MoveEntryDown(object obj)
        {
            if (obj is M3Headmorph option)
            {
                var startingIndex = Headmorphs.IndexOf(option);
                Headmorphs.RemoveAt(startingIndex); // e.g. Remove from position 3
                Headmorphs.Insert(startingIndex + 1, option);
            }
        }

        private bool CanMoveEntryDown(object obj)
        {
            if (obj is M3Headmorph option)
            {
                return Headmorphs.IndexOf(option) < Headmorphs.Count - 1; // -1 for 0 indexing. Less than covers the next -1.
            }
            return false;
        }

        private bool CanMoveEntryUp(object obj)
        {
            if (obj is M3Headmorph option)
            {
                return Headmorphs.IndexOf(option) > 0;
            }
            return false;
        }

        private void RemoveEntry(object obj)
        {
            if (obj is M3Headmorph option)
            {
                var removeRef = M3L.ShowDialog(Window.GetWindow(this), $"Remove headmorph reference '{option.Title}'?", "Confirm removal", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (removeRef == MessageBoxResult.Yes)
                {
                    Headmorphs.Remove(option);
                }
            }
        }

        private void AddHeadmorph()
        {
            var item = new M3Headmorph();
            item.BuildParameterMap(EditingMod); // Build the parameter map for the editor
            Headmorphs.Add(item);
        }

        private void AddHeadmorphJob()
        {
            HeadmorphsJob = new ModJob(ModJob.JobHeader.TEXTUREMODS, EditingMod);
            EditingMod.InstallationJobs.Add(HeadmorphsJob);
        }

        private void RemoveHeadmorphJob()
        {
            Headmorphs.ClearEx();
            HeadmorphsJob = null;
        }

        public override void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (HasLoaded) return;
            if (EditingMod.Game.IsLEGame()) // Only supported by LE
            {
                HeadmorphsJob = EditingMod?.GetJob(ModJob.JobHeader.HEADMORPHS);
                if (HeadmorphsJob != null)
                {
                    Headmorphs.ReplaceAll(HeadmorphsJob.HeadMorphFiles); // new bound list
                    foreach (var hm in Headmorphs)
                    {
                        hm.BuildParameterMap(EditingMod);
                    }
                }
                else
                {
                    Headmorphs.ClearEx();
                }
            }

            HasLoaded = true;
        }

        public override void Serialize(IniData ini)
        {
            // Never serialize this for non-LE as a safeguard
            if (EditingMod.Game.IsLEGame() && HeadmorphsJob != null && Headmorphs.Any())
            {
                string outStr = @"(";
                bool isFirst = true;
                foreach (var hm in Headmorphs)
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        outStr += @",";
                    }
                    outStr += StringStructParser.BuildCommaSeparatedSplitValueList(hm.ParameterMap.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key, x => x.Value));
                }

                outStr += @")";
                ini[ModJob.JobHeader.HEADMORPHS.ToString()][@"files"] = outStr;
            }
        }

        private void AutopopulateFiles()
        {
            var modHeadmorphsFolder = Path.Combine(EditingMod.ModPath, Mod.HEADMORPHS_FOLDER_NAME);
            if (Directory.Exists(modHeadmorphsFolder))
            {
                var morphFiles = Directory.GetFiles(modHeadmorphsFolder, @"*", SearchOption.TopDirectoryOnly).Where(x => IsHeadmorphFileType(EditingMod.Game, x));
                foreach (var morphFile in morphFiles)
                {
                    M3Headmorph mm = new M3Headmorph() { FileName = Path.GetFileName(morphFile), Title = Path.GetFileNameWithoutExtension(morphFile) };
                    mm.BuildParameterMap(EditingMod);
                    Headmorphs.Add(mm);
                }
            }
        }

        private bool IsHeadmorphFileType(MEGame game, string filePath)
        {
            var extension = Path.GetExtension(filePath);
            if (game.IsGame2() && extension == @".me2headmorph") return true; // Gibbed
            if (game.IsGame3() && extension == @".me3headmorph") return true; // Gibbed
            if (extension == @".ron") return true; // Trilogy Save Editor
            return false;
        }
    }
}
