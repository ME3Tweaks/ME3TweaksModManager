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

        public enum AltFileCondtion
        {
            COND_MANUAL,
            COND_DLC_PRESENT,
            COND_DLC_NOT_PRESENT
        }
        //public const string OPERATION_SUBSTITUTE = "OP_SUBSTITUTE"; //swap a file in a job
        //public const string OPERATION_NOINSTALL = "OP_NOINSTALL"; //do not install a file
        //public const string OPERATION_INSTALL = "OP_INSTALL"; //install a file
        //public const string CONDITION_MANUAL = "COND_MANUAL"; //user must choose alt
        //public const string CONDITION_DLC_PRESENT = "COND_DLC_PRESENT"; //automatically choose alt if DLC listed is present
        //public const string CONDITION_DLC_NOT_PRESENT = "COND_DLC_NOT_PRESENT"; //automatically choose if DLC is not present

        public AlternateFile(string alternateFileText)
        {
            var properties = StringStructParser.GetCommaSplitValues(alternateFileText);
        }

 }
}