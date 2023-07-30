using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.VisualStyles;
using Dark.Net;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.headmorph;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Dialog for selecting a headmorph file from a mod
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class HeadmorphSelectorDialog : Window
    {
        /// <summary>
        /// The selected headmorph
        /// </summary>
        public M3Headmorph SelectedHeadmorph { get; set; }
        public ObservableCollectionExtended<M3Headmorph> AvailableHeadmorphs { get; } = new();

        public HeadmorphSelectorDialog(Window owner, Mod mod)
        {
            Owner = owner;
            AvailableHeadmorphs.ReplaceAll(mod.GetJob(ModJob.JobHeader.HEADMORPHS).HeadMorphFiles);
            LoadCommands();
            InitializeComponent();
            this.ApplyDarkNetWindowStyle();
        }

        public GenericCommand SelectHeadmorphCommand { get; set; }
        public GenericCommand CancelCommand { get; set; }

        private void LoadCommands()
        {
            SelectHeadmorphCommand = new GenericCommand(SelectHeadmorph, CanSelectheadmorph);
            CancelCommand = new GenericCommand(Cancel);
        }

        private void Cancel()
        {
            DialogResult = false;
            Close();
        }

        private bool CanSelectheadmorph() => SelectedHeadmorph != null;

        private void SelectHeadmorph()
        {
            DialogResult = true;
            Close();
        }

        private void HeadmorphList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (CanSelectheadmorph()) SelectHeadmorph();
        }
    }
}
