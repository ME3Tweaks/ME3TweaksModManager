using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;
using Microsoft.Win32;
using Serilog;
using SevenZip;


namespace MassEffectModManagerCore.modmanager.usercontrols
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
            InitializeComponent();
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "7-zip archive file|*.7z",
                FileName = Utilities.SanitizePath($@"{ModForArchive.ModName}_{ModForArchive.ModVersionString}".Replace(@" ", ""), true)
            };
            var outputarchive = d.ShowDialog();
            if (outputarchive.HasValue && outputarchive.Value)
            {
                var bw = new NamedBackgroundWorker("TestArchiveGenerator");
                bw.DoWork += (a, b) =>
                {
                    var stagingPath = Directory.CreateDirectory(Path.Combine(Utilities.GetTempPath(), "TestGenerator")).FullName;
                    var referencedFiles = ModForArchive.GetAllRelativeReferences();
                    int numdone = 0;
                    ActionText = "Hashing files";
                    Parallel.ForEach(referencedFiles, new ParallelOptions() { MaxDegreeOfParallelism = 3 }, (x) =>
                      {
                          var sourcefile = Path.Combine(ModForArchive.ModPath, x);
                          var destfile = Path.Combine(stagingPath, x);

                          Log.Information(@"Hashing " + sourcefile);
                          var md5 = Utilities.CalculateMD5(sourcefile);
                          Directory.CreateDirectory(Directory.GetParent(destfile).FullName);
                          Log.Information(@"Writing blank hash file " + destfile);
                          File.WriteAllText(destfile, md5);


                          var done = Interlocked.Increment(ref numdone);
                          Percent = (int)(done * 100.0 / referencedFiles.Count);
                      });
                    Log.Information(@"Copying moddesc.ini");
                    File.Copy(ModForArchive.ModDescPath, Path.Combine(stagingPath, @"moddesc.ini"), true);
                    Mod testmod = new Mod(Path.Combine(stagingPath, @"moddesc.ini"), Mod.MEGame.Unknown);
                    if (testmod.ValidMod)
                    {
                        ActionText = "Creating archive";
                        SevenZipCompressor svc = new SevenZipCompressor();
                        svc.Progressing += (o, details) => { Percent = (int)(details.AmountCompleted * 100.0 / details.TotalAmount); };
                        svc.CompressDirectory(stagingPath, d.FileName);
                        Utilities.HighlightInExplorer(d.FileName);
                    }
                };
                bw.RunWorkerCompleted += (a, b) => { OnClosing(DataEventArgs.Empty); };
                bw.RunWorkerAsync();
            }
            else
            {
                OnClosing(DataEventArgs.Empty);
            }
        }


    }
}
