using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects
{
    [DebuggerDisplay("AlternateFile | {Condition} {Operation}, ConditionalDLC: {ConditionalDLC}, ModFile: {ModFile}, AltFile: {AltFile}")]
    public class AlternateFile : INotifyPropertyChanged
    {
        public enum AltFileOperation
        {
            OP_SUBSTITUTE,
            OP_NOINSTALL,
            OP_INSTALL
        }

        public enum AltFileCondition
        {
            COND_MANUAL,
            COND_DLC_PRESENT,
            COND_DLC_NOT_PRESENT
        }

        public AltFileCondition Condition;
        public AltFileOperation Operation;
        public bool IsManual => Condition == AltFileCondition.COND_MANUAL;

        public string FriendlyName { get; private set; }
        public string Description { get; private set; }
        public List<string> ConditionalDLC = new List<string>();

        /// <summary>
        /// Alternate file to use, if the operation uses an alternate file
        /// </summary>
        public string AltFile { get; private set; }
        /// <summary>
        /// In-game relative path that will be operated on according to the specified operation
        /// </summary>
        public string ModFile { get; private set; }

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

        public event PropertyChangedEventHandler PropertyChanged;

        public AlternateFile(string alternateFileText, Mod modForValidating)
        {
            var properties = StringStructParser.GetCommaSplitValues(alternateFileText);
            //todo: if statements to check these.
            if (properties.TryGetValue("FriendlyName", out string friendlyName))
            {
                FriendlyName = friendlyName;
            }

            Enum.TryParse(properties["Condition"], out Condition);
            Enum.TryParse(properties["ModOperation"], out Operation);


            if (properties.TryGetValue("Description", out string description))
            {
                Description = description;
            }

            if (properties.TryGetValue("ModFile", out string modfile))
            {
                ModFile = modfile.TrimStart('\\', '/');
            }
            else
            {
                Log.Error("Alternate file in-mod target (ModFile) required but not specified. This value is required for all Alternate files");
                ValidAlternate = false;
                LoadFailedReason = $"Alternate file {FriendlyName} does not declare ModFile but it is required for all Alternate Files.";
                return;
            }

            if (properties.TryGetValue("AltFile", out string altfile))
            {
                AltFile = altfile;
            }
            else if (AltFile == null && properties.TryGetValue("ModAltFile", out string maltfile))
            {
                AltFile = maltfile;
            }
            properties.TryGetValue("SubstituteFile", out SubstituteFile); //Only used in 4.5. In 5.0 and above this became AltFile.

            //workaround for 4.5
            if (modForValidating.ModDescTargetVersion == 4.5 && Operation == AltFileOperation.OP_SUBSTITUTE && SubstituteFile != null)
            {
                AltFile = SubstituteFile;
            }
            if (!string.IsNullOrEmpty(AltFile))
            {
                AltFile = AltFile.Replace('/', '\\'); //Standardize paths
            }

            //This needs reworked from java's hack implementation
            //Need to identify mods using substitution features

            if (Operation == AltFileOperation.OP_INSTALL || Operation == AltFileOperation.OP_SUBSTITUTE)
            {
                //Validate file
                var altPath = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, AltFile);
                var altFileSourceExists = FilesystemInterposer.FileExists(altPath, modForValidating.Archive);
                if (!altFileSourceExists)
                {
                    Log.Error("Alternate file source (AltFile) does not exist: " + AltFile);
                    ValidAlternate = false;
                    LoadFailedReason = $"Alternate file is specified with operation {Operation}, but required file doesn't exist: {AltFile}";
                    return;
                }

                //Ensure it is not part of  DLC directory itself.
                var modFile = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, ModFile);
                //Todo
            }
            //Todo: Pass through job so we can lookup targets for add/replace files.
            //else if (Operation == AltFileOperation.OP_NOINSTALL)
            //{
            //    //Validate noinstall file exists
            //    var modFile = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, ModFile);
            //    var modFileExists = FilesystemInterposer.FileExists(modFile, modForValidating.Archive);

            //    if (!modFileExists)
            //    {
            //        Log.Error("Alternate file target (ModFile) to be operated on does not exist: " + ModFile);
            //        ValidAlternate = false;
            //        LoadFailedReason = $"Alternate file is specified with operation {Operation}, but targeted file doesn't exist: {ModFile}";
            //        return;
            //    }
            //}

            CLog.Information($"Alternate file loaded and validated: {FriendlyName}", Settings.LogModStartup);
            ValidAlternate = true;
        }

        public bool IsSelected { get; set; }
        public void SetupInitialSelection(GameTarget target)
        {
            IsSelected = false; //Reset
            if (Condition == AltFileCondition.COND_MANUAL) return;
            var installedDLC = MEDirectories.GetInstalledDLC(target);
            switch (Condition)
            {
                case AltFileCondition.COND_DLC_NOT_PRESENT:
                    //case AltFileCondition.COND_ANY_DLC_NOT_PRESENT:
                    IsSelected = !ConditionalDLC.All(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                case AltFileCondition.COND_DLC_PRESENT:
                    //case AltFileCondition.COND_ANY_DLC_PRESENT:
                    IsSelected = ConditionalDLC.Any(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                    //case AltFileCondition.COND_ALL_DLC_NOT_PRESENT:
                    //    IsSelected = !ConditionalDLC.Any(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    //    break;
                    //case AltFileCondition.COND_ALL_DLC_PRESENT:
                    //    IsSelected = ConditionalDLC.All(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    //    break;
            }
        }
    }
}