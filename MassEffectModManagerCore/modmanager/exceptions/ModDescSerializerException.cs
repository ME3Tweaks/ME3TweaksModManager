using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.exceptions
{
    /// <summary>
    /// Thrown when there's an error serializing a moddesc.ini file in the editor, for handled cases
    /// </summary>
    internal class ModDescSerializerException : Exception
    {
        public ModDescSerializerException(string message) : base(message){ }
    }
}
