using LegendaryExplorerCore.GameFilesystem;

namespace ME3TweaksModManager.modmanager.postship
{
    /// <summary>
    /// Contains fixes for mods that have already shipped and would be cumbersome to fix otherwise.
    /// </summary>
    internal static class PostShipFixes
    {

        /// <summary>
        /// Applys fixes to mods that have already shipped and have just been installed
        /// </summary>
        /// <param name="target"></param>
        /// <param name="dlcFolder"></param>
        public static void ApplyPostShipDLCFixes(GameTarget target, string dlcFolder)
        {
            if (target.Game.IsLEGame())
            {
                ChangeALOTM3TOMounts(target, dlcFolder);
            }
        }

        private static void ChangeALOTM3TOMounts(GameTarget target, string dlcFolder)
        {
            var folderName = Path.GetFileName(dlcFolder);
            if (folderName == @"DLC_MOD_ALOT")
            {
                // It's the ALOT M3TO folder.
                switch (target.Game)
                {
                    case MEGame.LE1:
                        {
                            var mountPath = Path.Combine(dlcFolder, @"Autoload.ini");
                            if (File.Exists(mountPath))
                            {
                                var mount = new AutoloadIni();
                                if (mount.ModMount == 9234)
                                {
                                    mount.ModMount = 60; // Correct to mount 60, to give space above and below it.
                                    M3Log.Information($@"PostShipFixes: Correcting {target.Game} ALOT M3TO Autoload.ini mount to {mount.ModMount}");
                                    File.WriteAllText(mountPath, mount.ToString());
                                }
                            }
                        }
                        break;
                    case MEGame.LE2:
                    case MEGame.LE3:
                        {
                            var mountPath = Path.Combine(dlcFolder, @"CookedPCConsole", @"mount.dlc");
                            if (File.Exists(mountPath))
                            {
                                var mount = new MountFile(mountPath);
                                var shouldUpdate = mount.MountPriority == (target.Game == MEGame.LE2 ? 23433 : 31333);
                                if (shouldUpdate)
                                {
                                    // LE3 DLC must mount above 1000 or internal exe code will block it from running. But it is fine if we mount
                                    // below official DLC which starts at 2000.
                                    mount.MountPriority = target.Game == MEGame.LE2 ? 2100 : 1500;
                                    M3Log.Information($@"PostShipFixes: Correcting {target.Game} ALOT M3TO mount.dlc mount to {mount.MountPriority}");
                                    mount.WriteMountFile(mountPath);
                                }
                            }
                        }
                        break;
                }
            }
        }
    }
}
