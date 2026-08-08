using System.ComponentModel;

namespace ME3TweaksModManager.modmanager
{
    [Localizable(false)]
    public static partial class APIKeys
    {
        public static bool HasAppInsightsConnectionString => typeof(APIKeys).GetProperty("Private_AppInsightsConnectionString") != null;
        public static string AppInsightsConnectionString => (string)typeof(APIKeys).GetProperty("Private_AppInsightsConnectionString").GetValue(typeof(APIKeys));
    }
}
