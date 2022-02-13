using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.helpers;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// UI object used for filtering 
    /// </summary>
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

    [AddINotifyPropertyChangedInterface]
    public class GameFilterLoader : GameFilter
    {
        private static SolidColorBrush loadingBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x00, 0xFF, 0x44));
        public SolidColorBrush BackgroundColor => IsLoading ? loadingBrush : Brushes.Transparent;
        
        [AlsoNotifyFor(nameof(BackgroundColor))]
        public bool IsLoading { get; set; }

        public GameFilterLoader(MEGame game) : base(game)
        {

        }
    }
}
