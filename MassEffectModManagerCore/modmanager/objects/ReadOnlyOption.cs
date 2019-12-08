using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using MassEffectModManagerCore.modmanager.localizations;

namespace MassEffectModManagerCore.modmanager.objects
{
    public class ReadOnlyOption : INotifyPropertyChanged
    {
        public string Description { get; } = M3L.GetString(M3L.string_descriptionSetConfigFilesReadOnly);
        public bool IsSelected { get; set; }
        public string FriendlyName { get; } = M3L.GetString(M3L.string_makeConfigFilesReadonly);

        public event PropertyChangedEventHandler PropertyChanged;
    }
}