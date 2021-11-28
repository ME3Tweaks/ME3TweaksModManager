using System;
using System.IO;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.diagnostics;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// Package class that is tied to a specific directory root (supporting relative lookups)
    /// </summary>
    public class RelativePackageCache : PackageCache
    {
        /// <summary>
        /// Root path of a game that can be used to look up relative paths
        /// </summary>
        public string RootPath { get; init; }

        /// <summary>
        /// Thread-safe package cache fetch. Accepts relative package paths if gameRootPath is set. Can be passed to various methods to help expedite operations by preventing package reopening. Packages opened with this method do not use the global LegendaryExplorerCore caching system and will always load from disk if not in this local cache.
        /// </summary>
        /// <param name="packagePath"></param>
        /// <param name="openIfNotInCache">Open the specified package if it is not in the cache, and add it to the cache</param>
        /// <returns></returns>
        public override IMEPackage GetCachedPackage(string packagePath, bool openIfNotInCache = true)
        {
            // Cannot look up null paths
            if (packagePath == null)
                return null;

            // May need way to set maximum size of dictionary so we don't hold onto too much memory.
            lock (syncObj)
            {
                if (Cache.TryGetValue(packagePath, out var package))
                {
                    //Debug.WriteLine($@"PackageCache hit: {packagePath}");
                    return package;
                }

                // Relative path (struct lookup)
                if (RootPath != null)
                {
                    try
                    {
                        if (Cache.TryGetValue(Path.Combine(RootPath, packagePath), out var relPackage))
                        {
                            //Log.Information($@"RelativePackageCache hit: {packagePath}");
                            return relPackage;
                        }
                    }
                    catch (Exception e)
                    {
                        // in case two full paths are tried to be set this will probably throw invalid path exception.
                        M3Log.Error($@"Error combining paths: {RootPath}, {packagePath}");
                    }
                }

                if (openIfNotInCache)
                {
                    if (File.Exists(packagePath))
                    {
                        //Log.Information($@"RelativePackageCache load: {packagePath}");
                        package = MEPackageHandler.OpenMEPackage(packagePath, forceLoadFromDisk: true);
                        Cache[packagePath] = package;
                        return package;
                    }

                    if (RootPath != null)
                    {
                        try
                        {
                            packagePath = Path.Combine(RootPath, packagePath);
                            if (File.Exists(packagePath))
                            {
                                //Log.Information($@"RelativePackageCache load: {packagePath}");
                                package = MEPackageHandler.OpenMEPackage(packagePath, forceLoadFromDisk: true);
                                Cache[packagePath] = package;
                                return package;
                            }
                        }
                        catch (Exception e)
                        {
                            // in case two full paths are tried to be set this will probably throw invalid path exception.
                            M3Log.Error($@"Error combining paths: {RootPath}, {packagePath}");
                        }
                    }

                    //Log.Warning($@"RelativePackageCache miss: File not found in RPC, RPC root: {RootPath}, path: {packagePath}");
                }
            }

            return null; //Package could not be found
        }
    }
}
