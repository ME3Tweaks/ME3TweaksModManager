namespace ME3TweaksModManager.modmanager.usercontrols.interfaces
{
    /// <summary>
    /// Controls implementing this will have their content size adjusted to the window.
    /// </summary>
    public interface ISizeAdjustable
    {
        /// <summary>
        /// The maximum percentage width of the window that the control can use.
        /// </summary>
        public double MaxWindowWidthPercent { get; set; }

        /// <summary>
        /// The maximum percentage height of the window that the control can use.
        /// </summary>
        public double MaxWindowHeightPercent { get; set; }

        /// <summary>
        /// The maximum width of the control at any time.
        /// </summary>
        public double MaxControlWidth { get; set; }

        /// <summary>
        /// The minimum width of the control at any time.
        /// </summary>
        public double MaxControlHeight { get; set; }

        /// <summary>
        /// The minimum width of the control at any time.
        /// </summary>
        public double MinControlWidth { get; set; }
        /// <summary>
        /// The minimum height of the control at any time.
        /// </summary>
        public double MinControlHeight { get; set; }
    }
}
