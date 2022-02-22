using System.ComponentModel;
using System.Linq;
using System.Windows;
using IniParser.Model;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor.alternates
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
        public ObservableCollectionExtended<AlternateDLC> Alternates { get; } = new ObservableCollectionExtended<AlternateDLC>();

        public override void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!HasLoaded)
            {
                CustomDLCJob = EditingMod?.GetJob(ModJob.JobHeader.CUSTOMDLC);
                if (CustomDLCJob != null)
                {
                    Alternates.ReplaceAll(CustomDLCJob.AlternateDLCs);
                    foreach (var a in Alternates)
                    {
                        a.BuildParameterMap(EditingMod);
                    }
                }
                else
                {
                    Alternates.ClearEx();
                }

                HasLoaded = true;
            }
        }

        public override void Serialize(IniData ini)
        {
            if (CustomDLCJob != null && Alternates.Any())
            {
                string outStr = "(";
                bool isFirst = true;
                foreach (var adlc in Alternates)
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
            Alternates.Add(new AlternateDLC($@"Alternate DLC {Alternates.Count + 1}")); // As this is noun in mod manager terminology it shouldn't be localized, i think
        }

        public GenericCommand AddAlternateDLCCommand { get; set; }
    }
}
