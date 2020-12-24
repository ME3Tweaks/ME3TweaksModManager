using System.ComponentModel;

namespace MassEffectModManagerCore.modmanager
{
    [Localizable(false)]
    public static partial class APIKeys
    {
        public static bool HasAppCenterKey => typeof(APIKeys).GetProperty("Private_AppCenter") != null;
        public static string AppCenterKey => (string)typeof(APIKeys).GetProperty("Private_AppCenter").GetValue(typeof(APIKeys));

        public static bool HasNexusSearchKey => typeof(APIKeys).GetProperty("Private_NexusSearch") != null;
        public static string NexusSearchKey => (string)typeof(APIKeys).GetProperty("Private_NexusSearch").GetValue(typeof(APIKeys));
    }
}
