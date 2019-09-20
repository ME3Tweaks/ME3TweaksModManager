using MassEffectModManager.modmanager.helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManager.modmanager.objects
{
    public class AlternateFile
    {
        public enum AltFileOperation
        {
            OPERATION_SUBSTITUTE,
            OPERATION_NOINSTALL,
            OPERATION_INSTALL
        }

        public enum AltFileCondition
        {
            COND_MANUAL,
            COND_DLC_PRESENT,
            COND_DLC_NOT_PRESENT
        }

        public AltFileCondition Condition;
        public AltFileOperation Operation;
        public string FriendlyName;
        public string Description;

        /// <summary>
        /// Alternate file to use, if the operation uses an alternate file
        /// </summary>
        public string AltFile;
        /// <summary>
        /// In-mod path that is operated on
        /// </summary>
        public string ModFile;

        /// <summary>
        /// BACKWARDS COMPATIBLILITY ONLY: ModDesc 4.5 used SubstituteFile but was removed from support in 5.0
        /// </summary>
        public string SubstituteFile;

        //public const string OPERATION_SUBSTITUTE = "OP_SUBSTITUTE"; //swap a file in a job
        //public const string OPERATION_NOINSTALL = "OP_NOINSTALL"; //do not install a file
        //public const string OPERATION_INSTALL = "OP_INSTALL"; //install a file
        //public const string CONDITION_MANUAL = "COND_MANUAL"; //user must choose alt
        //public const string CONDITION_DLC_PRESENT = "COND_DLC_PRESENT"; //automatically choose alt if DLC listed is present
        //public const string CONDITION_DLC_NOT_PRESENT = "COND_DLC_NOT_PRESENT"; //automatically choose if DLC is not present

        public AlternateFile(string alternateFileText, Mod modForValidating)
        {
            var properties = StringStructParser.GetCommaSplitValues(alternateFileText);
            //todo: if statements to check these.
            Enum.TryParse(properties["Condition"], out Condition);
            Enum.TryParse(properties["ModOperation"], out Operation);
            properties.TryGetValue("FriendlyName", out FriendlyName);
            properties.TryGetValue("Description", out Description);

            properties.TryGetValue("ModFile", out ModFile);
            properties.TryGetValue("AltFile", out AltFile);

            //This needs reworked from java's hack implementation
            //Need to identify mods using substitution features
            properties.TryGetValue("SubstituteFile", out SubstituteFile);
        }

    }
}