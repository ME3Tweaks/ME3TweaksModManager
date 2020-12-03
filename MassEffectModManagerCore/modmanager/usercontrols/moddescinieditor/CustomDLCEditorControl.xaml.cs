using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.modmanager.objects.mod.editor;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for CustomDLCEditorControl.xaml
    /// </summary>
    public partial class CustomDLCEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public override void OnEditingModChanged(Mod newMod)
        {
            base.OnEditingModChanged(newMod);
            CustomDLCJob = EditingMod.GetJob(ModJob.JobHeader.CUSTOMDLC);
            CustomDLCParameters.ClearEx();

            if (CustomDLCJob != null)
            {
                foreach (var v in CustomDLCJob.CustomDLCFolderMapping)
                {
                    EditingMod.HumanReadableCustomDLCNames.TryGetValue(v.Value, out var hrName);
                    CustomDLCParameters.Add(new MDCustomDLCParameter
                    {
                        SourcePath = v.Key,
                        DestDLCName = v.Value,
                        HumanReadableName = hrName
                    });
                }
            }
        }

        public CustomDLCEditorControl()
        {
            InitializeComponent();
        }

        public ModJob CustomDLCJob { get; set; }

        public ObservableCollectionExtended<MDCustomDLCParameter> CustomDLCParameters { get; } = new ObservableCollectionExtended<MDCustomDLCParameter>();

        public event PropertyChangedEventHandler PropertyChanged;
        public override void Serialize(IniData ini)
        {
            // Pass 1: sourcedirs destdirs
            var srcDirs = CustomDLCParameters.ToDictionary(x => x.SourcePath, x => x.DestDLCName);

            ini[@"CUSTOMDLC"][@"sourcedirs"] = string.Join(';', srcDirs.Keys);
            ini[@"CUSTOMDLC"][@"destdirs"] = string.Join(';', srcDirs.Values);

            foreach (var v in CustomDLCParameters.Where(x => !string.IsNullOrWhiteSpace(x.HumanReadableName)))
            {
                ini[@"CUSTOMDLC"][v.DestDLCName] = v.HumanReadableName;
            }
        }
    }

    public class MDCustomDLCParameter
    {
        public string HumanReadableName { get; set; }
        public string DestDLCName { get; set; }
        public string SourcePath { get; set; }
    }
}
