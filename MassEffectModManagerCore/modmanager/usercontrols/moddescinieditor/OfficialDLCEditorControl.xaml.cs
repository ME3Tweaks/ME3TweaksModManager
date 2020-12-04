using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor.alternates;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for OfficialDLCEditorControl.xaml
    /// </summary>
    public partial class OfficialDLCEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public OfficialDLCEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddOfficialDLCJobCommand = new GenericCommand(AddOfficialDLCJob);
        }

        private void AddOfficialDLCJob()
        {
            var currentOfficialDLCJobs = EditingMod.InstallationJobs.Where(x => x.IsOfficialDLCJob(EditingMod.Game)).Select(x => x.Header).ToList();
            var acceptableHeaders = ModJob.GetSupportedOfficialDLCHeaders(EditingMod.Game);
            var selectableOptions = acceptableHeaders.Except(currentOfficialDLCJobs).ToList();
            var selection = DropdownSelectorDialog.GetSelection(Window.GetWindow(this), "Select task", selectableOptions, "Select a task header", "Select an official DLC to add a task header for. Only headers that are not already added are listed here.");
            if (selection is ModJob.JobHeader header)
            {
                ModJob job = new ModJob(header);
                EditingMod.InstallationJobs.Add(job);
                job.BuildParameterMap(EditingMod);
                OfficialDLCJobs.Add(job);
            }
        }

        public GenericCommand AddOfficialDLCJobCommand { get; set; }

        public ObservableCollectionExtended<ModJob> OfficialDLCJobs { get; } = new ObservableCollectionExtended<ModJob>();

        public override void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!HasLoaded)
            {
                OfficialDLCJobs.ReplaceAll(EditingMod.InstallationJobs.Where(x => x.IsOfficialDLCJob(EditingMod.Game)));
                foreach (var v in OfficialDLCJobs)
                {
                    v.BuildParameterMap(EditingMod);
                }

                HasLoaded = true;
            }
        }


        public override void Serialize(IniData ini)
        {
            foreach (var odlc in OfficialDLCJobs)
            {
                if (odlc.ParameterMap.Any(x => !string.IsNullOrWhiteSpace(x.Value)))
                {
                    // Serialize
                    foreach (var p in odlc.ParameterMap)
                    {
                        if (!string.IsNullOrWhiteSpace(p.Value))
                        {
                            ini[odlc.Header.ToString()][p.Key] = p.Value;
                        }
                    }
                }
            }
        }

        private void HandleMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // This forces scrolling to bubble up
            // cause expander eats it
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = (((Control)sender).TemplatedParent ?? ((Control)sender).Parent) as UIElement;
                parent.RaiseEvent(eventArg);
            }
        }
    }
}
