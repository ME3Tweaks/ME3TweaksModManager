using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media;
using MassEffectModManagerCore.modmanager.localizations;
using Serilog;

namespace MassEffectModManagerCore.modmanager.asi
{

    /// <summary>
    /// Object describing an installed ASI mod. It is mapped to an ASI Mod Verison
    /// </summary>
    public class InstalledASIMod
    {
        public Mod.MEGame Game { get; private set; }
        public string Hash { get; private set; }

        public InstalledASIMod(string asiFile, Mod.MEGame game)
        {
            Game = game;
            InstalledPath = asiFile;
            UnmappedFilename = Path.GetFileNameWithoutExtension(asiFile);
            Hash = Utilities.CalculateMD5(asiFile);
        }

        private static Brush installedBrush = new SolidColorBrush(Color.FromArgb(0x33, 0, 0xFF, 0));
        private static Brush outdatedBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0));

        // Unmapped values
        public string InstalledPath { get; set; }
        public string UnmappedFilename { get; set; }


        public bool UIOnly_Installed { get; set; }
        public bool UIOnly_Outdated { get; set; }
        public string InstallStatus => UIOnly_Outdated ? M3L.GetString(M3L.string_outdatedVersionInstalled) : (UIOnly_Installed ? M3L.GetString(M3L.string_installed) : "");
        public ASIModVersion AssociatedManifestItem { get; set; }

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

        /// <summary>
        /// Deletes the backing file for this ASI
        /// </summary>
        public void Uninstall()
        {
            Log.Information($@"Deleting installed ASI: {InstalledPath}");
            File.Delete(InstalledPath);
        }
    }
}
