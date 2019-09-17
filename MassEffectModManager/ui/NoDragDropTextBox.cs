using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MassEffectModManager.ui
{
    class NoDragDropTextBox : TextBox
    {
        public new event EventHandler<EventArgs> DragOver;
        public new event EventHandler<EventArgs> Drop;

    }
}
