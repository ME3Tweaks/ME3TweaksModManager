using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManagerCore.modmanager.usercontrols.interfaces
{
    public interface ISizeAdjustable
    {
        public double Adjustment { get; set; }
        public double FullSize { get; }

        /// <summary>
        /// Bind to this property for the UI. When the sizing needs updated, force a property change for it.
        /// </summary>
        public ISizeAdjustable Self { get; init; }
    }
}
