using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.localizations;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.objects.alternates
{
    /// <summary>
    /// Bindable UI object that contains a list of same-group Alternates.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class AlternateGroup
    {
        /// <summary>
        /// All alternate option choices
        /// </summary>
        public ObservableCollectionExtended<AlternateOption> AlternateOptions { get; } = new();

        /// <summary>
        /// All alternate option choices that are not the selected option, for use in the drop down
        /// </summary>
        public ObservableCollectionExtended<AlternateOption> OtherOptions { get; } = new();


        /// <summary>
        /// The currently selected option. If there is only one option, this is always that option.
        /// </summary>
        public AlternateOption SelectedOption { get; set; }

        /// <summary>
        /// The first option's sort index (multi mode) or the SortIndex itself on the option (single mode)
        /// </summary>
        public int SortIndex => AlternateOptions?.FirstOrDefault()?.SortIndex ?? 0;

        /// <summary>
        /// If the checkbox (when in single mode) is selected or not. This must be populated when the alternate group is generated for the initial selection to be correct
        /// </summary>
        //public bool SingleOptionIsSelected { get; set; }

        /// <summary>
        /// The name of the option group (interpolated into title)
        /// </summary>
        public string GroupName { get; init; }

        public string GroupNameTitleText => M3L.GetString(M3L.string_interp_groupNameAlternateOptionsHeader, GroupName, AlternateOptions.Count);
        public bool IsMultiSelector => AlternateOptions.Count > 1;

        /// <summary>
        /// MULTI MODE ONLY - Binding for Expander Expanded variable
        /// </summary>
        public bool UIIsDropdownOpen { get; set; }

        private Action<AlternateOption> OnUserSelectedOption;

        /// <summary>
        /// Creates an option group with multiple options (dropdown selector mode)
        /// </summary>
        /// <param name="options"></param>
        /// <exception cref="Exception"></exception>
        public AlternateGroup(List<AlternateOption> options)
        {
            // Find the already selected one
            if (options == null || options.Count == 0)
                throw new Exception(@"AlternateGroup being generated with null or empty list of options!");
            GroupName = options[0].GroupName;
            AlternateOptions.Add(options.First(x => x.CheckedByDefault));
            AlternateOptions.AddRange(options.Where(x => !x.CheckedByDefault));
            SelectedOption = AlternateOptions.FirstOrDefault();
        }

        /// <summary>
        /// Creates an option group with only one option (checkbox mode)
        /// </summary>
        /// <param name="singleOption"></param>
        public AlternateGroup(AlternateOption singleOption)
        {
            // Find the already selected one
            if (singleOption == null)
                throw new Exception(@"AlternateGroup being generated with null option!");
            if (singleOption.GroupName != null)
                throw new Exception(@"AlternateGroup cannot be generated from a single item that has a group name!");

            AlternateOptions.Add(singleOption);
            SelectedOption = singleOption; // Single option groups always point to the option object
        }

        /// <summary>
        /// Called when the value of the dropdown changes. This doesn't get called in Single Mode.
        /// </summary>
        public void OnSelectedOptionChanged()
        {
            if (AlternateOptions.Count > 0)
            {
                var optionsList = AlternateOptions.Where(x => x != SelectedOption).ToList();
                // Todo: Sort non-selectable to the bottom
                OtherOptions.ReplaceAll(optionsList);
            }
        }

        internal void ReleaseAssets()
        {
            foreach (var ao in AlternateOptions)
            {
                ao.ReleaseLoadedImageAsset();
            }
        }

        internal void SetIsSelectedChangeHandlers(EventHandler onAlternateSelectionChanged, Action<AlternateOption> onOptionChangedByUser)
        {
            foreach (var o in AlternateOptions)
            {
                o.IsSelectedChanged += onAlternateSelectionChanged;
            }

            // Used to record order of options chosen by user for batch installation.
            OnUserSelectedOption = onOptionChangedByUser;
        }

        public void RemoveIsSelectedChangeHandler(EventHandler onAlternateSelectionChanged)
        {
            foreach (var o in AlternateOptions)
            {
                o.IsSelectedChanged -= onAlternateSelectionChanged;
            }
        }

        /// <summary>
        /// Use this method to select an option in a group and raise the necessary events a selection brings
        /// </summary>
        /// <param name="newItem">The new item to select</param>
        internal void SelectNewOption(AlternateOption newItem)
        {
            if (SelectedOption != newItem)
            {
                // Multi mode
                SelectedOption.UIIsSelected = false;
                SelectedOption.RaiseIsSelectedChanged(); // Raise that we are de-selecting this multi-option. This is so deselection logic occurs

                SelectedOption = newItem;
                SelectedOption.UIIsSelected = true;

                UIIsDropdownOpen = false; // Multi mode
                SelectedOption.RaiseIsSelectedChanged(); // Raise the event on the newly selected option so logic that depends on it will fire.
                OnUserSelectedOption?.Invoke(newItem);
            }

            if (AlternateOptions.Count == 1 && !SelectedOption.IsAlways)
            {
                // Single mode
                SelectedOption.UIIsSelected = !SelectedOption.UIIsSelected;
                SelectedOption.RaiseIsSelectedChanged();
                OnUserSelectedOption?.Invoke(SelectedOption);
            }
        }

        public bool TrySelectOption(AlternateOption newItem, bool? shouldSetToTrue = null)
        {
            if (!newItem.UIIsSelectable) return false; // Do nothing. This option is not selectable.
            SelectNewOption(newItem);

            if (shouldSetToTrue != null)
            {
                // Validation
                if (newItem.UIIsSelected != shouldSetToTrue.Value)
                {
                    M3Log.Warning($@"Automatic selection for {newItem.FriendlyName} yielded incorrect result; result should be {shouldSetToTrue.Value} for selection but it was {newItem.UIIsSelected}");
                    return false; // The end result was wrong!
                }
            }

            return true;
        }
    }
}
