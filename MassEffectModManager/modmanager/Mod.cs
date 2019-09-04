using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManager.modmanager
{
    class Mod : INotifyPropertyChanged

    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string ModName { get; set; }
        public string ModDescription { get; set; }
        public string DisplayedModDescription { get; set; }
    }
}
