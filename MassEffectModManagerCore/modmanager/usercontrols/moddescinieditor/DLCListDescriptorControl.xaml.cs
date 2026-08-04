using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using PropertyChanged;
using IniParser.Model;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for DLCListDescriptorControl.xaml
    /// A control for editing semicolon-separated DLC folder lists with TPMI name lookup
    /// </summary>
    public partial class DLCListDescriptorControl : UserControl, INotifyPropertyChanged
    {
        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(DLCListDescriptorControl));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(DLCListDescriptorControl));

        public string DescriptorKey
        {
            get => (string)GetValue(DescriptorKeyProperty);
            set => SetValue(DescriptorKeyProperty, value);
        }

        public static readonly DependencyProperty DescriptorKeyProperty =
            DependencyProperty.Register(nameof(DescriptorKey), typeof(string), typeof(DLCListDescriptorControl), 
                new PropertyMetadata());

        public ModdescEditorControlBase ParentEditorControl
        {
            get => (ModdescEditorControlBase)GetValue(ParentEditorControlProperty);
            set => SetValue(ParentEditorControlProperty, value);
        }

        public static readonly DependencyProperty ParentEditorControlProperty =
            DependencyProperty.Register(nameof(ParentEditorControl), typeof(ModdescEditorControlBase), typeof(DLCListDescriptorControl));

        public ObservableCollectionExtended<DLCEntry> DLCEntries { get; } = new ObservableCollectionExtended<DLCEntry>();

        public ICommand AddEntryCommand { get; private set; }
        public ICommand RemoveEntryCommand { get; private set; }

        private bool _isInitializing = false;

        public DLCListDescriptorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddEntryCommand = new GenericCommand(AddEntry);
            RemoveEntryCommand = new RelayCommand(RemoveEntry, CanRemoveEntry);
        }

        private void AddEntry()
        {
            var entry = new DLCEntry(this);
            entry.PropertyChanged += DLCEntry_PropertyChanged;
            DLCEntries.Add(entry);
        }

        private bool CanRemoveEntry(object obj)
        {
            return obj is DLCEntry;
        }

        private void RemoveEntry(object obj)
        {
            if (obj is DLCEntry entry)
            {
                entry.PropertyChanged -= DLCEntry_PropertyChanged;
                DLCEntries.Remove(entry);
            }
        }
        private void DLCEntry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!_isInitializing && e.PropertyName == nameof(DLCEntry.DLCFolderName))
            {
                // Update the TPMI name when DLC folder name changes
                if (sender is DLCEntry entry)
                {
                    entry.UpdateTPMIName();
                }
            }
        }

        public void Serialize(string header, IniData data)
        {
            var nonEmptyEntries = DLCEntries
                .Where(x => !string.IsNullOrWhiteSpace(x.DLCFolderName))
                .Select(x => x.DLCFolderName.Trim())
                .ToList();


            // Set data if any
            if (nonEmptyEntries.Any())
            {
                data[header][DescriptorKey] = string.Join(";", nonEmptyEntries);
            }
        }

        /// <summary>
        /// Loads from a semicolon split string
        /// </summary>
        /// <param name="list">List as string</param>
        internal void LoadFromList(List<string> list)
        {
            if (list == null)
            {
                // Do nothing.
                return; 
            }

            foreach (var item in list)
            {
                var entry = new DLCEntry(this) { DLCFolderName = item };
                entry.UpdateTPMIName();
                entry.PropertyChanged += DLCEntry_PropertyChanged;
                DLCEntries.Add(entry);
            }
        }

        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// Represents a single DLC entry in the list
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class DLCEntry : INotifyPropertyChanged
    {
        private readonly DLCListDescriptorControl _parentControl;

        public DLCEntry(DLCListDescriptorControl parentControl)
        {
            _parentControl = parentControl;
        }

        public string DLCFolderName { get; set; }
        public string TPMIModName { get; set; }

        public void UpdateTPMIName()
        {
            if (string.IsNullOrWhiteSpace(DLCFolderName))
            {
                TPMIModName = string.Empty;
                return;
            }

            // Get the game from the parent control
            var game = _parentControl?.ParentEditorControl?.EditingMod?.Game;
            if (game == null)
            {
                TPMIModName = string.Empty;
                return;
            }

            // Lookup TPMI info
            var tpmi = TPMIService.GetThirdPartyModInfo(DLCFolderName, game.Value);
            TPMIModName = tpmi?.modname ?? string.Empty;
        }

        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
