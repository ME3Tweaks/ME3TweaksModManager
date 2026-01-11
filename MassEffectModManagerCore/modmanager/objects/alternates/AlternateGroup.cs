using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.localizations;

namespace ME3TweaksModManager.modmanager.objects.alternates
{
    /// <summary>
    /// Bindable UI object that contains a list of same-group <see cref="AlternateOption"/> objects.
    /// This class manages both single-option mode (checkbox) and multi-option mode (dropdown selector) for mod installation options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the group contains only one option, it operates in checkbox mode where the option can be toggled on/off.
    /// When the group contains multiple options, it operates in dropdown mode where one option must be selected from the group.
    /// </para>
    /// <para>
    /// For multi-option groups, options must share the same <see cref="AlternateOption.GroupName"/>.
    /// The group enforces mutual exclusivity - only one option can be selected at a time.
    /// </para>
    /// </remarks>
    [AddINotifyPropertyChangedInterface]
    public class AlternateGroup
    {
        /// <summary>
        /// All alternate option choices in this group.
        /// </summary>
        /// <remarks>
        /// In multi-option mode, the first item is always the currently selected option.
        /// In single-option mode, this collection contains exactly one item.
        /// </remarks>
        public ObservableCollectionExtended<AlternateOption> AlternateOptions { get; } = new();

        /// <summary>
        /// All alternate option choices that are not the selected option, for use in the dropdown UI.
        /// </summary>
        /// <remarks>
        /// This collection is only populated and used in multi-option mode (dropdown selector).
        /// It is automatically synchronized with <see cref="SelectedOption"/> to exclude the currently selected option.
        /// </remarks>
        public ObservableCollectionExtended<AlternateOption> OtherOptions { get; } = new();


        /// <summary>
        /// The currently selected option. If there is only one option, this always references that option.
        /// </summary>
        /// <remarks>
        /// When this property changes in multi-option mode, the previous selection is automatically deselected
        /// and appropriate events are raised.
        /// </remarks>
        public AlternateOption SelectedOption { get; set; }

        /// <summary>
        /// Gets the sort index for this group. Returns the first option's sort index in multi-option mode,
        /// or the single option's sort index in single-option mode.
        /// </summary>
        /// <value>
        /// The sort index value, or 0 if no options are present.
        /// </value>
        public int SortIndex => AlternateOptions?.FirstOrDefault()?.SortIndex ?? 0;

        /// <summary>
        /// The name of the option group used for display purposes.
        /// </summary>
        /// <remarks>
        /// This value is interpolated into <see cref="GroupNameTitleText"/> to create the header display.
        /// In single-option mode, this is null as the option is not part of a named group.
        /// </remarks>
        public string GroupName { get; init; }

        /// <summary>
        /// Gets the formatted title text for this group, including the group name and option count.
        /// </summary>
        /// <remarks>
        /// The text is localized and formatted as "[GroupName] (X options)" where X is the number of options.
        /// This property is typically bound to UI headers in multi-option mode.
        /// </remarks>
        public string GroupNameTitleText => M3L.GetString(M3L.string_interp_groupNameAlternateOptionsHeader, GroupName, AlternateOptions.Count);
        
        /// <summary>
        /// Gets a value indicating whether this group operates in multi-selector (dropdown) mode.
        /// </summary>
        /// <value>
        /// <c>true</c> if the group contains multiple options (dropdown mode); <c>false</c> if it contains a single option (checkbox mode).
        /// </value>
        public bool IsMultiSelector => AlternateOptions.Count > 1;

        /// <summary>
        /// Gets or sets whether the dropdown UI is currently expanded.
        /// </summary>
        /// <remarks>
        /// This property is only used in multi-selector mode and is typically bound to an Expander control's IsExpanded property.
        /// It is automatically set to false when a new option is selected via <see cref="SelectNewOption"/>.
        /// </remarks>
        public bool UIIsDropdownOpen { get; set; }

        /// <summary>
        /// Callback invoked when the user selects an option. Used to record the order of options chosen for batch installation.
        /// </summary>
        private Action<AlternateOption> OnUserSelectedOption;

        /// <summary>
        /// Creates an option group with multiple options (dropdown selector mode).
        /// </summary>
        /// <param name="options">The list of options to include in the group. All options must share the same <see cref="AlternateOption.GroupName"/>.</param>
        /// <exception cref="Exception">Thrown when <paramref name="options"/> is null or empty.</exception>
        /// <remarks>
        /// The option marked with <see cref="AlternateOption.CheckedByDefault"/> is placed first in <see cref="AlternateOptions"/>
        /// and becomes the initial <see cref="SelectedOption"/>.
        /// </remarks>
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
        /// Creates an option group with only one option (checkbox mode).
        /// </summary>
        /// <param name="singleOption">The single option for this group.</param>
        /// <exception cref="Exception">
        /// Thrown when <paramref name="singleOption"/> is null, or when the option has a <see cref="AlternateOption.GroupName"/> 
        /// (single options should not be part of a named group).
        /// </exception>
        /// <remarks>
        /// Single option groups always set <see cref="SelectedOption"/> to the provided option, and <see cref="GroupName"/> remains null.
        /// </remarks>
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
        /// Called when the <see cref="SelectedOption"/> changes. Synchronizes the <see cref="OtherOptions"/> collection accordingly.
        /// </summary>
        /// <param name="oldItem">The previously selected option, or null during initial population.</param>
        /// <param name="newItem">The newly selected option.</param>
        /// <remarks>
        /// <para>
        /// This method is only relevant in multi-selector mode (when <see cref="AlternateOptions"/> contains multiple items).
        /// </para>
        /// <para>
        /// When <paramref name="oldItem"/> is null, the method performs initial population of <see cref="OtherOptions"/>.
        /// Otherwise, it swaps the old and new items in the <see cref="OtherOptions"/> collection to maintain proper ordering.
        /// </para>
        /// </remarks>
        public void OnSelectedOptionChanged(AlternateOption oldItem, AlternateOption newItem)
        {
            if (AlternateOptions.Count > 0)
            {
                if (newItem != null && oldItem != null)
                {
                    var swappingOutIdx = OtherOptions.IndexOf(newItem);
                    OtherOptions.Remove(newItem);
                    OtherOptions.Insert(swappingOutIdx, oldItem);
                } 
                else if (oldItem == null)
                {
                    // Initial population
                    var optionsList = AlternateOptions.Where(x => x != SelectedOption).ToList();
                    OtherOptions.ReplaceAll(optionsList);
                }
            }
        }

        /// <summary>
        /// Releases image asset references from all options in this group to allow garbage collection.
        /// </summary>
        /// <remarks>
        /// This method should be called when the group is no longer needed to free up memory used by loaded image assets.
        /// It iterates through all <see cref="AlternateOptions"/> and calls <see cref="AlternateOption.ReleaseLoadedImageAsset"/> on each.
        /// </remarks>
        internal void ReleaseAssets()
        {
            foreach (var ao in AlternateOptions)
            {
                ao.ReleaseLoadedImageAsset();
            }
        }

        /// <summary>
        /// Registers event handlers for selection changes on all options in this group.
        /// </summary>
        /// <param name="onAlternateSelectionChanged">Event handler to be attached to each option's <see cref="AlternateOption.IsSelectedChanged"/> event.</param>
        /// <param name="onOptionChangedByUser">Action to be invoked when the user manually changes the selected option. Used to record installation choices.</param>
        /// <remarks>
        /// This method should be called during initialization to set up the event wiring for the group.
        /// The <paramref name="onOptionChangedByUser"/> callback is used to track the order of user selections for batch installations.
        /// </remarks>
        internal void SetIsSelectedChangeHandlers(EventHandler onAlternateSelectionChanged, Action<AlternateOption> onOptionChangedByUser)
        {
            foreach (var o in AlternateOptions)
            {
                o.IsSelectedChanged += onAlternateSelectionChanged;
            }

            // Used to record order of options chosen by user for batch installation.
            OnUserSelectedOption = onOptionChangedByUser;
        }

        /// <summary>
        /// Removes the specified event handler from all options in this group.
        /// </summary>
        /// <param name="onAlternateSelectionChanged">The event handler to remove from each option's <see cref="AlternateOption.IsSelectedChanged"/> event.</param>
        /// <remarks>
        /// This method should be called during cleanup to prevent memory leaks from event handler references.
        /// </remarks>
        public void RemoveIsSelectedChangeHandler(EventHandler onAlternateSelectionChanged)
        {
            foreach (var o in AlternateOptions)
            {
                o.IsSelectedChanged -= onAlternateSelectionChanged;
            }
        }

        /// <summary>
        /// Selects a new option in the group and raises the necessary selection change events.
        /// </summary>
        /// <param name="newItem">The option to select.</param>
        /// <remarks>
        /// <para>
        /// <b>Multi-option mode behavior:</b> If <paramref name="newItem"/> is different from <see cref="SelectedOption"/>,
        /// the current selection is deselected, the new item becomes selected, the dropdown is closed, and events are raised.
        /// </para>
        /// <para>
        /// <b>Single-option mode behavior:</b> If this is a single-option group (not using <see cref="AlternateOption.IsAlways"/>),
        /// the selection is toggled on/off.
        /// </para>
        /// <para>
        /// The <see cref="OnUserSelectedOption"/> callback is invoked after selection changes to record the user's choice.
        /// </para>
        /// </remarks>
        internal void SelectNewOption(AlternateOption newItem)
        {
            if (SelectedOption != newItem)
            {
                // Multi mode
                var previousOption = SelectedOption; // Store the previous option BEFORE changing SelectedOption (as this may trigger changes)
                previousOption.UIIsSelected = false;
                previousOption.RaiseIsSelectedChanged(); // Raise that we are de-selecting this multi-option. This is so deselection logic occurs

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

        /// <summary>
        /// Attempts to select the specified option if it is selectable, optionally validating the result.
        /// </summary>
        /// <param name="newItem">The option to select.</param>
        /// <param name="shouldSetToTrue">
        /// Optional validation parameter. If provided, the method verifies that the option's final 
        /// <see cref="AlternateOption.UIIsSelected"/> state matches this value after selection.
        /// </param>
        /// <returns>
        /// <c>true</c> if the option was successfully selected (or toggled) and passed validation (if requested); 
        /// <c>false</c> if the option is not selectable or failed validation.
        /// </returns>
        /// <remarks>
        /// <para>
        /// If the option's <see cref="AlternateOption.UIIsSelectable"/> property is false, this method returns false immediately
        /// without attempting to change the selection.
        /// </para>
        /// <para>
        /// When <paramref name="shouldSetToTrue"/> is provided, the method logs a warning if the validation fails,
        /// indicating a potential logic error in the selection system.
        /// </para>
        /// </remarks>
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
