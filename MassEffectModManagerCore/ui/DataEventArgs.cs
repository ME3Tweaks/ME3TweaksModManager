using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManager.ui
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
