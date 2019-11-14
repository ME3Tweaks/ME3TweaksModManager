using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static MassEffectModManagerCore.modmanager.Mod;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for GUICompatibilityGenerator.xaml
    /// </summary>
    public partial class GUICompatibilityGenerator : MMBusyPanelBase
    {
        private GameTarget target;
        public GUICompatibilityGenerator(GameTarget target)
        {
            if (target.Game != MEGame.ME3) throw new Exception("Cannot generate compatibility mods for " + target.Game);
            DataContext = this;
            this.target = target;
            InitializeComponent();
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void OnPanelVisible()
        {
            StartGuiCompatibilityScanner();
        }

        private static readonly string[] DLCUIModFolderNames =
        {
            "DLC_CON_XBX",
            "DLC_MOD_UIScaling",
            "DLC_MOD_UIScaling_Shared"
        };
        private void StartGuiCompatibilityScanner()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("GUICompatibilityScanner");
            bw.DoWork += (a, b) =>
            {
                var installedDLCMods = VanillaDatabaseService.GetInstalledDLCMods(target);
                var numMods = installedDLCMods.Count;
                installedDLCMods = installedDLCMods.Except(DLCUIModFolderNames).ToList();

                if (installedDLCMods.Count < numMods && installedDLCMods.Count > 0)
                {
                    //We have UI mod(s) installed and at least one other DLC mod.

                }
            };
            bw.RunWorkerAsync();
        }
    }
}
