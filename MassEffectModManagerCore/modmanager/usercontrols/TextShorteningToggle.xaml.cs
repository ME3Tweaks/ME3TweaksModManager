using ME3TweaksModManager.modmanager.localizations;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// A text display control that can expand/collapse when text exceeds a certain height
    /// </summary>
    public partial class TextShorteningToggle : UserControl, INotifyPropertyChanged
    {
        #region Dependency Properties

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(TextShorteningToggle),
            new PropertyMetadata(string.Empty, OnTextChanged));

        public static readonly DependencyProperty CollapsedMaxHeightProperty = DependencyProperty.Register(
            nameof(CollapsedMaxHeight),
            typeof(double),
            typeof(TextShorteningToggle),
            new PropertyMetadata(60.0, OnCollapsedMaxHeightChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public double CollapsedMaxHeight
        {
            get => (double)GetValue(CollapsedMaxHeightProperty);
            set => SetValue(CollapsedMaxHeightProperty, value);
        }

        #endregion

        #region Properties

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                    OnPropertyChanged(nameof(ShowFadeEffect));
                    UpdateExpandState();
                }
            }
        }

        public double CurrentMaxHeight => IsExpanded ? double.PositiveInfinity : CollapsedMaxHeight;

        public string ExpandButtonText => IsExpanded
            ? M3L.GetString(M3L.string_showLess)
            : M3L.GetString(M3L.string_showMore);

        private bool _showExpandButton;
        public bool ShowExpandButton
        {
            get => _showExpandButton;
            private set
            {
                if (_showExpandButton != value)
                {
                    _showExpandButton = value;
                    OnPropertyChanged(nameof(ShowExpandButton));
                    OnPropertyChanged(nameof(ShowFadeEffect));
                }
            }
        }

        public bool ShowFadeEffect => ShowExpandButton && !IsExpanded;

        #endregion

        public TextShorteningToggle()
        {
            InitializeComponent();
            Loaded += TextShorteningToggle_Loaded;
            SizeChanged += TextShorteningToggle_SizeChanged;
        }

        private void TextShorteningToggle_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateExpandButtonVisibility();
        }

        private void TextShorteningToggle_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.HeightChanged)
            {
                UpdateExpandButtonVisibility();
            }
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextShorteningToggle control)
            {
                control.IsExpanded = false;
                control.UpdateExpandButtonVisibility();
            }
        }

        private static void OnCollapsedMaxHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextShorteningToggle control)
            {
                control.OnPropertyChanged(nameof(CurrentMaxHeight));
                control.UpdateExpandButtonVisibility();
            }
        }

        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            IsExpanded = !IsExpanded;
        }

        private void UpdateExpandState()
        {
            OnPropertyChanged(nameof(CurrentMaxHeight));
            OnPropertyChanged(nameof(ExpandButtonText));
        }

        private void UpdateExpandButtonVisibility()
        {
            if (DisplayTextBlock == null || !IsLoaded)
                return;

            DisplayTextBlock.Measure(new Size(ActualWidth, double.PositiveInfinity));
            var desiredHeight = DisplayTextBlock.DesiredSize.Height;

            ShowExpandButton = DisplayTextBlock.ActualHeight > CollapsedMaxHeight;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
