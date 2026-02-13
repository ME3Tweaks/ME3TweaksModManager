using System.Windows.Controls;

namespace ME3TweaksModManager.modmanager.usercontrols.options
{
    [AddINotifyPropertyChangedInterface]
    public abstract class M3Setting : UserControl
    {
        public string SettingCategoryHeader { get; set; }
        public string SettingTitle
        {
            get;
            set;
        }
        public string SettingDescription
        {
            get;
            set;
        }

        /// <summary>
        /// Makes the setting visible only if dev mode is enabled.
        /// </summary>
        public bool RequiresDevMode { get; set; }
        
        /// <summary>
        /// Makes the setting visible only if beta mode is enabled.
        /// </summary>
        public bool RequiresBetaMode { get; set; }
    }
}
