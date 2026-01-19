using LegendaryExplorerCore.Misc;

namespace ME3TweaksModManager.modmanager.usercontrols.options
{
    [AddINotifyPropertyChangedInterface]
    public class M3SettingGroup
    {
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
        /// If true, this group is only visible when beta mode is enabled
        /// </summary>
        public bool RequiresBetaMode { get; internal set; }
    }
}
