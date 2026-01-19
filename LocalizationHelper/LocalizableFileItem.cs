#if DEBUG
using System.ComponentModel;

namespace LocalizationHelper
{
    /// <summary>
    /// Represents a source file that can be localized, with status information about localization needs.
    /// </summary>
    public class LocalizableFileItem : INotifyPropertyChanged
    {
        private bool _hasStringsNeedingLocalization;
        private bool _isScanned;

        /// <summary>
        /// Gets or sets the relative path of the source file.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets whether this file has strings that need localization.
        /// </summary>
        public bool HasStringsNeedingLocalization
        {
            get => _hasStringsNeedingLocalization;
            set
            {
                if (_hasStringsNeedingLocalization != value)
                {
                    _hasStringsNeedingLocalization = value;
                    OnPropertyChanged(nameof(HasStringsNeedingLocalization));
                    OnPropertyChanged(nameof(StatusIcon));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(FilePathOpacity));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether this file has been scanned for localization needs.
        /// </summary>
        public bool IsScanned
        {
            get => _isScanned;
            set
            {
                if (_isScanned != value)
                {
                    _isScanned = value;
                    OnPropertyChanged(nameof(IsScanned));
                    OnPropertyChanged(nameof(StatusIcon));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(FilePathOpacity));
                }
            }
        }

        /// <summary>
        /// Gets the icon to display based on localization status.
        /// ⚠️ if file has strings needing localization
        /// ✓ if file has been scanned and has no strings needing localization
        /// ? if file has not been scanned yet
        /// </summary>
        public string StatusIcon
        {
            get
            {
                if (!IsScanned) return "?";
                return HasStringsNeedingLocalization ? "⚠️" : "✓";
            }
        }

        /// <summary>
        /// Gets the color to display for the status icon.
        /// Orange (#FFA500) if file has strings needing localization
        /// Green (#00FF00) if file has been scanned and has no strings needing localization
        /// Gray (#808080) if file has not been scanned yet
        /// </summary>
        public string StatusColor
        {
            get
            {
                if (!IsScanned) return "#808080"; // Gray
                return HasStringsNeedingLocalization ? "#ab4a00" : "#007a02"; // Orange : Green
            }
        }

        /// <summary>
        /// Gets the opacity for the file path text.
        /// 1.0 (fully visible) if file has strings needing localization or hasn't been scanned
        /// 0.5 (dimmed) if file has been scanned and has no strings needing localization
        /// </summary>
        public double FilePathOpacity
        {
            get
            {
                if (!IsScanned) return 1.0; // Not yet scanned, show fully
                return HasStringsNeedingLocalization ? 1.0 : 0.5; // Dim if no strings needed
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
#endif
