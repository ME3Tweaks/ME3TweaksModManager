using System.Windows.Media;
using FontAwesome5;
using PropertyChanged;

namespace ME3TweaksModManager.ui
{
    /// <summary>
    /// Class defining a basic UI task, that defaults to an gray hourglass, no spin, with no text.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class BasicUITask
    {
        public BasicUITask(string tasktext)
        {
            TaskText = tasktext;
        }

        public BasicUITask() { }

        public string TaskText { get; set; }
        public bool Spin { get; set; } = false;
        public Brush Foreground { get; set; } = Brushes.Gray;
        public EFontAwesomeIcon Icon { get; set; } = EFontAwesomeIcon.Regular_Hourglass;

        public void SetDone()
        {
            Spin = false;
            Icon = EFontAwesomeIcon.Regular_CheckCircle;
            Foreground = Brushes.Green;
        }

        public void SetInProgress()
        {
            Spin = true;
            Icon = EFontAwesomeIcon.Solid_Spinner;
            Foreground = Brushes.SaddleBrown;
        }
    }
}