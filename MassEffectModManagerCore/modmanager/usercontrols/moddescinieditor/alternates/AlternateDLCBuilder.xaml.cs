using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using IniParser;
using IniParser.Model;
using IniParser.Parser;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor.alternates
{
    /// <summary>
    /// Interaction logic for AlternateDLCBuilder.xaml
    /// </summary>
    public partial class AlternateDLCBuilder : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public ModJob CustomDLCJob { get; set; }
        /// <summary>
        /// List of editing Alternate DLCs. These have to be extracted out of the job as they are not bindable in job
        /// </summary>
        public ObservableCollectionExtended<AlternateDLC> AlternateDLCs { get; } = new ObservableCollectionExtended<AlternateDLC>();

        public override void OnEditingModChanged(Mod newMod)
        {
            base.OnEditingModChanged(newMod);
            CustomDLCJob = EditingMod?.GetJob(ModJob.JobHeader.CUSTOMDLC);
            if (CustomDLCJob != null)
            {
                AlternateDLCs.ReplaceAll(CustomDLCJob.AlternateDLCs);
            }
            else
            {
                AlternateDLCs.ClearEx();
            }
        }

        public override void Serialize(IniData ini)
        {
            if (CustomDLCJob != null && AlternateDLCs.Any())
            {
                string outStr = "(";
                bool isFirst = true;
                foreach (var adlc in AlternateDLCs)
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        outStr += @",";
                    }
                    outStr += StringStructParser.BuildCommaSeparatedSplitValueList(adlc.ParameterMap.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key, x => x.Value));
                }

                outStr += ")";
                ini[@"CUSTOMDLC"][@"altdlc"] = outStr;
            }
        }

        public AlternateDLCBuilder()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddAlternateDLCCommand = new GenericCommand(AddAlternateDLC, CanAddAlternateDLC);
        }

        private bool CanAddAlternateDLC() => EditingMod != null && EditingMod.GetJob(ModJob.JobHeader.CUSTOMDLC) != null;

        private void AddAlternateDLC()
        {

        }


        public GenericCommand AddAlternateDLCCommand { get; set; }

        //public ObservableCollectionExtended<AlternateDLC> AlternateDLCs { get; } = new ObservableCollectionExtended<AlternateDLC>();
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
