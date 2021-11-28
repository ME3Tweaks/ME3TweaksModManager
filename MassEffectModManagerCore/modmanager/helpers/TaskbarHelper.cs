using ME3TweaksCore.Misc;
using ME3TweaksCoreWPF.UI;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace ME3TweaksModManager.modmanager.helpers
{

    /// <summary>
    /// Helper for taskbar operations. Designed so it can avoid strange issues where setting taskbar stuff crashes the app instead
    /// due to some fun bugs in wpf
    /// </summary>
    public static class TaskbarHelper
    {
        public static void SetProgress(int currentvalue, int maxvalue)
        {
            try
            {
                TaskbarManager.Instance?.SetProgressValue(currentvalue, maxvalue);
            }
            catch
            {
                // Sometimes windows throws exception internally fetching progressbar and it bubbles out to here (yes, I've seen this)
            }
        }

        /// <summary>
        /// Sets the progress value. Value must be between 0 and 1
        /// </summary>
        /// <param name="progressVal"></param>
        public static void SetProgress(double progressVal)
        {
            try
            {
                TaskbarManager.Instance?.SetProgressValue((int)(progressVal * 100), 100);
            }
            catch
            {
                // Sometimes windows throws exception internally fetching progressbar and it bubbles out to here (yes, I've seen this)
            }
        }

        public static void SetProgressState(MTaskbarState state)
        {
            SetProgressState(MTaskbarStateWPF.ConvertTaskbarState(state));
        }

        public static void SetProgressState(TaskbarProgressBarState state)
        {
            try
            {
                TaskbarManager.Instance?.SetProgressState(state);
            }
            catch
            {
                // Sometimes windows throws exception internally fetching progressbar and it bubbles out to here (yes, I've seen this)
            }
        }
    }
}
