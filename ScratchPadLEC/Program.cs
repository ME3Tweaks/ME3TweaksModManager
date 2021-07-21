using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;

namespace ScratchPadLEC
{
    class Program
    {
        static void Main(string[] args)
        {
            LegendaryExplorerCoreLib.InitLib(TaskScheduler.Current);
            MEPackageHandler.GlobalSharedCacheEnabled = false;

            // Scratch projects here.
            var package = MEPackageHandler.OpenMEPackage(@"C:\users\mgamerz\desktop\BioD_Nor_100Cabin.pcc");

            var size1 = package.Imports[0].Header.Length;
            foreach (var v in package.Imports)
            {
                if (v.Header.Length != size1)
                    Debugger.Break();
            }

            var streamSaved = package.SaveToStream(true);
            streamSaved.Position = 0;
            var p2 = MEPackageHandler.OpenMEPackageFromStream(streamSaved);
            Console.WriteLine("Hello World!");
        }
    }
}
