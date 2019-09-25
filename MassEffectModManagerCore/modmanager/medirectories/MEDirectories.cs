using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManager.modmanager.objects;
using static MassEffectModManager.modmanager.Mod;

namespace MassEffectModManager.GameDirectories

{
    public static class MEDirectories
    {
        public static string CookedPath(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.cookedPath;
                case MEGame.ME2:
                    return ME2Directory.cookedPath;
                case MEGame.ME3:
                    return ME3Directory.cookedPath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }
        public static string GamePath(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.gamePath;
                case MEGame.ME2:
                    return ME2Directory.gamePath;
                case MEGame.ME3:
                    return ME3Directory.gamePath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }
        public static string BioGamePath(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.BioGamePath;
                case MEGame.ME2:
                    return ME2Directory.BioGamePath;
                case MEGame.ME3:
                    return ME3Directory.BIOGamePath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        /// <summary>
        /// Gets path to executable for the specified Game Target
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static string ExecutablePath(GameTarget game)
        {
            switch (game.Game)
            {
                case MEGame.ME1:
                    return ME1Directory.ExecutablePath(game.TargetPath);
                case MEGame.ME2:
                    return ME2Directory.ExecutablePath(game.TargetPath);
                case MEGame.ME3:
                    return ME3Directory.ExecutablePath(game.TargetPath);
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string DLCPath(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.DLCPath;
                case MEGame.ME2:
                    return ME2Directory.DLCPath;
                case MEGame.ME3:
                    return ME3Directory.DLCPath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string DLCPath(GameTarget target)
        {
            switch (target.Game)
            {
                case MEGame.ME1:
                    return Path.Combine(target.TargetPath, "DLC");
                case MEGame.ME2:
                case MEGame.ME3:
                    return Path.Combine(target.TargetPath, "BIOGame", "DLC");
                default:
                    throw new ArgumentOutOfRangeException(nameof(target.Game), target.Game, null);
            }
        }

        public static List<string> OfficialDLC(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.OfficialDLC;
                case MEGame.ME2:
                    return ME2Directory.OfficialDLC;
                case MEGame.ME3:
                    return ME3Directory.OfficialDLC;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        //public static bool IsInBasegame(this IMEPackage pcc) => IsInBasegame(pcc.FilePath, pcc.Game);

        public static bool IsInBasegame(string path, MEGame game) => path.StartsWith(CookedPath(game));

        //public static bool IsInOfficialDLC(this IMEPackage pcc) => IsInOfficialDLC(pcc.FilePath, pcc.Game);

        public static bool IsInOfficialDLC(string path, MEGame game)
        {
            if (game == MEGame.Unknown)
            {
                return false;
            }
            string dlcPath = DLCPath(game);

            return OfficialDLC(game).Any(dlcFolder => path.StartsWith(Path.Combine(dlcPath, dlcFolder)));
        }

        public static List<string> EnumerateGameFiles(MEGame GameVersion, string searchPath, bool recurse = true, Predicate<string> predicate = null)
        {
            List<string> files = Directory.EnumerateFiles(searchPath, "*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();

            files = EnumerateGameFiles(GameVersion, files, predicate);

            return files;
        }

        public static List<string> EnumerateGameFiles(MEGame GameVersion, List<string> files, Predicate<string> predicate = null)
        {
            if (predicate == null)
            {
                // KFreon: Set default search predicate.
                switch (GameVersion)
                {
                    case MEGame.ME1:
                        predicate = s => s.ToLowerInvariant().EndsWith(".upk", true, null) || s.ToLowerInvariant().EndsWith(".u", true, null) || s.ToLowerInvariant().EndsWith(".sfm", true, null);
                        break;
                    case MEGame.ME2:
                    case MEGame.ME3:
                        predicate = s => s.ToLowerInvariant().EndsWith(".pcc", true, null) || s.ToLowerInvariant().EndsWith(".tfc", true, null);
                        break;
                }
            }

            return files.Where(t => predicate(t)).ToList();
        }
    }
}
