using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.ui;
using Microsoft.Win32;
using SevenZip;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for UpdateCompletedPanel.xaml
    /// </summary>
    public partial class TestArchiveGenerator : MMBusyPanelBase
    {
        public int Percent { get; private set; }
        public string ActionText { get; private set; }
        public Mod ModForArchive { get; private set; }

        public TestArchiveGenerator(Mod mod)
        {
            DataContext = this;
            ModForArchive = mod;
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = $@"{M3L.GetString(M3L.string_7zipArchiveFile)}|*.7z",
                FileName = M3Utilities.SanitizePath($@"{ModForArchive.ModName}_{ModForArchive.ModVersionString}".Replace(@" ", ""), true)
            };
            var outputarchive = d.ShowDialog();
            if (outputarchive.HasValue && outputarchive.Value)
            {
                var nbw = new NamedBackgroundWorker(@"TestArchiveGenerator");
                nbw.DoWork += (a, b) =>
                {
                    var stagingPath = Directory.CreateDirectory(Path.Combine(M3Utilities.GetTempPath(), @"TestGenerator")).FullName;
                    var referencedFiles = ModForArchive.GetAllRelativeReferences();
                    int numdone = 0;
                    ActionText = M3L.GetString(M3L.string_hashingFiles);

                    Parallel.ForEach(referencedFiles, new ParallelOptions() { MaxDegreeOfParallelism = 3 }, (x) =>
                      {
                          var sourcefile = Path.Combine(ModForArchive.ModPath, x);
                          var destfile = Path.Combine(stagingPath, x);

                          M3Log.Information(@"Hashing " + sourcefile);
                          var md5 = M3Utilities.CalculateMD5(sourcefile);
                          Directory.CreateDirectory(Directory.GetParent(destfile).FullName);

                          if (Path.GetExtension(destfile) == @".m3m")
                          {
                              File.Copy(sourcefile, destfile);
                          }
                          else
                          {
                              M3Log.Information(@"Writing blank hash file " + destfile);
                              File.WriteAllText(destfile, md5);
                          }


                          var done = Interlocked.Increment(ref numdone);
                          Percent = (int)(done * 100.0 / referencedFiles.Count);
                      });
                    M3Log.Information(@"Copying moddesc.ini");
                    File.Copy(ModForArchive.ModDescPath, Path.Combine(stagingPath, @"moddesc.ini"), true);
                    Mod testmod = new Mod(Path.Combine(stagingPath, @"moddesc.ini"), MEGame.Unknown);
                    if (testmod.ValidMod)
                    {
                        ActionText = M3L.GetString(M3L.string_creatingArchive);

                        SevenZipCompressor svc = new SevenZipCompressor();
                        svc.Progressing += (o, details) => { Percent = (int)(details.AmountCompleted * 100.0 / details.TotalAmount); };
                        svc.CompressDirectory(stagingPath, d.FileName);
                        M3Utilities.HighlightInExplorer(d.FileName);
                    }
                };
                nbw.RunWorkerCompleted += (a, b) =>
                {
                    if (b.Error != null)
                    {
                        M3Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                    }
                    OnClosing(DataEventArgs.Empty);
                };
                nbw.RunWorkerAsync();
            }
            else
            {
                OnClosing(DataEventArgs.Empty);
            }
        }


    }
}
