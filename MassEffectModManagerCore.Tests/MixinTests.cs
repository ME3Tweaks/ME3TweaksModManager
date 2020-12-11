using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using ME3ExplorerCore.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MassEffectModManagerCore.Tests
{
    [TestClass]
    public class MixinTests
    {
        [TestMethod]
        public void TestMixins()
        {
            GlobalTest.Init();

            var me3BackupPath = BackupService.GetGameBackupPath(MEGame.ME3);
            if (me3BackupPath != null)
            {
                GlobalTest.CreateScratchDir();
                MixinHandler.LoadME3TweaksPackage();
                // We can conduct this test
                var mixins = MixinHandler.ME3TweaksPackageMixins.Where(x => !x.IsFinalizer).ToList();
                MixinHandler.LoadPatchDataForMixins(mixins);

                List<string> failedMixins = new List<string>();
                void failedApplicationCallback(string str)
                {
                    failedMixins.Add(str);
                }
                var compilingListsPerModule = MixinHandler.GetMixinApplicationList(mixins, failedApplicationCallback);

                //Mixins are ready to be applied
                foreach (var mapping in compilingListsPerModule)
                {
                    var dlcFolderName = ModMakerCompiler.ModmakerChunkNameToDLCFoldername(mapping.Key.ToString());
                    var outdir = Path.Combine(Path.Combine(GlobalTest.GetScratchDir(), "MixinTest"), ModMakerCompiler.HeaderToDefaultFoldername(mapping.Key), @"CookedPCConsole");
                    Directory.CreateDirectory(outdir);
                    if (mapping.Key == ModJob.JobHeader.BASEGAME)
                    {
                        //basegame
                        foreach (var file in mapping.Value)
                        {
                            try
                            {
                                using var packageAsStream = VanillaDatabaseService.FetchBasegameFile(MEGame.ME3, Path.GetFileName(file.Key));
                                using var decompressedStream = MEPackage.GetDecompressedPackageStream(packageAsStream, true);
                                using var finalStream = MixinHandler.ApplyMixins(decompressedStream, file.Value, true, null, failedApplicationCallback);
                                CLog.Information(@"Compressing package to mod directory: " + file.Key, Settings.LogModMakerCompiler);
                                finalStream.Position = 0;
                                var package = MEPackageHandler.OpenMEPackageFromStream(finalStream);
                                var outfile = Path.Combine(outdir, Path.GetFileName(file.Key));
                                package.Save(outfile, true); // don't compress
                            }
                            catch (Exception e)
                            {
                                var mixinsStr = string.Join(@", ", file.Value.Select(x => x.PatchName));
                                failedApplicationCallback($@"{file.Key} Error applying mixin {mixinsStr}: {e.Message}");
                            }
                        }
                    }
                    else
                    {
                        //dlc
                        var dlcPackage = VanillaDatabaseService.FetchVanillaSFAR(dlcFolderName); //do not have to open file multiple times.
                        foreach (var file in mapping.Value)
                        {
                            try
                            {
                                using var packageAsStream = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcFolderName, file.Key, forcedDLC: dlcPackage);
                                using var finalStream = MixinHandler.ApplyMixins(packageAsStream, file.Value, true, null, failedApplicationCallback);
                                CLog.Information(@"Compressing package to mod directory: " + file.Key, Settings.LogModMakerCompiler);
                                finalStream.Position = 0;
                                var package = MEPackageHandler.OpenMEPackageFromStream(finalStream);
                                var outfile = Path.Combine(outdir, Path.GetFileName(file.Key));
                                package.Save(outfile, true);
                            }
                            catch (Exception e)
                            {
                                var mixinsStr = string.Join(@", ", file.Value.Select(x => x.PatchName));
                                failedApplicationCallback($@"{file.Key} Error applying mixin {mixinsStr}: {e.Message}");
                            }

                            //finalStream.WriteToFile(outfile);
                        }
                    }
                }
                MixinHandler.FreeME3TweaksPatchData();
                GlobalTest.DeleteScratchDir();
                if (failedMixins.Any())
                {
                    Assert.Fail($"MixinTests failed. {failedMixins.Count} mixins failed to apply.");
                }
            }
            else
            {
                Console.WriteLine(@"No backup for ME3 is available. MixinTests will be skipped.");
            }
        }
    }
}
