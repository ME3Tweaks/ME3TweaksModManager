using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.alternates
{
    /// <summary>
    /// Contains alternate parsing keys for consistent parsing.
    /// </summary>
    public static class AlternateKeys
    {
        // Shared keys
        public static readonly string ALTSHARED_KEY_FRIENDLYNAME = @"FriendlyName";
        public static readonly string ALTSHARED_KEY_CONDITION = @"Condition";
        public static readonly string ALTSHARED_KEY_MODOPERATION = @"ModOperation";
        public static readonly string ALTSHARED_KEY_DESCRIPTION = @"Description";
        public static readonly string ALTSHARED_KEY_CONDITIONALDLC = @"ConditionalDLC";
        public static readonly string ALTSHARED_KEY_MULTILIST_ROOTPATH = @"MultiListRootPath";
        public static readonly string ALTSHARED_KEY_MULTILIST_ID = @"MultiListId";
        public static readonly string ALTSHARED_KEY_MULTILIST_FLATTENOUTPUT = @"FlattenMultiListOutput";
        public static readonly string ALTSHARED_KEY_DLCREQUIREMENTS = @"DLCRequirements";
        public static readonly string ALTSHARED_KEY_CHECKEDBYDEFAULT = @"CheckedByDefault";
        public static readonly string ALTSHARED_KEY_OPTIONGROUP = @"OptionGroup";
        public static readonly string ALTSHARED_KEY_HIDDEN = @"Hidden";
        public static readonly string ALTSHARED_KEY_OPTIONKEY = @"OptionKey";
        public static readonly string ALTSHARED_KEY_DEPENDSONACTION_MET = @"DependsOnMetAction";
        public static readonly string ALTSHARED_KEY_DEPENDSONACTION_NOTMET = @"DependsOnNotMetAction";
        public static readonly string ALTSHARED_KEY_DEPENDSON_KEYS = @"DependsOnKeys";
        public static readonly string ALTSHARED_KEY_SORTINDEX = @"SortIndex";
        public static readonly string ALTSHARED_KEY_APPLICABLE_TEXT = @"ApplicableAutoText";
        public static readonly string ALTSHARED_KEY_NOTAPPLICABLE_TEXT = @"NotApplicableAutoText";
        public static readonly string ALTSHARED_KEY_IMAGENAME = @"ImageAssetName";
        public static readonly string ALTSHARED_KEY_IMAGEHEIGHT = @"ImageHeight";

        // AltFile Specific
        public static readonly string ALTFILE_KEY_MULTILIST_TARGETPATH = @"MultiListTargetPath";
        public static readonly string ALTFILE_KEY_MERGEFILES = @"MergeFiles";
        public static readonly string ALTFILE_KEY_ALTFILE = @"AltFile";
        /// <summary>
        /// Same as AltFile. This key was accidentally used when alternates were first introduced. Only here for backwards compatibility.
        /// </summary>
        public static readonly string ALTFILE_KEY_ALTFILE2 = @"ModAltFile"; // Same as AltFile in parsing.
        public static readonly string ALTFILE_KEY_MODFILE = @"ModFile";
        public static readonly string ALTFILE_KEY_SUBSTITUTEFILE = @"SubstituteFile"; // Deprecated


        // AltDLC Specific
        public static readonly string ALTDLC_KEY_ALTDLC = @"ModAltDLC";
        public static readonly string ALTDLC_KEY_DESTDLC = @"ModDestDLC";
        public static readonly string ALTDLC_KEY_REQUIREDRELATIVEFILEPATHS = @"RequiredFileRelativePaths";
        public static readonly string ALTDLC_KEY_REQUIREDFILESIZES = @"RequiredFileSizes";

    }
}