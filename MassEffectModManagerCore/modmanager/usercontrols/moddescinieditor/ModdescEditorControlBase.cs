using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using IniParser.Model;
using IniParser.Parser;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor.alternates;
using MassEffectModManagerCore.modmanager.windows;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    public abstract class ModdescEditorControlBase : UserControl, INotifyPropertyChanged
    {

        private Mod _mod;
        public Mod EditingMod
        {
            get
            {
                if (_mod != null) return _mod;
                var window = Window.GetWindow(this);
                if (window is ModDescEditor mde)
                {
                    _mod = mde.EditingMod;
                }

                return _mod;
            }
        }

        /// <summary>
        /// If the control has initialized
        /// </summary>
        public bool HasLoaded;

        public ModdescEditorControlBase()
        {
            Loaded += OnLoaded;
        }

        public abstract void OnLoaded(object sender, RoutedEventArgs e);

        //Fody uses this property on weaving
#pragma warning disable 67
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 67
        public abstract void Serialize(IniData ini);
    }
}
