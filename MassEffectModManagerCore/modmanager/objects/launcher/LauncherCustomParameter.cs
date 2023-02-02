using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.launcher
{
    /// <summary>
    /// Defines a custom parameter for launching a game
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    
    public class LauncherCustomParameter
    {
        // DO NOT CHANGE
        // Keys for the M3L package files
        public const string KEY_AUTORESUME = @"autoresume";
        public const string KEY_MINIDUMPS = @"enableminidumps";
        public const string KEY_NOFORCEFEEDBACK = @"noforcefeedback";

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
        /// <summary>
        /// Tooltip shown when hovering over the text
        /// </summary>
        public string ToolTip { get; set; }

        /// <summary>
        /// The key this option is serialized to in the LOP file if this is a keyed entry. Use the constants from this class to set them.
        /// </summary>
        public string SaveKey { get; set; }

    }
}
