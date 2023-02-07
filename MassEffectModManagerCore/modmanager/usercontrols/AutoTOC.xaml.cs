using System;
using System.Windows.Input;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// In-Window content container for AutoTOC. Most of this class was ported from Mod Manager Command Line Tools.
    /// </summary>
    public partial class AutoTOC : MMBusyPanelBase
    {
        private const string SFAR_SUBPATH = @"CookedPCConsole\Default.sfar";

        private enum AutoTOCMode
        {
            MODE_GAMEWIDE,
            MODE_MOD
        }

        //private AutoTOCMode mode;
        //private Mod modModeMod;
        private GameTargetWPF gameWideModeTarget;

        public int Percent { get; private set; }
        public string ActionText { get; private set; }

        public AutoTOC(GameTargetWPF target)
        {
            DataContext = this;
            this.gameWideModeTarget = target ?? throw new Exception(@"Null target specified for AutoTOC");
        }

        public AutoTOC(Mod mod)
        {
            DataContext = this;
            if (mod.Game != MEGame.ME3 && !mod.Game.IsLEGame()) throw new Exception(@"AutoTOC cannot be run on mods not designed for Mass Effect 3/Legendary Edition games.");
            //this.modModeMod = mod;
        }

        private void RunModAutoTOC()
        {
            //Implement mod-only autotoc, for deployments
            // TODO actually do this
        }

        public static bool RunTOCOnGameTarget(GameTargetWPF target, Action<int> percentDoneCallback = null)
        {
            M3Log.Information(@"Autotocing game: " + target.TargetPath);
            TOCCreator.CreateTOCForGame(target.Game, percentDoneCallback, target.TargetPath);
            return true;
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();

            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"AutoTOC");
            nbw.DoWork += (a, b) =>
            {
                //if (mode == AutoTOCMode.MODE_GAMEWIDE)
                {
                    RunTOCOnGameTarget(gameWideModeTarget, x => Percent = x);
                }
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }
    }
}
