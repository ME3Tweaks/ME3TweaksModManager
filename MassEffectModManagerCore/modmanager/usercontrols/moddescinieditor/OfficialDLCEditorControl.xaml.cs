using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IniParser.Model;
using LegendaryExplorerCore.Misc;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
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
            var selection = DropdownSelectorDialog.GetSelection(Window.GetWindow(this), M3L.GetString(M3L.string_selectTask), selectableOptions, M3L.GetString(M3L.string_selectATaskHeader), M3L.GetString(M3L.string_chooser_selectOfficialDLCHeader));
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
                // Main job descriptors
                odlc.Serialize(ini, EditingMod);
            }

            // This is kind of a hack but I'm not sure how to get reference unless I pass reference to owner so it can be registered
            var multilistEditors = this.FindVisualChildren<MultilistEditorControl>().ToList();
            var alternateEditors = this.FindVisualChildren<AlternateFileBuilder>().ToList();

            foreach (var m in multilistEditors)
            {
                m.Serialize(ini);
            }

            foreach (var m in alternateEditors)
            {
                m.Serialize(ini);
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
