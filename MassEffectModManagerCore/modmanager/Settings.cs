using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace MassEffectModManagerCore.modmanager
{
    public class Settings : INotifyPropertyChanged
    {
        public static bool LogModStartup { get; set; }
        public static string ModLibraryPath { get; internal set; }
        public static DateTime LastContentCheck { get; internal set; }

        public static void Save()
        {
            //implement later
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
