using System.ComponentModel;
using LegendaryExplorerCore.Misc;

namespace ME3TweaksModManager.modmanager.usercontrols.options
{
    public class M3SettingGroup : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Header for the group
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// The description to show for the group
        /// </summary>
        public string GroupDescription { get; set; }

        /// <summary>
        /// The settings in the group
        /// </summary>
        public ObservableCollectionExtended<M3Setting> AllSettings { get; init; }

        /// <summary>
        /// Delegate that gets invoked to determine if this group should be visible
        /// </summary>
        public Func<bool> VisibilityDelegate { get; init; } = () => true;

        /// <summary>
        /// Exposes the result of the visibility delegate as a bindable property
        /// (assumes the delegate is always present)
        /// </summary>
        public bool IsVisible => VisibilityDelegate();

        internal void RefreshVisibility()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }
}
