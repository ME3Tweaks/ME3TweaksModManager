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
        /// If set to true the panel converter will ignore MaxWindowWidth/Height and just use Auto. Good for small controls that don't need lots of space.
        /// </summary>
        public bool DisableM3AutoSizer { get; set; }
    }
}
