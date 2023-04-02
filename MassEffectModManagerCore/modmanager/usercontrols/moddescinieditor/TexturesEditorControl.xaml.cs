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
using ME3TweaksModManager.modmanager.usercontrols.moddescinieditor.alternates;
using ME3TweaksModManager.modmanager.windows;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Moddesc.ini editor control for [TEXTUREMODS] task header
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class TexturesEditorControl : ModdescEditorControlBase
    {
        #region Commands
        public ICommand AddTextureModsJobCommand { get; set; }
        public ICommand RemoveTextureModsJobCommand { get; set; }
        public ICommand AddTextureModCommand { get; set; }
        public ICommand DeleteEntryCommand { get; set; }
        public ICommand MoveEntryDownCommand { get; set; }
        public ICommand MoveEntryUpCommand { get; set; }
        public ICommand AutopopulateFilesCommand { get; set; }
        #endregion


        /// <summary>
        /// ModJob this editor control is associated iwth
        /// </summary>
        public ModJob TextureModsJob { get; set; }

        /// <summary>
        /// List of texture mod objects that can be edited. These are not updated directly into the job; they are bound when the moddesc is reserialized.
        /// </summary>
        public ObservableCollectionExtended<M3MEMMod> TextureMods { get; set; } = new();

        public TexturesEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddTextureModsJobCommand = new GenericCommand(AddTextureModsJob, () => TextureModsJob == null);
            RemoveTextureModsJobCommand = new GenericCommand(RemoveTextureModsJob, () => TextureModsJob != null);
            AddTextureModCommand = new GenericCommand(AddTextureMod, () => TextureModsJob != null);
            DeleteEntryCommand = new RelayCommand(RemoveEntry);
            MoveEntryDownCommand = new RelayCommand(MoveEntryDown, CanMoveEntryDown);
            MoveEntryUpCommand = new RelayCommand(MoveEntryUp, CanMoveEntryUp);
            AutopopulateFilesCommand = new GenericCommand(AutopopulateFiles, () => TextureModsJob != null);
        }

        private void MoveEntryUp(object obj)
        {
            if (obj is M3MEMMod option)
            {
                var startingIndex = TextureMods.IndexOf(option);
                TextureMods.RemoveAt(startingIndex); // e.g. Remove from position 3
                TextureMods.Insert(startingIndex - 1, option);
            }
        }

        private void MoveEntryDown(object obj)
        {
            if (obj is M3MEMMod option)
            {
                var startingIndex = TextureMods.IndexOf(option);
                TextureMods.RemoveAt(startingIndex); // e.g. Remove from position 3
                TextureMods.Insert(startingIndex + 1, option);
            }
        }

        private bool CanMoveEntryDown(object obj)
        {
            if (obj is M3MEMMod option)
            {
                return TextureMods.IndexOf(option) < TextureMods.Count - 1; // -1 for 0 indexing. Less than covers the next -1.
            }
            return false;
        }

        private bool CanMoveEntryUp(object obj)
        {
            if (obj is M3MEMMod option)
            {
                return TextureMods.IndexOf(option) > 0;
            }
            return false;
        }

        private void RemoveEntry(object obj)
        {
            if (obj is M3MEMMod option)
            {
                var removeRef = M3L.ShowDialog(Window.GetWindow(this), $"Remove texture mod reference '{option.Title}'?", "Confirm removal", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (removeRef == MessageBoxResult.Yes)
                {
                    TextureMods.Remove(option);
                }
            }
        }

        private void AddTextureMod()
        {
            var item = new M3MEMMod() { ModdescMod = EditingMod };
            item.BuildParameterMap(EditingMod); // Build the parameter map for the editor
            TextureMods.Add(item);
        }

        private void AddTextureModsJob()
        {
            TextureModsJob = new ModJob(ModJob.JobHeader.TEXTUREMODS, EditingMod);
            EditingMod.InstallationJobs.Add(TextureModsJob);
        }

        private void RemoveTextureModsJob()
        {
            TextureMods.ClearEx();
            TextureModsJob = null;
        }

        public override void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (HasLoaded) return;
            if (EditingMod.Game.IsLEGame()) // Only supported by LE
            {
                TextureModsJob = EditingMod?.GetJob(ModJob.JobHeader.TEXTUREMODS);
                if (TextureModsJob != null)
                {
                    TextureMods.ReplaceAll(TextureModsJob.TextureModReferences); // new bound list
                    foreach (var tex in TextureMods)
                    {
                        tex.BuildParameterMap(EditingMod);
                    }
                }
                else
                {
                    TextureMods.ClearEx();
                }
            }

            HasLoaded = true;
        }

        public override void Serialize(IniData ini)
        {
            // Never serialize this for non-LE as a safeguard
            if (EditingMod.Game.IsLEGame() && TextureModsJob != null && TextureMods.Any())
            {
                string outStr = @"(";
                bool isFirst = true;
                foreach (var m3mm in TextureMods)
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        outStr += @",";
                    }
                    outStr += StringStructParser.BuildCommaSeparatedSplitValueList(m3mm.ParameterMap.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key, x => x.Value));
                }

                outStr += @")";
                ini[ModJob.JobHeader.TEXTUREMODS.ToString()][@"files"] = outStr;
            }
        }

        private void AutopopulateFiles()
        {
            var modTexturesFolder = Path.Combine(EditingMod.ModPath, Mod.TEXTUREMOD_FOLDER_NAME);
            if (Directory.Exists(modTexturesFolder))
            {
                var memFiles = Directory.GetFiles(modTexturesFolder, @"*.mem", SearchOption.TopDirectoryOnly);
                foreach (var memFile in memFiles)
                {
                    M3MEMMod mm = M3MEMMod.CreateForEditor(EditingMod, memFile);
                    mm.BuildParameterMap(EditingMod);
                    TextureMods.Add(mm);
                }
            }
        }


    }
}
