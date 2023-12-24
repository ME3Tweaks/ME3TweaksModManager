using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.Helpers;

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
        public static void Parse(Mod mod, string groupData)
        {
            M3ASIVersion v = new M3ASIVersion();
            var data = StringStructParser.GetCommaSplitValues(groupData);
            if (data.Count == 0 || data.Count > 2)
            {
                M3Log.Error($@"M3ASIVersion data in moddesc is not valid. At most an M3ASIVersion struct can contain 1 or 2 values: GroupID and optionally Version. This has {data.Count} entries. Data: {data}");
                mod.LoadFailedReason = $"M3ASIVersion data in moddesc is not valid. At most an M3ASIVersion struct can contain 1 or 2 values: GroupID and optionally Version. This has {data.Count} entries. Data: {data}";
                return;
            }

            // Mandatory: GroupId
            if (!data.TryGetValue(GROUP_KEY_NAME, out var groupIdStr))
            {
                M3Log.Error(@$"M3ASIVersion data in moddesc is not valid. Key '{GROUP_KEY_NAME}' is missing. Data: {data}");
                mod.LoadFailedReason = $"M3ASIVersion data in moddesc is not valid. Key '{GROUP_KEY_NAME}' is missing. Data: {data}";
                return;
            }

            if (!int.TryParse(groupIdStr, out var groupId))
            {
                M3Log.Error(@$"M3ASIVersion data in moddesc is not valid. Key '{GROUP_KEY_NAME}' did not resolve to an integer. Data: {data}");
                mod.LoadFailedReason = $"M3ASIVersion data in moddesc is not valid. Key '{GROUP_KEY_NAME}' did not resolve to an integer. Data: {data}";
                return;
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
                        mod.LoadFailedReason = $"{VERSION_KEY_NAME} value cannot be less than or equal to 0 in M3ASIVersion structs. Invalid value: {versionStr}";
                        return;
                    }

                    M3Log.Information($@"ASI version {versionNum} will be installed of groupid {groupId}", Settings.LogModStartup);
                    v.Version = versionNum;
                }
                else
                {
                    M3Log.Error($@"{VERSION_KEY_NAME} key value must resolve to an integer in M3ASIVersion structs. Invalid value: {versionStr}");
                    mod.LoadFailedReason = $"{VERSION_KEY_NAME} key value must resolve to an integer in M3ASIVersion structs. Invalid value: {versionStr}";
                    return;
                }
            }

            // Basic validation has passed. A second validation pass will be conducted before mod install.
            mod.ASIModsToInstall.Add(v);
        }
    }
}
