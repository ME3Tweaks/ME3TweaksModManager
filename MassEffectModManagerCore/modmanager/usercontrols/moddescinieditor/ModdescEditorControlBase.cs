using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Controls;
using IniParser.Model;
using IniParser.Parser;
using MassEffectModManagerCore.modmanager.objects.mod;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    public abstract class ModdescEditorControlBase : UserControl, INotifyPropertyChanged
    {
        public Mod EditingMod { get; set; }

        public virtual void OnEditingModChanged(Mod newMod)
        {
            EditingMod = newMod;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public abstract void Serialize(IniData ini);
    }
}
