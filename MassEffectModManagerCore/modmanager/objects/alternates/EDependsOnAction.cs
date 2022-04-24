using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.alternates
{
    /// <summary>
    /// The list of available actions that can be taken when a DependsOn condition is met or not met.
    /// </summary>
    public enum EDependsOnAction
    {
        /// <summary>
        /// The developer did not set an action. If a DependsKey is defined, this will immediately cause the mod to fail to load.
        /// </summary>
        ACTION_INVALID,
        /// <summary>
        /// Unlocks the alternate option for user choice. This clears the option by default.
        /// </summary>
        ACTION_ALLOW_SELECT,
        /// <summary>
        /// Unlocks the alternate option for user choice. This prechecks the box by default.
        /// </summary>
        ACTION_ALLOW_SELECT_CHECKED,
        /// <summary>
        /// Locks the alternate option, but selected the item. This is equivalent to 'Auto applied'.
        /// </summary>
        ACTION_DISALLOW_SELECT,

        /// <summary>
        /// Locks the alernate option, unchecking the item. This is equivalent to 'Not applicable'.
        /// </summary>
        ACTION_DISALLOW_SELECT_CHECKED,
    }
}
