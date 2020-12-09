using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
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
        public override void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!HasLoaded)
            {
                var job = EditingMod.GetJob(ModJob.JobHeader.CUSTOMDLC);
                job?.BuildParameterMap(EditingMod);
                CustomDLCJob = EditingMod.GetJob(ModJob.JobHeader.CUSTOMDLC);
                if (CustomDLCJob != null)
                {
                    CustomDLCJob.BuildParameterMap(EditingMod);
                    foreach (var v in CustomDLCJob.CustomDLCFolderMapping)
                    {
                        EditingMod.HumanReadableCustomDLCNames.TryGetValue(v.Value, out var hrName);
                        var cdp = new MDCustomDLCParameter
                        {
                            SourcePath = v.Key,
                            DestDLCName = v.Value,
                            HumanReadableName = hrName
                        };
                        cdp.PropertyChanged += CustomDLCPropertyChanged;
                        CustomDLCParameters.Add(cdp);
                    }
                }

                HasLoaded = true;
            }

            //customdlc_multilists_editor.OnLoaded(newMod);
        }

        public CustomDLCEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddCustomDLCCommand = new GenericCommand(AddCustomDLC, CanAddCustomDLC);
        }

        private bool CanAddCustomDLC() => CustomDLCJob == null || !CustomDLCParameters.Any() ||
                                          (!string.IsNullOrWhiteSpace(CustomDLCParameters.Last().SourcePath)
                                           && (!string.IsNullOrWhiteSpace(CustomDLCParameters.Last().DestDLCName) && CustomDLCParameters.Last().DestDLCName.StartsWith(@"DLC_")));

        private void AddCustomDLC()
        {
            if (CustomDLCJob == null)
            {
                // Generate the job
                CustomDLCJob = new ModJob(ModJob.JobHeader.CUSTOMDLC);
                CustomDLCJob.BuildParameterMap(EditingMod);
                EditingMod.InstallationJobs.Add(CustomDLCJob);
            }

            var job = CustomDLCJob;
            CustomDLCJob = null;
            CustomDLCJob = job; // Rebind??/s

            var cdp = new MDCustomDLCParameter();
            cdp.PropertyChanged += CustomDLCPropertyChanged;
            CustomDLCParameters.Add(cdp); //empty data
        }

        private void CustomDLCPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MDCustomDLCParameter.SourcePath) || e.PropertyName == nameof(MDCustomDLCParameter.DestDLCName))
            {
                AddCustomDLCCommand.RaiseCanExecuteChanged();
            }
        }

        public GenericCommand AddCustomDLCCommand { get; set; }

        public ModJob CustomDLCJob { get; set; }

        public ObservableCollectionExtended<MDCustomDLCParameter> CustomDLCParameters { get; } = new ObservableCollectionExtended<MDCustomDLCParameter>();

        public override void Serialize(IniData ini)
        {
            if (CustomDLCJob != null)
            {
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

                customdlc_multilists_editor.Serialize(ini);

                foreach (var p in CustomDLCJob.ParameterMap)
                {
                    //sourcedirs, destdirs was already serialized, skip them
                    if (!string.IsNullOrWhiteSpace(p.Value) && p.Key == @"incompatiblecustomdlc" || p.Key == @"requiredcustomdlc")
                    {
                        ini[@"CUSTOMDLC"][p.Key] = p.Value;
                    }
                }
            }
        }
    }

    public class MDCustomDLCParameter : INotifyPropertyChanged
    {
        public string HumanReadableName { get; set; } = "";
        public string DestDLCName { get; set; } = "";
        public string SourcePath { get; set; } = "";
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
