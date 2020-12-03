using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using IniParser.Model;
using MassEffectModManagerCore.modmanager.objects.mod;
using ME3ExplorerCore.Misc;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for OfficialDLCEditorControl.xaml
    /// </summary>
    public partial class OfficialDLCEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public OfficialDLCEditorControl()
        {
            InitializeComponent();
        }

        public ObservableCollectionExtended<ModJob> OfficialDLCJobs { get; } = new ObservableCollectionExtended<ModJob>();

        public override void OnEditingModChanged(Mod newMod)
        {
            base.OnEditingModChanged(newMod);
            OfficialDLCJobs.ReplaceAll(newMod.InstallationJobs.Where(x => x.IsOfficialDLCJob(EditingMod.Game)));
        }

        public override void Serialize(IniData ini)
        {
            foreach (var odlc in OfficialDLCJobs)
            {
                // Serialize
            }
        }
    }
}
