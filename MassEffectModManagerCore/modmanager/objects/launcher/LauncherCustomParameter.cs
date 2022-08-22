using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.launcher
{
    [AddINotifyPropertyChangedInterface]
    public class LauncherCustomParameter
    {

        /// <summary>
        /// The string shown to the user
        /// </summary>
        public string DisplayString { get; set; }

        /// <summary>
        /// The text to set for the command line when added
        /// </summary>
        public string CommandLineText { get; set; }
        /// <summary>
        /// If this custom option is selected
        /// </summary>
        public bool IsSelected { get; set; }

    }
}
