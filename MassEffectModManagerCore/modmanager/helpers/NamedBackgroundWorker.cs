using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MassEffectModManager.modmanager.helpers
{
    public class NamedBackgroundWorker : BackgroundWorker
    {
        public NamedBackgroundWorker(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            if (Thread.CurrentThread.Name == null) // Can only set it once
                Thread.CurrentThread.Name = Name;

            base.OnDoWork(e);
        }
    }
}
