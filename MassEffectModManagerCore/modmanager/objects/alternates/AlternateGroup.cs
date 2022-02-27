using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
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
        /// If the checkbox (when in single mode) is selected or not. This must be populated when the alternate group is generated for the initial selection to be correct
        /// </summary>
        //public bool SingleOptionIsSelected { get; set; }

        /// <summary>
        /// The name of the option group (interpolated into title)
        /// </summary>
        public string GroupName { get; init; }

        public string GroupNameTitleText => $"{GroupName} - {AlternateOptions.Count} options(s)";

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
            var optionsList = AlternateOptions.Where(x => x != SelectedOption).ToList();
            // Todo: Sort non-selectable to the bottom

            OtherOptions.ReplaceAll(optionsList);
        }

        internal void ReleaseAssets()
        {
            foreach (var ao in AlternateOptions)
            {
                ao.ReleaseLoadedImageAsset();
            }
        }

        internal void SetIsSelectedChangeHandler(EventHandler onAlternateSelectionChanged)
        {
            foreach (var o in AlternateOptions)
            {
                o.IsSelectedChanged += onAlternateSelectionChanged;
            }
        }
    }
}
