using System.Collections.Generic;

namespace ME3TweaksModManager.modmanager.save.game2.Components
{
    internal class PlayerNotoriety
    {
        public FileFormats.Save.NotorietyType Type { get; set; }
        public string Name { get; set; }

        public PlayerNotoriety(FileFormats.Save.NotorietyType type, string name)
        {
            this.Type = type;
            this.Name = name;
        }

        public static List<PlayerNotoriety> GetNotorieties()
        {
            List<PlayerNotoriety> notorieties = new List<PlayerNotoriety>();
            notorieties.Add(new PlayerNotoriety(FileFormats.Save.NotorietyType.None, "None"));
            notorieties.Add(new PlayerNotoriety(FileFormats.Save.NotorietyType.Ruthless, "Ruthless"));
            notorieties.Add(new PlayerNotoriety(FileFormats.Save.NotorietyType.Survivor, "Sole Survivor"));
            notorieties.Add(new PlayerNotoriety(FileFormats.Save.NotorietyType.Warhero, "War Hero"));
            return notorieties;
        }
    }
}
