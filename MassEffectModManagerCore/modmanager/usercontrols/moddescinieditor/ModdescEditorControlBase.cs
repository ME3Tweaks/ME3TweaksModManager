using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using IniParser.Model;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.windows;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
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
#pragma warning disable
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
        public abstract void Serialize(IniData ini);
    }
}
