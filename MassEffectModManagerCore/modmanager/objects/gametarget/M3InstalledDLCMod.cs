using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using ME3TweaksCoreWPF.Targets;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.objects.gametarget
{
    [AddINotifyPropertyChangedInterface]
    public class M3InstalledDLCMod : InstalledDLCModWPF
    {
        private static readonly SolidColorBrush DisabledBrushLightMode = new SolidColorBrush(Color.FromArgb(0xff, 232, 26, 26));
        private static readonly SolidColorBrush DisabledBrushDarkMode = new SolidColorBrush(Color.FromArgb(0xff, 247, 88, 77));

        [DependsOn(nameof(DLCFolderName))]
        public SolidColorBrush TextColor
        {
            get
            {
                if (DLCFolderName.StartsWith('x'))
                {
                    return Settings.DarkTheme ? DisabledBrushDarkMode : DisabledBrushLightMode;
                }
                return Application.Current.FindResource(AdonisUI.Brushes.ForegroundBrush) as SolidColorBrush;
            }
        }

        public M3InstalledDLCMod(string dlcFolderPath, MEGame game, Func<InstalledDLCMod, bool> deleteConfirmationCallback, Action notifyDeleted, Action notifyToggled, bool modNamePrefersTPMI) : base(dlcFolderPath, game, deleteConfirmationCallback, notifyDeleted, notifyToggled, modNamePrefersTPMI)
        {
        }

        /// <summary>
        /// Can be used as a delegate to generate an M3InstalledDLCMod object.
        /// </summary>
        /// <param name="dlcFolderPath"></param>
        /// <param name="game"></param>
        /// <param name="deleteConfirmationCallback"></param>
        /// <param name="notifyDeleted"></param>
        /// <param name="notifyToggled"></param>
        /// <param name="modNamePrefersTPMI"></param>
        /// <returns></returns>
        public static M3InstalledDLCMod GenerateInstalledDLCMod(string dlcFolderPath, MEGame game, Func<InstalledDLCMod, bool> deleteConfirmationCallback, Action notifyDeleted, Action notifyToggled, bool modNamePrefersTPMI)
        {
            return new M3InstalledDLCMod(dlcFolderPath, game, deleteConfirmationCallback, notifyDeleted, notifyToggled, modNamePrefersTPMI);
        }
    }
}
