using System;

namespace ME3TweaksModManager.ui
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

        public new static DataEventArgs Empty  => new DataEventArgs();
    }
}
