using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManager.modmanager.objects
{
    public class GameTarget
    {
        public Mod.MEGame Game { get; }
        public string TargetPath { get; }

        public GameTarget(Mod.MEGame game, string target)
        {
            this.Game = game;
            this.TargetPath = target.TrimEnd('\\');
        }
    }
}
