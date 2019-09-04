using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace MassEffectModManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static int BuildNumber = Assembly.GetEntryAssembly().GetName().Version.Revision;

        public static string AppVersionHR
        {
            get
            {
                Version assemblyVersion = Assembly.GetEntryAssembly().GetName().Version;
                string version = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";
                if (assemblyVersion.Build != 0)
                {
                    version += "." + assemblyVersion.Build;
                }

#if DEBUG
                version += " DEBUG";
#else
                version += "TEST BUILD";
#endif
                return $"Mass Effect Mod Manager {version} (Build {BuildNumber})";
            }
        }
    }
}
