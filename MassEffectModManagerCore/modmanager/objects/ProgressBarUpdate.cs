using System.Windows;

namespace MassEffectModManagerCore.modmanager.objects
{
    /// <summary>
    /// Used by the usercontrols to callback to the main window UI to tell it what to do with the progressbar
    /// </summary>
    public class ProgressBarUpdate
    {
        public enum UpdateTypes
        {
            SET_MAX,
            SET_VALUE,
            SET_VISIBILITY,
            SET_INDETERMINATE
        }

        public object Data;
        public UpdateTypes UpdateType;

        public ProgressBarUpdate(UpdateTypes type, ulong data)
        {
            this.UpdateType = type;
            this.Data = data;
        }

        public ProgressBarUpdate(UpdateTypes type, byte data)
        {
            this.UpdateType = type;
            this.Data = (ulong)data;
        }

        public ProgressBarUpdate(UpdateTypes type, bool boolean)
        {
            this.UpdateType = type;
            this.Data = boolean;
        }

        public ProgressBarUpdate(UpdateTypes type, Visibility visibility)
        {
            this.UpdateType = type;
            this.Data = visibility;
        }

        public ulong GetDataAsULong() => (ulong)Data;
        public Visibility GetDataAsVisibility() => (Visibility)Data;
        public bool GetDataAsBool() => (bool)Data;
    }
}
