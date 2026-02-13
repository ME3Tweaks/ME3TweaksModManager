using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.localizations;

namespace ME3TweaksModManager.modmanager.objects.mod.moddesc
{
    /// <summary>
    /// Moddesc struct for a mod requesting an ASI version to be installed when the mod is installed
    /// </summary>
    public class M3ASIVersion
    {
        public const string GROUP_KEY_NAME = @"GroupID";
        public const string VERSION_KEY_NAME = @"Version";

        public int ASIGroupID { get; set; }
        public int? Version { get; set; }

        /// <summary>
        /// Initializes a new instance of the M3ASIVersion class.
        /// </summary>
        public M3ASIVersion() { }

        /// <summary>
        /// Parses group and version information from the specified data string and updates the provided mod instance
        /// with validation results.
        /// </summary>
        /// <remarks>If the input data is invalid or missing required keys, the method sets an appropriate
        /// error message on the mod's LoadFailedReason property. Only basic validation is performed; further validation
        /// may occur before mod installation.</remarks>
        /// <param name="mod">The mod instance to update with parsing results and any validation errors. This parameter cannot be null.</param>
        /// <param name="groupData">A string containing group and optional version data, Sin a comma-separated key-value format. Must
        /// include a valid group identifier.</param>
        public static M3ASIVersion Parse(Mod mod, string groupData)
        {
            M3ASIVersion v = new M3ASIVersion();
            var data = StringStructParser.GetCommaSplitValues(groupData);
            if (data.Count == 0 || data.Count > 2)
            {
                M3Log.Error($@"M3ASIVersion data in moddesc is not valid. At most an M3ASIVersion struct can contain 1 or 2 values: GroupID and optionally Version. This has {data.Count} entries. Data: {data}");
                mod.LoadFailedReason = M3L.GetString(M3L.string_validation_m3ASIVersionWrongValueCount, data.Count, data);
                return null;
            }

            // Mandatory: GroupId
            if (!data.TryGetValue(GROUP_KEY_NAME, out var groupIdStr))
            {
                M3Log.Error(@$"M3ASIVersion data in moddesc is not valid. Key '{GROUP_KEY_NAME}' is missing. Data: {data}");
                mod.LoadFailedReason = M3L.GetString(M3L.string_interp_m3ASIVersionMissingKey, GROUP_KEY_NAME, data);
                return null;
            }

            if (!int.TryParse(groupIdStr, out var groupId))
            {
                M3Log.Error(@$"M3ASIVersion data in moddesc is not valid. Key '{GROUP_KEY_NAME}' did not resolve to an integer. Data: {data}");
                mod.LoadFailedReason = M3L.GetString(M3L.string_interp_m3ASIVersionNotAnInteger, GROUP_KEY_NAME, data);
                return null;
            }

            // We don't validate the value is >= 0 cause if it doesn't exist it won't resolve anyways.
            v.ASIGroupID = groupId;

            // Optional: Version
            if (!data.TryGetValue(VERSION_KEY_NAME, out var versionStr))
            {
                M3Log.Information(@"Version key was not detected for M3ASIVersion. The latest version of this ASI will be installed.", Settings.LogModStartup);
            }
            else
            {
                // Todo: Maybe make localized strings more consistent
                // Has 'Version'
                if (int.TryParse(versionStr, out var versionNum))
                {
                    if (versionNum <= 0)
                    {
                        M3Log.Error($@"{VERSION_KEY_NAME} value cannot be less than or equal to 0 in M3ASIVersion structs. Invalid value: {versionStr}");
                        mod.LoadFailedReason = M3L.GetString(M3L.string_interp_m3ASIVersionMustBeGTOne, VERSION_KEY_NAME, versionStr);
                        return null;
                    }

                    M3Log.Information($@"ASI version {versionNum} will be installed of groupid {groupId}", Settings.LogModStartup);
                    v.Version = versionNum;
                }
                else
                {
                    M3Log.Error($@"{VERSION_KEY_NAME} key value must resolve to an integer in M3ASIVersion structs. Invalid value: {versionStr}");
                    mod.LoadFailedReason = M3L.GetString(M3L.string_interp_m3ASIVersionValueMustBeInteger, VERSION_KEY_NAME, versionStr);
                    return null;
                }
            }

            // Basic validation has passed. A second validation pass will be conducted before mod install.
            return v;
        }

        /// <summary>
        /// Converts this object to it's moddesc.ini representation
        /// </summary>
        /// <returns></returns>
        public string ToStruct()
        {
            var data = new Dictionary<string, string>(2);
            data[M3ASIVersion.GROUP_KEY_NAME] = ASIGroupID.ToString();
            if (Version != null)
            {
                data[M3ASIVersion.VERSION_KEY_NAME] = Version.ToString();
            }
            return StringStructParser.BuildCommaSeparatedSplitValueList(data);
        }

        public override string ToString()
        {
            return $@"{nameof(M3ASIVersion)} GroupID: {ASIGroupID}, Version: {Version}";
        }
    }
}
