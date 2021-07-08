using System.Collections.Generic;

namespace MassEffectModManagerCore.modmanager.save.game2.Components
{
    internal class PlayerClass
    {
        public string Type { get; set; }
        public string Name { get; set; }

        public PlayerClass(string type, string name)
        {
            this.Type = type;
            this.Name = name;
        }

        public static List<PlayerClass> GetClasses()
        {
            List<PlayerClass> classes = new List<PlayerClass>();
            classes.Add(new PlayerClass("SFXGame.SFXPawn_PlayerAdept", "Adept"));
            classes.Add(new PlayerClass("SFXGame.SFXPawn_PlayerEngineer", "Engineer"));
            classes.Add(new PlayerClass("SFXGame.SFXPawn_PlayerInfiltrator", "Infiltrator"));
            classes.Add(new PlayerClass("SFXGame.SFXPawn_PlayerSentinel", "Sentinel"));
            classes.Add(new PlayerClass("SFXGame.SFXPawn_PlayerSoldier", "Soldier"));
            classes.Add(new PlayerClass("SFXGame.SFXPawn_PlayerVanguard", "Vanguard"));
            return classes;
        }
    }
}
