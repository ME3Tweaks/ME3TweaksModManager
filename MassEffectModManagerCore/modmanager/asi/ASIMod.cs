using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Media;
using MassEffectModManagerCore.modmanager.localizations;

namespace MassEffectModManagerCore.modmanager.asi
{
    /// <summary>
    /// Object containing information about a single version of an ASI mod in the ASI mod manifest
    /// </summary>
    public class ASIMod : INotifyPropertyChanged
    {
        private static Brush installedBrush = new SolidColorBrush(Color.FromArgb(0x33, 0, 0xFF, 0));
        private static Brush outdatedBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0));
        public string DownloadLink { get; internal set; }
        public string SourceCodeLink { get; internal set; }
        
        /// <summary>
        /// MD5 of the ASI
        /// </summary>
        public string Hash { get; internal set; }
        public string Version { get; internal set; }
        public string Author { get; internal set; }
        public string InstalledPrefix { get; internal set; }
        public string Name { get; internal set; }
        public Mod.MEGame Game { get; set; }
        public string Description { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool UIOnly_Installed { get; set; }
        public bool UIOnly_Outdated { get; set; }
        public string InstallStatus => UIOnly_Outdated ? M3L.GetString(M3L.string_outdatedVersionInstalled) : (UIOnly_Installed ? M3L.GetString(M3L.string_installed) : "");
        public InstalledASIMod InstalledInfo { get; set; }

        public Brush BackgroundColor
        {
            get
            {
                if (UIOnly_Outdated)
                {
                    return outdatedBrush;
                }
                else if (UIOnly_Installed)
                {
                    return installedBrush;
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
