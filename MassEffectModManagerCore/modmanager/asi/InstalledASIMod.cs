using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using MassEffectModManagerCore.modmanager.localizations;
using Serilog;

namespace MassEffectModManagerCore.modmanager.asi
{

    /// <summary>
    /// Object describing an installed ASI mod. Subclasses determine if this is known or unknown due to fun data binding issues in WPF
    /// </summary>
    public abstract class InstalledASIMod
    {
        public Mod.MEGame Game { get; private set; }
        public string Hash { get; private set; }

        protected InstalledASIMod(string asiFile, string hash, Mod.MEGame game)
        {
            Game = game;
            InstalledPath = asiFile;
            Hash = hash;
        }

        public string InstalledPath { get; set; }
        public abstract Brush BackgroundColor { get; }

        /// <summary>
        /// Deletes the backing file for this ASI
        /// </summary>
        public bool Uninstall()
        {
            Log.Information($@"Deleting installed ASI: {InstalledPath}");
            try
            {
                File.Delete(InstalledPath);
                return true;
            }
            catch (Exception e)
            {
                Log.Error($@"Error uninstalling ASI {InstalledPath}: {e.Message}");
                return false;
            }
        }
    }

    public class KnownInstalledASIMod : InstalledASIMod
    {
        private static Brush installedBrush = new SolidColorBrush(Color.FromArgb(0x33, 0, 0xFF, 0));
        private static Brush outdatedBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0));

        public KnownInstalledASIMod(string filepath, string hash, Mod.MEGame game, ASIModVersion mappedVersion) : base(filepath, hash, game)
        {
            AssociatedManifestItem = mappedVersion;
        }

        /// <summary>
        /// The manifest version information about this installed ASI mod
        /// </summary>
        public ASIModVersion AssociatedManifestItem { get; set; }

        /// <summary>
        /// If this installed ASI mod is outdated
        /// </summary>
        public bool Outdated => AssociatedManifestItem.OwningMod.Versions.Last() != AssociatedManifestItem;

        public string InstallStatus => Outdated ? M3L.GetString(M3L.string_outdatedVersionInstalled) : M3L.GetString(M3L.string_installed);
        public override Brush BackgroundColor => Outdated ? outdatedBrush : installedBrush;
    }

    public class UnknownInstalledASIMod : InstalledASIMod
    {
        private static Brush brush = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0x10, 0x10));

        public UnknownInstalledASIMod(string filepath, string hash, Mod.MEGame game) : base(filepath, hash, game)
        {
            UnmappedFilename = Path.GetFileNameWithoutExtension(filepath);
        }
        public string UnmappedFilename { get; set; }
        public override Brush BackgroundColor => brush;

    }
}
