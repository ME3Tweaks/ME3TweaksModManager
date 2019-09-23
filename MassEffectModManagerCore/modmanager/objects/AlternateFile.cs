using MassEffectModManager.modmanager.helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using MassEffectModManagerCore.modmanager;

namespace MassEffectModManager.modmanager.objects
{
    [DebuggerDisplay("AlternateFile | {Condition} {Operation}, ConditionalDLC: {ConditionalDLC}, ModFile: {ModFile}, AltFile: {AltFile}")]
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
        public List<string> ConditionalDLC = new List<string>();

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
        public bool ValidAlternate;
        public string LoadFailedReason;
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
            properties.TryGetValue("SubstituteFile", out SubstituteFile); //Only used in 4.5. In 5.0 and above this became AltFile.

            if (modForValidating.ModDescTargetVersion == 4.5 && Operation == AltFileOperation.OPERATION_SUBSTITUTE)
            {
                AltFile = SubstituteFile;
            }
            if (!string.IsNullOrEmpty(AltFile))
            {
                AltFile = AltFile.Replace('/', '\\'); //Standardize paths
            }

            //This needs reworked from java's hack implementation
            //Need to identify mods using substitution features

            if (Operation == AltFileOperation.OPERATION_INSTALL || Operation == AltFileOperation.OPERATION_SUBSTITUTE)
            {
                //Validate file
                var altPath = modForValidating.PathCombine(modForValidating.ModPath, AltFile);
                var altFileSourceExists = modForValidating.FileExists(altPath);
                if (!altFileSourceExists)
                {
                    Log.Error("Alternate file source (AltFile) does not exist: " + AltFile);
                    ValidAlternate = false;
                    LoadFailedReason = $"Alternate file is specified with operation {Operation}, but required file doesn't exist: {AltFile}";
                    return;
                }
            }

            CLog.Information($"Alternate file loaded and validated: {FriendlyName}", Settings.LogModStartup);
            ValidAlternate = true;
        }

    }
}