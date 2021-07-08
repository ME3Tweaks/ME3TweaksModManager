using System.ComponentModel;
using System.Windows.Media;
using FontAwesome.WPF;

namespace MassEffectModManagerCore.ui
{
    /// <summary>
    /// Class defining a basic UI task, that defaults to an gray hourglass, no spin, with no text.
    /// </summary>
    public class BasicUITask : INotifyPropertyChanged
    {
        public BasicUITask(string tasktext)
        {
            TaskText = tasktext;
        }

        public BasicUITask() { }
        //Fody uses this property on weaving
#pragma warning disable
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
        public string TaskText { get; set; }
        public bool Spin { get; set; } = false;
        public Brush Foreground { get; set; } = Brushes.Gray;
        public FontAwesomeIcon Icon { get; set; } = FontAwesomeIcon.Hourglass;

        public void SetDone()
        {
            Spin = false;
            Icon = FontAwesomeIcon.CheckCircleOutline;
            Foreground = Brushes.Green;
        }

        public void SetInProgress()
        {
            Spin = true;
            Icon = FontAwesomeIcon.Spinner;
            Foreground = Brushes.SaddleBrown;
        }
    }
}