using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.launcher
{
    [AddINotifyPropertyChangedInterface]
    public class LauncherLanguageOption
    {
        /// <summary>
        /// The string to display to the user
        /// </summary>
        public string DisplayString { get; set; }
        /// <summary>
        /// The suffix locale code
        /// </summary>
        public string LanguageString { get; set; }
        /// <summary>
        /// If this is actually a custom option
        /// </summary>
        public bool IsCustomOption { get; set; }

        /// <summary>
        /// String to show in the UI
        /// </summary>
        public string UIDisplayString => $@"{LanguageString} - {DisplayString}";
    }
}
