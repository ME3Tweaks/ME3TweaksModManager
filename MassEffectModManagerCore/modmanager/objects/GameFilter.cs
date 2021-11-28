using System.ComponentModel;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.helpers;

namespace ME3TweaksModManager.modmanager.objects
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
