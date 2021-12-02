using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Targets;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for TargetSelector.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class TargetSelector : UserControl
    {
        #region ShowTextureInfo DP

        /// <summary>
        /// Sets if this selector should show the texture info or not. Typically in space constrained scenarios this can be hidden
        /// </summary>
        public bool ShowTextureInfo
        {
            get => (bool)GetValue(ShowTextureInfoProperty);
            set => SetValue(ShowTextureInfoProperty, value);
        }

        /// <summary>
        /// Identified the Label dependency property
        /// </summary>
        public static readonly DependencyProperty ShowTextureInfoProperty =
            DependencyProperty.Register(@"ShowTextureInfo", typeof(bool),
                typeof(TargetSelector), new PropertyMetadata(true));

        #endregion

        #region SelectedGameTarget DP

        /// <summary>
        /// The current selected game target
        /// </summary>
        public GameTargetWPF SelectedGameTarget
        {
            get => (GameTargetWPF)GetValue(SelectedGameTargetProperty);
            set => SetValue(SelectedGameTargetProperty, value);
        }

        /// <summary>
        /// Which target is selected
        /// </summary>
        public static readonly DependencyProperty SelectedGameTargetProperty =
            DependencyProperty.Register(@"SelectedGameTarget", typeof(GameTargetWPF),
                typeof(TargetSelector), new FrameworkPropertyMetadata(
                    null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        #endregion

        #region Available Targets DP

        /// <summary>
        /// The current selected game target
        /// </summary>
        public ObservableCollectionExtended<GameTargetWPF> AvailableTargets
        {
            get => (ObservableCollectionExtended<GameTargetWPF>)GetValue(AvailableTargetsProperty);
            set => SetValue(AvailableTargetsProperty, value);
        }

        /// <summary>
        /// Identified the Label dependency property
        /// </summary>
        public static readonly DependencyProperty AvailableTargetsProperty =
            DependencyProperty.Register(@"AvailableTargets", typeof(ObservableCollectionExtended<GameTargetWPF>),
                typeof(TargetSelector), new PropertyMetadata(new ObservableCollectionExtended<GameTargetWPF>()));

        #endregion

        #region Theme DP

        public enum TargetSelectorTheme
        {
            Normal,
            Accent
        }

        /// <summary>
        /// Sets if this selector should show the texture info or not. Typically in space constrained scenarios this can be hidden
        /// </summary>
        public TargetSelectorTheme Theme
        {
            get => (TargetSelectorTheme)GetValue(ThemeProperty);
            set
            {
                SetValue(ThemeProperty, value);
                if (value == TargetSelectorTheme.Normal)
                {
                    ContainerStyle = (Style)FindResource(@"TargetSelectorContainerStyle");
                }
                else if (value == TargetSelectorTheme.Accent)
                {
                    ContainerStyle = (Style)FindResource(@"TargetSelectorContainerAccentStyle");
                }
                else
                {
                    throw new Exception($@"{value} is not a valid value for TargetSelector Theme property!");
                }
            }
        }

        /// <summary>
        /// The theme of the TargetSelector. Normal is default, Accent makes it blue.
        /// </summary>
        public static readonly DependencyProperty ThemeProperty =
            DependencyProperty.Register(@"Theme", typeof(TargetSelectorTheme),
                typeof(TargetSelector), new PropertyMetadata(TargetSelectorTheme.Normal));

        #endregion

        public Style ContainerStyle { get; set; }
        public TargetSelector()
        {
            ContainerStyle = (Style) FindResource(@"TargetSelectorContainerStyle"); // Default
            InitializeComponent();
        }
    }

    public class ExtendedTextureInfoVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // LEAVE AS GAMETARGET!
            if (value is GameTarget gt && parameter is TargetSelector ts)
            {
                return (ts.ShowTextureInfo && gt.TextureModded) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
