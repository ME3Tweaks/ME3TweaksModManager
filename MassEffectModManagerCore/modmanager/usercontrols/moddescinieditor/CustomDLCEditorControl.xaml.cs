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
            customdlc_multilists_editor.OnEditingModChanged(newMod);
        }

        public CustomDLCEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddCustomDLCCommand = new GenericCommand(AddCustomDLC);
        }

        private void AddCustomDLC()
        {
            if (CustomDLCJob == null)
            {
                // Generate the job
                CustomDLCJob = new ModJob(ModJob.JobHeader.CUSTOMDLC);
                EditingMod.InstallationJobs.Add(CustomDLCJob);
            }

            CustomDLCParameters.Add(new MDCustomDLCParameter()); //empty data
        }

        public GenericCommand AddCustomDLCCommand { get; set; }

        public ModJob CustomDLCJob { get; set; }

        public ObservableCollectionExtended<MDCustomDLCParameter> CustomDLCParameters { get; } = new ObservableCollectionExtended<MDCustomDLCParameter>();

        public event PropertyChangedEventHandler PropertyChanged;
        public override void Serialize(IniData ini)
        {
            if (CustomDLCJob != null)
            {
                // Pass 1: sourcedirs destdirs
                var srcDirs = CustomDLCParameters.ToDictionary(x => x.SourcePath, x => x.DestDLCName);

                if (srcDirs.Any())
                {
                    ini[@"CUSTOMDLC"][@"sourcedirs"] = string.Join(';', srcDirs.Keys);
                    ini[@"CUSTOMDLC"][@"destdirs"] = string.Join(';', srcDirs.Values);

                    foreach (var v in CustomDLCParameters.Where(x => !string.IsNullOrWhiteSpace(x.HumanReadableName)))
                    {
                        ini[@"CUSTOMDLC"][v.DestDLCName] = v.HumanReadableName;
                    }
                }
            }
        }
    }

    public class MDCustomDLCParameter
    {
        public string HumanReadableName { get; set; } = "";
        public string DestDLCName { get; set; } = "";
        public string SourcePath { get; set; } = "";
    }
}
