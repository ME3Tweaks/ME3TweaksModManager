using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace MassEffectModManagerCore.modmanager.objects
{
    public class ReadOnlyOption : INotifyPropertyChanged
    {
        public string Description { get; } = "Sets the configuration files this mod installs to read-only to prevent Mass Effect from resetting them. In-game options that modify these files will not persist across play sessions.";
        public bool IsSelected { get; set; }
        public string FriendlyName { get; } = "Make config files read-only";

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
