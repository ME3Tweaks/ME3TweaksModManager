using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using FontAwesome.WPF;

namespace MassEffectModManagerCore.modmanager.windows
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
        public event PropertyChangedEventHandler PropertyChanged;
        public string TaskText { get; set; }
        public bool Spin { get; set; } = false;
        public Brush Foreground { get; set; } = Brushes.Gray;
        public FontAwesomeIcon Icon { get; set; } = FontAwesomeIcon.Hourglass;
    }
}