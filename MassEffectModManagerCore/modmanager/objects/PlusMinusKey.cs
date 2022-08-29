using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// Represents a moddesc.ini key for +/- prefixes and a value.
    /// </summary>
    public class PlusMinusKey
    {
        /// <summary>
        /// If the key is prefixed with + or -, or null if none.
        /// </summary>
        public bool? IsPlus { get; set; }

        /// <summary>
        /// The value of the key
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// String representation of this key
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string str = "";
            if (IsPlus != null) str += IsPlus.Value ? "+" : "-";
            str += Key;
            return str;
        }

        public PlusMinusKey() { }

        /// <summary>
        /// Constructor that sets a positive/negative with a value.
        /// </summary>
        /// <param name="isPlus"></param>
        /// <param name="key"></param>
        public PlusMinusKey(bool isPlus, string key)
        {
            IsPlus = isPlus;
            Key = key;
        }



        /// <summary>
        /// Constructor that takes a full string value and parses it out
        /// </summary>
        /// <param name="fullValue">Full value including an optional +/- prefix</param>
        public PlusMinusKey(string fullValue)
        {
            var keyOp = fullValue[0];
            if (keyOp == '+') IsPlus = true;
            if (keyOp == '-') IsPlus = false;
            if (IsPlus.HasValue)
            {
                Key = fullValue.Substring(1);
            }
            else
            {
                Key = fullValue;
            }
        }

    }
}
