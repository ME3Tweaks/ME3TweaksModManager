using System;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Targets;
using ME3TweaksModManager.modmaker.generator;

namespace ME3TweaksModManager.modmaker.compiler
{
    /// <summary>
    /// Where the generated DLC has to sit in the mount order.
    ///
    /// Two numbers, arrived at differently. The PRIORITY is READ from the install: it has to be above
    /// everything the mod overrides, or the config merges underneath the thing it is trying to change
    /// and nothing happens - so it is one above the highest-mounting DLC present. The FLAGS are a
    /// RULE, in <see cref="Flags"/>, and not read from anything.
    ///
    /// The priority is resolved here, at compile time, and written into the manifest as a concrete
    /// number - the emitter never looks at the install. That is what makes a manifest reproducible and
    /// a built mod explainable.
    /// </summary>
    public class InstallMount
    {
        public int Priority { get; init; }

        /// <summary>The DLC this one mounts above. Diagnostic, but it is the thing a user would ask about.</summary>
        public string MountsAbove { get; init; }

        /// <summary>
        /// A ModMaker DLC loads in multiplayer, and nowhere else.
        ///
        /// This was briefly copied from the DLC below - on the reasoning that a config override should
        /// load wherever the content it overrides loads - which is the sort of rule that sounds right
        /// and is really just deferring the decision to whatever Milan happens to say today. Milan is
        /// a multiplayer game and a ModMaker mod is a multiplayer mod, so the flags are stated.
        /// </summary>
        public const int LoadsInMultiplayer = (int)EME3MountFileFlag.LoadsInMultiplayer;

        /// <summary>
        /// Lets a player into a match with people who do not have this mod.
        ///
        /// Config differences between host and clients DO desync - a mod that changes what spawns is
        /// changing something both machines simulate - but it is not usually harmful, and a
        /// well-designed mod is built around it. Set by default because the alternative is a mod
        /// nobody can play unless their whole lobby installed it; removable per project, because for
        /// some mods that is exactly the point.
        /// </summary>
        public const int SetupCanMisMatch = (int)EME3MountFileFlag.DLCSetupCanMisMatch;

        /// <summary>
        /// The mount flags for a ModMaker DLC.
        ///
        /// <paramref name="requireAllPlayers"/> drops <see cref="SetupCanMisMatch"/>, which stops the
        /// game putting this player in a lobby with anyone who does not have the mod.
        /// </summary>
        public static int Flags(bool requireAllPlayers) =>
            requireAllPlayers ? LoadsInMultiplayer : LoadsInMultiplayer | SetupCanMisMatch;

        /// <summary>
        /// Reads the mount position for a new ModMaker DLC on this install.
        ///
        /// ModMaker's own output is excluded from the scan, so rebuilding does not walk the priority
        /// upwards one number at a time on every compile.
        /// </summary>
        public static InstallMount Read(GameTarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var dlcRoot = M3Directories.GetDLCPath(target);
            var folders = ModMakerCatalogue.ScannableDLCFoldersInMountOrder(target);

            // Mount order, lowest first - so the last one is the one to sit above.
            var top = folders.LastOrDefault();
            if (top == null) return new InstallMount { Priority = DefaultPriority };

            var mountFile = Path.Combine(dlcRoot, top, target.Game.CookedDirName(), @"Mount.dlc");
            if (!File.Exists(mountFile))
                return new InstallMount { Priority = DefaultPriority, MountsAbove = top };

            var mount = new MountFile(mountFile);
            return new InstallMount
            {
                Priority = mount.MountPriority + 1,
                MountsAbove = top,
            };
        }

        /// <summary>
        /// Used only when the install has no DLC to read, which on Milan cannot happen - Milan IS a
        /// DLC. Kept so the compiler degrades to a wrong-but-obvious number rather than a zero that
        /// would mount below the basegame.
        /// </summary>
        public const int DefaultPriority = 4800;
    }
}
