using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.usercontrols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.deployment.checks
{
    internal class ManualValidationFunction
    {
        /// <summary>
        /// Manually validated items
        /// </summary>
        /// <param name="item"></param>
        internal static void ManualValidation(DeploymentChecklistItem item)
        {
            item.ToolTip = M3L.GetString(M3L.string_thisItemMustBeManuallyCheckedByYou);
        }
    }
}
