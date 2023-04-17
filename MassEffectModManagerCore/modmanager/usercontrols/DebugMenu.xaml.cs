using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.gamemd5;
using ME3TweaksModManager.modmanager.merge.dlc;
using ME3TweaksModManager.modmanager.merge.game2email;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.save;
using ME3TweaksModManager.modmanager.save.game2.UI;
using ME3TweaksModManager.modmanager.squadmates;
using SevenZip;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    public static class DebugMenu
    {
        public static void RouteDebugCall(string sender, MainWindow window)
        {
#if DEBUG
            if (sender == nameof(MainWindow.Test7z_MenuItem)) Test7z(window);
            if (sender == nameof(MainWindow.RebuildMD5DB_MenuItem)) RebuildMD5Map(window);
            if (sender == nameof(MainWindow.TestMMV1_MenuItem)) BuildMMV1MergeMod(window);
            if (sender == nameof(MainWindow.ReloadSelectedMod_MenuItem)) ReloadSelectedMod_Click(window);
            if (sender == nameof(MainWindow.DebugPrintInstallationQueue_MenuItem)) DebugPrintInstallationQueue_Click(window);
            if (sender == nameof(MainWindow.DebugPrintReferencedFiles_MenuItem)) DebugPrintReferencedFiles_Click(window);
            if (sender == nameof(MainWindow.ShowMEMDB_MenuItem)) ShowMEMViewer(window);
            if (sender == nameof(MainWindow.ShowBackupNag_MenuItem)) ShowBackupNag_Click(window);
            if (sender == nameof(MainWindow.TestCode_MenuItem)) TestCode_Click(window);
            if (sender == nameof(MainWindow.DebugSquadmateMerge_MenuItem)) TestSquadmateMerge_Click(window);
            if (sender == nameof(MainWindow.DebugEmailMerge_MenuItem)) TestEmailMerge_Click(window);
            if (sender == nameof(MainWindow.UpdateMD5DB_MenuItem)) UpdateMD5Map(window);
            if (sender == nameof(MainWindow.StampCurrentTargetWithALOTMarker_MenuItem)) StampCurrentTargetWithALOT_Click(window);
            if (sender == nameof(MainWindow.StripCurrentTargetWithALOTMarker_MenuItem)) StripCurrentTargetALOTMarker_Click(window);
            if (sender == nameof(MainWindow.ShowWelcomePanel_MenuItem)) ShowWelcomePanel_Click(window);
            if (sender == nameof(MainWindow.ShowBGFISDB_MenuItem)) ShowBGFISDB_Click(window);
#endif
        }

#if DEBUG
        private static void ShowBGFISDB_Click(MainWindow window)
        {
            var previewPanel = new BasegameFileIdentificationServicePanel();
            previewPanel.Close += (a, b) =>
            {
                window.ReleaseBusyControl();
            };
            window.ShowBusyControl(previewPanel);
        }

        private static void ShowWelcomePanel_Click(MainWindow window)
        {
            window.ShowFirstRunPanel();
        }

        private static void TestSquadmateMerge_Click(MainWindow window)
        {
            // Note this is mutual exclusive, for testing only!
            var target = window.SelectedGameTarget;
            M3MergeDLC dlc = new M3MergeDLC(target);
            M3MergeDLC.RemoveMergeDLC(target);
            dlc.GenerateMergeDLC();
            SQMOutfitMerge.RunGame3SquadmateOutfitMerge(dlc);
        }

        private static void TestEmailMerge_Click(MainWindow window)
        {
            // Note this is mutual exclusive, for testing only!
            var target = window.SelectedGameTarget;
            M3MergeDLC dlc = new M3MergeDLC(target);
            M3MergeDLC.RemoveMergeDLC(target);
            dlc.GenerateMergeDLC();
            ME2EmailMerge.RunGame2EmailMerge(dlc);
        }

        private static void ShowMEMViewer(MainWindow window)
        {
            var previewPanel = new MEMVanillaDBViewer();
            previewPanel.Close += (a, b) =>
            {
                window.ReleaseBusyControl();
            };
            window.ShowBusyControl(previewPanel);
        }

        private static void RebuildMD5Map(MainWindow window)
        {
            Task.Run(() =>
            {
                MD5Gen.GenerateMD5Map(MEGame.LE1, @"B:\SteamLibrary\steamapps\common\Mass Effect Legendary Edition\Game\ME1", @"le1.bin");
                MD5Gen.GenerateMD5Map(MEGame.LE2, @"B:\SteamLibrary\steamapps\common\Mass Effect Legendary Edition\Game\ME2", @"le2.bin");
                MD5Gen.GenerateMD5Map(MEGame.LE3, @"B:\SteamLibrary\steamapps\common\Mass Effect Legendary Edition\Game\ME3", @"le3.bin");
                Debug.WriteLine(@"Done");
            });
        }

        private static void UpdateMD5Map(MainWindow window)
        {
            Task.Run(() =>
            {
                MD5Gen.UpdateMD5Map(MEGame.LE1, @"B:\SteamLibrary\steamapps\common\Mass Effect Legendary Edition\Game\ME1", @"le1.bin");
                MD5Gen.UpdateMD5Map(MEGame.LE2, @"B:\SteamLibrary\steamapps\common\Mass Effect Legendary Edition\Game\ME2", @"le2.bin");
                MD5Gen.UpdateMD5Map(MEGame.LE3, @"B:\SteamLibrary\steamapps\common\Mass Effect Legendary Edition\Game\ME3", @"le3.bin");
                Debug.WriteLine(@"Done");
            });
        }

        private static void BuildMMV1MergeMod(MainWindow mw)
        {
            //SERIALIZER
            //var testfile = MergeModLoader.SerializeManifest(, 1);

            //// LOADER
            //using FileStream fs = File.OpenRead(testfile);
            //var mergeMod = MergeModLoader.LoadMergeMod(fs, "MMVV1.m3m", false);

            //var le2t = mw.GetCurrentTarget(MEGame.LE2);
            //mergeMod.ApplyMergeMod(null, le2t);
        }

        private static void Test7z(MainWindow window)
        {
            var compressor = new SevenZipCompressor();
            compressor.CustomParameters.Add(@"s", @"on");
            compressor.CustomParameters.Add(@"yx", @"9");
            compressor.CustomParameters.Add(@"d", @"28"); //Dictionary size 2^28 (256MB)
            compressor.CompressionMode = CompressionMode.Create; //Append to 

            var out7z = @"C:\Users\mgame\Desktop\BDTS Music\out.7z";
            compressor.CompressDirectory(@"C:\Users\mgame\Desktop\BDTS Music\BioD_Nor_104Comm", out7z);

            compressor.CustomParameters.Clear(); //remove custom params as it seems to force LZMA
            compressor.CustomParameters.Add(@"s", @"off");
            compressor.CompressionMode = CompressionMode.Append; //Append to 
            compressor.CompressionLevel = CompressionLevel.Ultra;
            compressor.CompressDirectory(@"C:\Users\mgame\Desktop\BDTS Music\loop", out7z);

            using var decompressor = new SevenZipExtractor(out7z);
            foreach (var f in decompressor.ArchiveFileData)
            {
                if (f.IsDirectory)
                    continue;
                Debug.WriteLine($@"Decompressing {f.FileName}");
                MemoryStream ms = new MemoryStream();
                decompressor.ExtractFile(f.Index, ms);
            }
        }

        private static void ReloadSelectedMod_Click(MainWindow mw)
        {
            Mod m = new Mod(mw.SelectedMod.ModDescPath, MEGame.Unknown);
        }


        private static void StampCurrentTargetWithALOT_Click(MainWindow mw)
        {
            if (mw.SelectedGameTarget != null)
            {
                mw.SelectedGameTarget.StampDebugALOTInfo();
                mw.SelectedGameTarget.ReloadGameTarget();
            }
        }

        private static void StripCurrentTargetALOTMarker_Click(MainWindow mw)
        {
            if (mw.SelectedGameTarget != null)
            {
                mw.SelectedGameTarget.StripALOTInfo();
                mw.SelectedGameTarget.ReloadGameTarget();
            }
        }

        private static void DebugPrintReferencedFiles_Click(MainWindow mw)
        {
            if (mw.SelectedMod != null)
            {
                var refed = mw.SelectedMod.GetAllRelativeReferences();
                Debug.WriteLine(@"Referenced files:");
                foreach (var refx in refed)
                {
                    Debug.WriteLine(refx);
                }
            }
        }

        private static void DebugPrintInstallationQueue_Click(MainWindow mw)
        {
            //if (mw.SelectedMod != null)
            //{
            //    var queues = mw.SelectedMod.GetInstallationQueues(mw.InstallationTargets.FirstOrDefault(x => x.Game == mw.SelectedMod.Game));
            //    Debug.WriteLine(@"Installation Queue:");
            //    foreach (var job in queues.Item1)
            //    {
            //        foreach (var file in job.Value.unpackedJobMapping)
            //        {
            //            Debug.WriteLine($@"[UNPACKED {job.Key.Header.ToString()}] {file.Value.FilePath} => {file.Key}");
            //        }
            //    }

            //    foreach (var job in queues.Item2)
            //    {
            //        foreach (var file in job.Item3)
            //        {
            //            Debug.WriteLine($@"[SFAR {job.job.Header.ToString()}] {file.Value.FilePath} => {file.Key}");
            //        }
            //    }
            //}
        }

        private static void ShowBackupNag_Click(MainWindow mw)
        {
            mw.ShowBackupNag();
        }

        private static void TestCode_Click(MainWindow mw)
        {

        }
#endif
    }
}