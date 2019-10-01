using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MassEffectModManager;
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
