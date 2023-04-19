using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.importer;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.installer;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using ME3TweaksModManager.modmanager.usercontrols;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SevenZip;
using Mod = ME3TweaksModManager.modmanager.objects.mod.Mod;

namespace ME3TweaksModManager.Tests
{
    [TestClass]

    public class ModExtractionTests
    {
        //This method just runs some code to make sure no exceptions are hit.
        //In future hopefully this will have code to actually check results.
        [TestMethod]
        public void TestBuildingExtractionQueues()
        {
            GlobalTest.Init();

            Console.WriteLine("Fetching third party services");
            TPMIService.LoadService(GlobalTest.CombinedServiceData);
            TPIService.LoadService(GlobalTest.CombinedServiceData);
            //throw new Exception("TPISService not implemented!");

            //App.ThirdPartyIdentificationService = OnlineContent.FetchThirdPartyIdentificationManifest();

            var compressedModsDirectory = GlobalTest.GetTestingDataDirectoryFor(@"compressedmods");
            List<Mod> modsFoundInArchive = new List<Mod>();

            void addModCallback(Mod m)
            {
                Console.WriteLine($"Found mod in archive: {m.ModName}");
                modsFoundInArchive.Add(m);
            }

            void addTextureMod(MEMMod m)
            {
                Console.WriteLine($"Found texture mod in archive: {m.ModName}");
                // Todo
            }

            void failedModCallback(Mod m)
            {
                //Console.WriteLine($"A mod failed to load. This may be expected: {m.ModName}");
            }

            void logMessageCallback(string m)
            {
                Console.WriteLine(m);
            }

            #region Get Targets
            var targets = new List<GameTargetWPF>();
            var root = GlobalTest.GetTestGameFoldersDirectory(MEGame.ME1);
            foreach (var d in Directory.GetDirectories(root))
            {
                var gt = new GameTargetWPF(MEGame.ME1, d, false, false);
                gt.ValidateTarget();
                if (gt.IsValid)
                {
                    targets.Add(gt);
                }
            }
            root = GlobalTest.GetTestGameFoldersDirectory(MEGame.ME2);
            foreach (var d in Directory.GetDirectories(root))
            {
                var gt = new GameTargetWPF(MEGame.ME2, d, false, false);
                gt.ValidateTarget();
                if (gt.IsValid)
                {
                    targets.Add(gt);
                }
            }
            root = GlobalTest.GetTestGameFoldersDirectory(MEGame.ME3);
            foreach (var d in Directory.GetDirectories(root))
            {
                var gt = new GameTargetWPF(MEGame.ME3, d, false, false);
                gt.ValidateTarget();
                if (gt.IsValid)
                {
                    targets.Add(gt);
                }
            }
            #endregion

            //Compressed Mods

            foreach (var archive in Directory.GetFiles(compressedModsDirectory))
            {
                modsFoundInArchive.Clear();
                var realArchiveInfo = GlobalTest.ParseRealArchiveAttributes(archive);
                Console.WriteLine($@"Inspecting archive: { archive}");
                ModImport.FindModsInArchive(archive, addModCallback, failedModCallback, addTextureMod, logMessageCallback, forcedMD5: realArchiveInfo.md5, forcedSize: realArchiveInfo.size);
                var archiveZ = new SevenZipExtractor(archive);
                foreach (var mod in modsFoundInArchive)
                {
                    if (!mod.ValidMod)
                    {
                        Console.WriteLine($@"Skipping invalid mod: {mod.ModName} at {mod.ModDescPath}");
                        continue;
                    }
                    bool altsOn = false;
                    while (true)
                    {
                        if (altsOn)
                        {
                            foreach (var job in mod.InstallationJobs)
                            {
                                List<string> selectedGroups = new List<string>();
                                foreach (var altfile in job.AlternateFiles)
                                {
                                    if (altfile.GroupName != null)
                                    {
                                        if (selectedGroups.Contains(altfile.GroupName))
                                        {
                                            continue; //we already did first time of this. I know that's a weak test case...
                                        }
                                        selectedGroups.Add(altfile.GroupName);
                                    }

                                    altfile.UIIsSelected = true;
                                }
                            }
                        }

                        var refs = mod.GetAllRelativeReferences(!mod.IsVirtualized, archiveZ); //test
                        //validate references are actually in this archive
                        foreach (var fileREf in refs)
                        {
                            var expectedPath = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, fileREf);
                            //var expectedPath = fileREf;
                            var inArchiveFile = archiveZ.ArchiveFileData.FirstOrDefault(x => x.FileName == expectedPath);
                            Assert.IsNotNull(inArchiveFile.FileName, "Relative referenced file was not found in archive: " + fileREf);
                        }

                        //size test
                        Assert.AreNotEqual(0, mod.SizeRequiredtoExtract, "Archive extraction size is zero! For file " + archive);

                        var targetsForMod = targets.Where(x => x.Game == mod.Game).ToList();
                        foreach (var target in targetsForMod)
                        {
                            var headerMapping = new Dictionary<ModJob.JobHeader, List<AlternateOption>>();
                            foreach (var job in mod.InstallationJobs)
                            {
                                headerMapping[job.Header] = new List<AlternateOption>();
                            }
                            ModInstallOptionsPackage package = new ModInstallOptionsPackage()
                            {
                                ModBeingInstalled = mod,
                                InstallTarget = target,
                                SelectedOptions = headerMapping, // Blank
                            };
                            var queue = mod.GetInstallationQueues(package);
                            foreach (var jobMapping in queue.UnpackedJobMappings)
                            {
                                foreach (var unpackedItem in jobMapping.Value.FileMapping)
                                {
                                    string sourceFile;
                                    if (jobMapping.Key.JobDirectory == null || unpackedItem.Value.IsFullRelativeFilePath)
                                    {
                                        sourceFile = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, unpackedItem.Value.FilePath);
                                    }
                                    else
                                    {
                                        sourceFile = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, jobMapping.Key.JobDirectory, unpackedItem.Value.FilePath);
                                    }
                                    Assert.IsTrue(archiveZ.ArchiveFileNames.Contains(sourceFile), "Archive should contain a file specified by mod (mod is valid) but does not appear to. File: " + sourceFile);
                                }
                            }
                        }

                        if (!altsOn)
                        {
                            altsOn = true;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            //EXE mods
            var exeModsDirectory = GlobalTest.GetTestingDataDirectoryFor(@"exemods");

            if (Directory.Exists(exeModsDirectory))
            {
                foreach (var exe in Directory.GetFiles(exeModsDirectory))
                {
                    modsFoundInArchive.Clear();
                    //var realArchiveInfo = GlobalTest.ParseRealArchiveAttributes(exe);
                    Console.WriteLine($@"Inspecting exe: { exe}");
                    ModImport.FindModsInArchive(exe, addModCallback, failedModCallback, addTextureMod, logMessageCallback);
                    var archiveZ = new SevenZipExtractor(exe, InArchiveFormat.Nsis);
                    foreach (var mod in modsFoundInArchive)
                    {
                        if (!mod.ValidMod)
                        {
                            Console.WriteLine($@"Skipping invalid mod: {mod.ModName} at {mod.ModDescPath}");
                            continue;
                        }
                        foreach (var job in mod.InstallationJobs)
                        {
                            List<string> selectedGroups = new List<string>();
                            foreach (var altfile in job.AlternateFiles)
                            {
                                if (altfile.GroupName != null)
                                {
                                    if (selectedGroups.Contains(altfile.GroupName))
                                    {
                                        continue; //we already did first time of this. I know that's a weak test case...
                                    }
                                    selectedGroups.Add(altfile.GroupName);
                                }

                                altfile.UIIsSelected = true;
                            }
                        }


                        var refs = mod.GetAllRelativeReferences(false, archiveZ); //test and get refs. exe mods will always be virtualized as they won't have a moddesc.ini file.
                                                                                  //exe mods remap to subconetns
                                                                                  //same code as Mod-Extraction.cs
                        foreach (var fileREf in refs)
                        {
                            var expectedPath = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, fileREf);
                            //var expectedPath = fileREf;
                            var inArchiveFile = archiveZ.ArchiveFileData.FirstOrDefault(x => x.FileName == expectedPath);
                            Assert.IsNotNull(inArchiveFile.FileName, "Relative referenced file was not found in archive: " + fileREf);
                        }

                        mod.ExtractFromArchive(exe, "", false, testRun: true);
                    }
                }
            }
            else
            {
                Console.WriteLine("No exemods directory found. This section of testing will be skipped");
            }
        }
    }
}
