/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;

namespace ME3TweaksModManager.modmanager.save.game3
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [OriginalName("ME1PlotTableRecord")]
    // ReSharper disable InconsistentNaming
    public class ME1PlotTable : IUnrealSerializable, INotifyPropertyChanged
        // ReSharper restore InconsistentNaming
    {
        public ME1PlotTable()
        {
            this._BoolVariablesWrapper = new BitArrayWrapper(this._BoolVariables);
        }

        #region Fields
        [OriginalName("BoolVariables")]
        private BitArray _BoolVariables = new BitArray(0);

        private readonly BitArrayWrapper _BoolVariablesWrapper;

        [OriginalName("IntVariables")]
        private List<int> _IntVariables = new List<int>();

        [OriginalName("FloatVariables")]
        private List<float> _FloatVariables = new List<float>();
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._BoolVariables);
            stream.Serialize(ref this._IntVariables);
            stream.Serialize(ref this._FloatVariables);
        }

        #region Properties
        [Browsable(false)]
        public BitArray BoolVariables
        {
            get { return this._BoolVariables; }
        }

        [DisplayName("Bool Variables")]
        public BitArrayWrapper BoolVariablesWrapper
        {
            get { return this._BoolVariablesWrapper; }
            /*set
            {
                if (value != this._BoolVariables)
                {
                    this._BoolVariables = value;
                    this.NotifyPropertyChanged("BoolVariables");
                }
            }*/
        }

        [DisplayName("Int Variables")]
        public List<int> IntVariables
        {
            get { return this._IntVariables; }
            set
            {
                if (value != this._IntVariables)
                {
                    this._IntVariables = value;
                    this.NotifyPropertyChanged("IntVariables");
                }
            }
        }

        [DisplayName("Float Variables")]
        public List<float> FloatVariables
        {
            get { return this._FloatVariables; }
            set
            {
                if (value != this._FloatVariables)
                {
                    this._FloatVariables = value;
                    this.NotifyPropertyChanged("FloatVariables");
                }
            }
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
