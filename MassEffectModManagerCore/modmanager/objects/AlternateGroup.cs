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
        public string GroupName { get; }
        public ObservableCollectionExtended<AlternateOption> AlternateOptions { get; } = new();
        public AlternateOption SelectedOption { get; set; }

        public AlternateGroup(List<AlternateOption> options)
        {
            // Find the already selected one
            AlternateOptions.Add(options.First(x => x.CheckedByDefault));
            AlternateOptions.AddRange(options.Where(x => !x.CheckedByDefault));
            SelectedOption = AlternateOptions.FirstOrDefault();
        }
    }
}
