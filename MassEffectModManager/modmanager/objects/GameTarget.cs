using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MassEffectModManager.modmanager.objects
{
    public class GameTarget
    {
        private static readonly Color ME1BackgroundColor = Color.FromArgb(80, 181, 181, 181);
        private static readonly Color ME2BackgroundColor = Color.FromArgb(80, 255, 176, 171);
        private static readonly Color ME3BackgroundColor = Color.FromArgb(80, 196, 24, 24);
        public Mod.MEGame Game { get; }
        public string TargetPath { get; }
        public bool Active { get; set; }
        public Brush BackgroundColor
        {
            get
            {
                if (Active)
                {
                    switch (Game)
                    {
                        case Mod.MEGame.ME1:
                            return new SolidColorBrush(ME1BackgroundColor);
                        case Mod.MEGame.ME2:
                            return new SolidColorBrush(ME2BackgroundColor);
                        case Mod.MEGame.ME3:
                            return new SolidColorBrush(ME3BackgroundColor);
                    }
                }
                return null;
            }
        }

        public GameTarget(Mod.MEGame game, string target, bool currentActive)
        {
            this.Game = game;
            this.Active = currentActive;
            this.TargetPath = target.TrimEnd('\\');
        }
    }
}
