using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MassEffectModManagerCore.Tests
{
    [TestClass]
    public class UITesting
    {
        [TestMethod]
        public void InitialUILoadTest()
        {
            GlobalTest.Init();
            App app = new App(); //Pre boot

            var xamlPaths = Path.Combine(GlobalTest.FindDirectoryInParentDirectories("MassEffectModManagerCore"), "modmanager", "localizations");
            var files = Directory.GetFiles(xamlPaths, "*.xaml");
            foreach (var filepath in files)
            {
                // Will throw exception if there's an error loading the file
                Console.WriteLine($@"Loading localization file {filepath}");
                XamlReader.Load(new XmlTextReader(filepath));
            }

            //Thread thread = new Thread(() =>
            //{
            //    app.InitializeComponent();
            //    app.Run();
            //});
            //thread.SetApartmentState(ApartmentState.STA);
            //thread.Start();
            //thread.Join();
            //var shutdownTimer = new System.Timers.Timer();
            //shutdownTimer.Interval = 15000;
            //shutdownTimer.Elapsed += (o, e) =>
            //{
            //    app.Shutdown();
            //    app = null;

            //};

            //shutdownTimer.Start();
            //while (app != null)
            //{
            //    Thread.Sleep(1000);
            //}
        }
    }
}
