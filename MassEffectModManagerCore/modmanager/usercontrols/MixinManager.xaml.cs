using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Linq;
using MassEffectModManagerCore.GameDirectories;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for MixinManager.xaml
    /// </summary>
    public partial class MixinManager : MMBusyPanelBase
    {
        public ObservableCollectionExtended<Mixin> AvailableOfficialMixins { get; set; } = new ObservableCollectionExtended<Mixin>();
        public Mixin SelectedMixin { get; set; }
        public string BottomLeftMessage { get; set; } = "Select Mixins to compile";
        public bool AtLeastOneMixinSelected => AvailableOfficialMixins.Any(x => x.UISelectedForUse);
        public MixinManager()
        {
            DataContext = this;
            MixinHandler.LoadME3TweaksPackage();
            AvailableOfficialMixins.ReplaceAll(MixinHandler.ME3TweaksPackageMixins.OrderBy(x => x.PatchName));

            var backupPath = Utilities.GetGameBackupPath(Mod.MEGame.ME3);
            if (backupPath != null)
            {
                var dlcPath = MEDirectories.DLCPath(backupPath, Mod.MEGame.ME3);
                var headerTranslation = ModJob.GetHeadersToDLCNamesMap(Mod.MEGame.ME3);
                foreach (var mixin in AvailableOfficialMixins)
                {
                    mixin.UIStatusChanging += MixinUIStatusChanging;
                    if (mixin.TargetModule == ModJob.JobHeader.TESTPATCH)
                    {
                        string biogame = MEDirectories.BioGamePath(backupPath);
                        var sfar = Path.Combine(biogame, "Patches", "PCConsole", "Patch_001.sfar");
                        if (File.Exists(sfar))
                        {
                            mixin.CanBeUsed = true;
                        }
                    }
                    else if (mixin.TargetModule != ModJob.JobHeader.BASEGAME)
                    {
                        //DLC
                        var resolvedPath = Path.Combine(dlcPath, headerTranslation[mixin.TargetModule]);
                        if (Directory.Exists(resolvedPath))
                        {
                            mixin.CanBeUsed = true;
                        }
                    }
                    else
                    {
                        //BASEGAME
                        mixin.CanBeUsed = true;
                    }
                }
            }
            else
            {
                BottomLeftMessage = "No game backup of ME3 is available. Mixins cannot be used without a backup.";
            }

            ResetMixinsUIState();
            LoadCommands();
            InitializeComponent();
        }

        private void MixinUIStatusChanging(object sender, EventArgs e)
        {
            TriggerPropertyChangedFor(nameof(AtLeastOneMixinSelected));
        }

        private void ResetMixinsUIState()
        {
            foreach (var m in AvailableOfficialMixins)
            {
                m.UISelectedForUse = false;
            }
        }

        public ICommand CloseCommand { get; set; }
        public ICommand ToggleSelectedMixinCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
            ToggleSelectedMixinCommand = new GenericCommand(ToggleSelectedMixin, MixinIsSelected);
        }

        private void ToggleSelectedMixin()
        {
            SelectedMixin.UISelectedForUse = !SelectedMixin.UISelectedForUse;
        }

        private bool MixinIsSelected() => SelectedMixin != null;
        private bool CanClosePanel() => true;

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            // throw new NotImplementedException();
        }

        public override void OnPanelVisible()
        {
            // throw new NotImplementedException();
        }

        public void OnSelectedMixinChanged()
        {

        }
        //private void MixinList_ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (e.AddedItems.Count > 0)
        //    {
        //        SelectedMixin = (Mixin)e.AddedItems[0];
        //    }
        //    else
        //    {
        //        SelectedMixin = null;
        //    }
        //}
    }
}
