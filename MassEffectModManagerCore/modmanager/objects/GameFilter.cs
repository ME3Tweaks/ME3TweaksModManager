using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.objects
{
    public class GameFilter : INotifyPropertyChanged
    {
        public GameFilter(MEGame game)
        {
            Game = game;
            IsEnabled = true;
        }

        public MEGame Game { get; }
        public bool IsEnabled { get; set; }
        public bool IsVisible => Game.IsEnabledGeneration();

        public void NotifyGenerationChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Settings.GenerationSettingOT) or nameof(Settings.GenerationSettingLE))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
            }
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
