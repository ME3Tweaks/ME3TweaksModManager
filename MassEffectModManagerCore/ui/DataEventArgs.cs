using System;

namespace MassEffectModManagerCore.ui
{
    public class DataEventArgs : EventArgs
    {
        public object Data { get; set; }
        public DataEventArgs(object data)
        {
            Data = data;
        }

        public DataEventArgs()
        {

        }

        public static DataEventArgs Empty  => new DataEventArgs();
    }
}
