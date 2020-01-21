using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Linq;
namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for MixinManager.xaml
    /// </summary>
    public partial class MixinManager : MMBusyPanelBase
    {
        public ObservableCollectionExtended<Mixin> AvailableOfficialMixins { get; set; } = new ObservableCollectionExtended<Mixin>();
        public Mixin SelectedMixin { get; set; }

        public MixinManager()
        {
            DataContext = this;
            MixinHandler.LoadME3TweaksPackage();
            AvailableOfficialMixins.ReplaceAll(MixinHandler.ME3TweaksPackageMixins.OrderBy(x => x.PatchName));
            ResetMixinsUIState();
            LoadCommands();
            InitializeComponent();
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
