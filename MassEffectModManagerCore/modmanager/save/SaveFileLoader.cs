using System.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.save.game3;

namespace MassEffectModManagerCore.modmanager.save
{
    public class SaveFileLoader
    {
        public static ISaveFile LoadSaveFile(Stream stream, string fileName = null)
        {
            var version = stream.ReadInt32();
            stream.Position -= 4;
            switch (version)
            {
                case 29:
                    return SaveFileGame3.Read(stream, fileName, MEGame.ME2);
                case 30:
                    return SaveFileGame3.Read(stream, fileName, MEGame.LE2);
                case 59:
                    return SaveFileGame3.Read(stream, fileName); // both have same version number, nice

            }


            return null;
        }
    }
}
