using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.deployment.checks
{
    internal static class TextureChecks
    {
        /// <summary>
        /// Adds texture checks to the encompassing mod deployment checks.
        /// </summary>
        /// <param name="check"></param>
        public static void AddTextureChecks(EncompassingModDeploymentCheck check)
        {
            check.DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = M3L.GetString(M3L.string_texturesCheck),
                ModToValidateAgainst = check.ModBeingDeployed,
                DialogMessage = M3L.GetString(M3L.string_texturesCheckDetectedErrors),
                DialogTitle = M3L.GetString(M3L.string_textureErrorsInMod),
                ValidationFunction = CheckTextures
            });
        }

        /// <summary>
        /// Checks texture references and known bad texture setups
        /// </summary>
        /// <param name="item"></param>
        private static void CheckTextures(DeploymentChecklistItem item)
        {
            // if (item.ModToValidateAgainst.Game >= MEGame.ME2)
            //{

            // LE1: TFC in basegame
            if (item.ModToValidateAgainst.Game == MEGame.LE1)
            {
                var installableFiles = item.ModToValidateAgainst.GetAllInstallableFiles();
                var basegameTFCs = installableFiles.Where(x => x.Replace('/', '\\').TrimStart('\\').StartsWith(@"BioGame\CookedPCConsole\", StringComparison.InvariantCultureIgnoreCase) && x.EndsWith(".tfc")).ToList();
                foreach (var basegameTFC in basegameTFCs)
                {
                    M3Log.Error($@"Found basegame TFC being deployed for LE1: {basegameTFC}");
                    item.AddBlockingError($"Cannot install TFC {Path.GetFileName(basegameTFC)} to /BIOGame/CookedPCConsole in LE1. Additional game TFCs must be added through a Custom DLC folder.");
                }
            }

            // CHECK REFERENCES
            item.ItemText = M3L.GetString(M3L.string_checkingTexturesInMod);
            var referencedFiles = item.ModToValidateAgainst.GetAllRelativeReferences().Select(x => Path.Combine(item.ModToValidateAgainst.ModPath, x)).ToList();
            var allTFCs = referencedFiles.Where(x => Path.GetExtension(x) == @".tfc").ToList();
            int numChecked = 0;
            foreach (var f in referencedFiles)
            {
                if (item.CheckDone) return;
                numChecked++;
                item.ItemText = $@"{M3L.GetString(M3L.string_checkingTexturesInMod)} [{numChecked}/{referencedFiles.Count}]";
                if (f.RepresentsPackageFilePath())
                {
                    var relativePath = f.Substring(item.ModToValidateAgainst.ModPath.Length + 1);
                    M3Log.Information(@"Checking file for broken textures: " + f);
                    var package = MEPackageHandler.OpenMEPackage(f);
                    if (package.Game != item.ModToValidateAgainst.Game)
                        continue; // Don't bother checking this
                    var textures = package.Exports.Where(x => x.IsTexture() && !x.IsDefaultObject).ToList();
                    foreach (var texture in textures)
                    {
                        if (item.CheckDone) return;

                        if (package.Game > MEGame.ME1)
                        {
                            // CHECK NEVERSTREAM
                            // 1. Has more than six mips.
                            // 2. Has no external mips.
                            Texture2D tex = new Texture2D(texture);

                            var topMip = tex.GetTopMip();
                            if (topMip.storageType == StorageTypes.pccUnc)
                            {
                                // It's an internally stored texture
                                if (!tex.NeverStream && tex.Mips.Count(x => x.storageType != StorageTypes.empty) > 6)
                                {
                                    // NEVERSTREAM SHOULD HAVE BEEN SET.
                                    M3Log.Error(@"Found texture missing 'NeverStream' attribute " + texture.InstancedFullPath);
                                    item.AddBlockingError(M3L.GetString(M3L.string_interp_fatalMissingNeverstreamFlag, relativePath, texture.UIndex, texture.InstancedFullPath));
                                }
                            }

                            if (package.Game == MEGame.ME3) // ME3 only. does not affect LE3
                            {
                                // CHECK FOR 4K NORM
                                var compressionSettings = texture.GetProperty<EnumProperty>(@"CompressionSettings");
                                if (compressionSettings != null && compressionSettings.Value == @"TC_NormalMapUncompressed")
                                {
                                    var mipTailBaseIdx = texture.GetProperty<IntProperty>(@"MipTailBaseIdx");
                                    if (mipTailBaseIdx != null && mipTailBaseIdx == 12)
                                    {
                                        // It's 4K (2^12)
                                        M3Log.Error(@"Found 4K Norm. These are not used by game (they use up to 1 mip below the diff) and waste large amounts of memory. Drop the top mip to correct this issue. " + texture.InstancedFullPath);
                                        item.AddBlockingError(M3L.GetString(M3L.string_interp_fatalFound4KNorm, relativePath, texture.UIndex, texture.InstancedFullPath));
                                    }
                                }
                            }


                            var cache = texture.GetProperty<NameProperty>(@"TextureFileCacheName");
                            if (cache != null)
                            {
                                if (!VanillaDatabaseService.IsBasegameTFCName(cache.Value, item.ModToValidateAgainst.Game))
                                {
                                    //var mips = Texture2D.GetTexture2DMipInfos(texture, cache.Value);
                                    try
                                    {
                                        tex.GetImageBytesForMip(tex.GetTopMip(), item.internalValidationTarget.Game, false, out _, item.internalValidationTarget.TargetPath, allTFCs); //use active target
                                    }
                                    catch (Exception e)
                                    {
                                        M3Log.Warning(@"Found broken texture: " + texture.InstancedFullPath);
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_couldNotLoadTextureData, relativePath, texture.InstancedFullPath, e.Message));
                                    }
                                }

                                if (cache.Value.Name.Contains(@"CustTextures"))
                                {
                                    // ME3Explorer 3.0 or below Texplorer
                                    item.AddSignificantIssue(M3L.GetString(M3L.string_interp_error_foundCustTexturesTFCRef, relativePath, texture.InstancedFullPath, cache.Value.Name));
                                }
                                else if (cache.Value.Name.Contains(@"TexturesMEM"))
                                {
                                    // Textures replaced by MEM. This is not allowed in mods as it'll immediately be broken
                                    item.AddBlockingError(M3L.GetString(M3L.string_interp_error_foundTexturesMEMTFCRef, relativePath, texture.InstancedFullPath, cache.Value.Name));
                                }
                            }
                        }
                        else
                        {
                            Texture2D tex = new Texture2D(texture);
                            var cachename = tex.GetTopMip().TextureCacheName;
                            if (cachename != null)
                            {
                                foreach (var mip in tex.Mips)
                                {
                                    try
                                    {
                                        tex.GetImageBytesForMip(mip, item.internalValidationTarget.Game, false, out _, item.internalValidationTarget.TargetPath);
                                    }
                                    catch (Exception e)
                                    {
                                        M3Log.Warning(@"Found broken texture: " + texture.InstancedFullPath);
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_couldNotLoadTextureData, relativePath, texture.InstancedFullPath, e.Message));
                                    }
                                }
                            }
                        }
                    }
                }
            }



            if (!item.HasAnyMessages())
            {
                item.ItemText = M3L.GetString(M3L.string_noBrokenTexturesWereFound);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.ItemText = M3L.GetString(M3L.string_textureIssuesWereDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationFailed);
            }
        }
    }
}
