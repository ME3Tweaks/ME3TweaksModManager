using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.objects
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
        /// The displayed group name
        /// </summary>
        public string GroupName { get; init; }

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

        public void OnSelectedOptionChanged()
        {
            var optionsList = AlternateOptions.Where(x => x != SelectedOption).ToList();
            // Todo: Sort non-selectable to the bottom


            OtherOptions.ReplaceAll(optionsList);
        }
    }
}
