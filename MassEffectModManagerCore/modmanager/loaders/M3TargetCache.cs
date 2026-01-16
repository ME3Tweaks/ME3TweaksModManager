using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects;
using Org.BouncyCastle.Asn1.X509;
using Pathoschild.FluentNexus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ME3TweaksModManager.modmanager.loaders
{
    /// <summary>
    /// Holds the list of targets that are in the target cache files so we don't need to 
    /// do disk I/O to see the list of targets. Accessing the targets cache should be done
    /// through this.
    /// </summary>
    internal static class M3TargetCache
    {
        /// <summary>
        /// Contains a list of targets that are in the target cache files, cached for performance.
        /// Key is the game, value is the list of paths that are targets for the key's game
        /// </summary>
        private static Dictionary<MEGame, List<string>> _targetCache = new();

        /// <summary>
        /// Gets cached targets for a specific game, optionally tracking failures
        /// </summary>
        /// <param name="game">The game to get cached targets for</param>
        /// <param name="existingTargets">Targets to exclude from loading</param>
        /// <param name="failedTargets">Out list to populate with failed targets</param>
        /// <param name="returnInvalid">Whether to include invalid targets in the returned list.</param>
        /// <returns>List of valid targets</returns>
        internal static List<TargetCacheInfo> GetCachedTargets(MEGame game, List<GameTargetWPF> existingTargets, out List<TargetCacheInfo> failedTargets)
        {
            failedTargets = new List<TargetCacheInfo>();

            if (!_targetCache.TryGetValue(game, out var gameCache))
            {
                // We are now attempting first load; set the list
                gameCache = new List<string>();
                _targetCache[game] = gameCache;

                // Populate the cache from the file
                var cacheFile = M3Filesystem.GetCachedTargetsFile(game);
                if (File.Exists(cacheFile))
                {
                    gameCache.AddRange(M3Utilities.WriteSafeReadAllLines(cacheFile).Distinct());
                }
            }

            // Enumerate the cached targets and validate them, skipping any that are in existingTargets
            var targets = new List<TargetCacheInfo>();
            foreach (var gameDir in gameCache)
            {
                // If existing targets are provided, skip any that match the current gameDir
                if (existingTargets != null && existingTargets.Any(x => x.TargetPath.Equals(gameDir, StringComparison.InvariantCultureIgnoreCase)))
                {
                    continue; //don't try to load an existing target
                }

                //Validate game directory
                if (Directory.Exists(gameDir))
                {
                    var target = new GameTargetWPF(game, gameDir, false);
                    var failureReason = target.ValidateTarget();
                    if (failureReason == null)
                    {
                        targets.Add(new TargetCacheInfo(game, gameDir, true, null, target));
                    }
                    else
                    {
                        M3Log.Error($@"Cached target for {target.Game} is invalid: {failureReason}");
                        failedTargets.Add(new TargetCacheInfo(game, gameDir, false, failureReason, null));
                    }
                }
                else
                {
                    M3Log.Warning($@"Cached target directory does not exist, skipping: {gameDir}");
                    failedTargets.Add(new TargetCacheInfo(game, gameDir, false, "Invalid target: Directory does not exist", null));
                }
            }

            return targets;
        }

        /// <summary>
        /// Gets all cached target information for all games, including failed targets
        /// </summary>
        /// <returns>List of all cached target information</returns>
        internal static List<TargetCacheInfo> GetAllCachedTargetInfo()
        {
            var allTargetInfo = new List<TargetCacheInfo>();

            foreach (MEGame game in Enum.GetValues(typeof(MEGame)))
            {
                if (game == MEGame.Unknown) continue;
                if (game == MEGame.UDK) continue;

                var gameCache = GetCachedTargets(game, null, out var failedInfo);
                allTargetInfo.AddRange(gameCache);
                allTargetInfo.AddRange(failedInfo);
            }

            return allTargetInfo;
        }


        /// <summary>
        /// Adds a game target to the cached targets file for its game.
        /// Attempts retry logic if the initial write fails.
        /// </summary>
        /// <param name="target">The game target to add to the cache.</param>
        internal static void AddCachedTarget(GameTarget target)
        {
            // Ensure loaded, populated
            GetCachedTargets(target.Game, null, out _);
            var pathList = _targetCache[target.Game];

            var path = Path.GetFullPath(target.TargetPath); //standardize
            if (pathList.Contains(path, StringComparer.InvariantCultureIgnoreCase))
            {
                // Already in the cache
                return;
            }

            pathList.Add(path); // This also inserts into the in-memory cache

            // Write to disk cache
            var cachefile = M3Filesystem.GetCachedTargetsFile(target.Game);
            try
            {
                M3Log.Information($@"Saving new entry into targets cache for {target.Game}: {path}");
                try
                {
                    File.WriteAllLines(cachefile, pathList);
                }
                catch (Exception)
                {
                    Thread.Sleep(300);
                    try
                    {
                        File.WriteAllLines(cachefile, pathList);
                    }
                    catch (Exception ex)
                    {
                        M3Log.Error($@"Could not save cached targets on retry: {ex.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                M3Log.Error($@"Unable to add cached target: {e.Message}");
            }
        }

        /// <summary>
        /// Removes a game target from the cached targets for its game.
        /// </summary>
        /// <param name="target">The game target to remove from the cache.</param>
        internal static void RemoveCachedTarget(GameTarget target)
        {
            RemoveCachedTarget(target.Game, target.TargetPath);
        }

        /// <summary>
        /// Removes a cached target by game and path
        /// </summary>
        /// <param name="game">The game the target belongs to</param>
        /// <param name="targetPath">The path to the target to remove</param>
        internal static void RemoveCachedTarget(MEGame game, string targetPath)
        {
            // Ensure loaded, populated
            GetCachedTargets(game, null, out _);
            var pathList = _targetCache[game];

            var path = Path.GetFullPath(targetPath); //standardize
            if (!pathList.Contains(path, StringComparer.InvariantCultureIgnoreCase))
            {
                // Already not in the cache
                return;
            }

            var cachefile = M3Filesystem.GetCachedTargetsFile(game);
            if (!File.Exists(cachefile)) return; //can't do anything.

            int numRemoved = pathList.RemoveAll(x => string.Equals(path, x, StringComparison.InvariantCultureIgnoreCase));
            if (numRemoved > 0)
            {
                try
                {
                    File.WriteAllLines(cachefile, pathList);
                    M3Log.Information($@"Removed {numRemoved} targets matching {path}");
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Unable to remove cached target from disk cache: {e.Message}");
                }
            }
        }
    }
}
