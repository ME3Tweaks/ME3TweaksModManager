using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.tutorial
{
    /// <summary>
    /// Represents a step in the Mod Manager tutorial
    /// </summary>
    public class TutorialStep
    {
        public string step { get; set; }
        public string internalname { get; set; }
        public string imagename { get; set; }
        public string imagemd5 { get; set; }
        public string columnindex { get; set; }
        public string rowindex { get; set; }
        public string columnspan { get; set; }
        public string rowspan { get; set; }
        public string lang_int { get; set; }
        public string lang_rus { get; set; }
        public string lang_pol { get; set; }
        public string lang_deu { get; set; }
        public string lang_fra { get; set; }
        public string lang_esn { get; set; }
        /// <summary>
        /// The current string being shown 
        /// </summary>
        public string UIString { get; set; }
        /// <summary>
        /// The path to the image
        /// </summary>
        public string UIImagePath { get; set; } //image path

    }
}
