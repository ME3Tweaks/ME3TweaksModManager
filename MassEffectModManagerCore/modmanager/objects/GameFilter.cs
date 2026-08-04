using System.ComponentModel;
using System.Windows.Media;
using ME3TweaksModManager.modmanager.helpers;

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
        private static SolidColorBrush loadingBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x92, 0xA5, 0xF4));
        public SolidColorBrush BackgroundColor => IsLoading ? loadingBrush : Brushes.Transparent;

        [AlsoNotifyFor(nameof(BackgroundColor))]
        public bool IsLoading { get; set; }

        public GameFilterLoader(MEGame game) : base(game)
        {

        }
    }
}
