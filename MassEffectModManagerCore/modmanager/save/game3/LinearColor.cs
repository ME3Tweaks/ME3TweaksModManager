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

using System.ComponentModel;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;

namespace ME3TweaksModManager.modmanager.save.game3
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class LinearColor : IUnrealSerializable, INotifyPropertyChanged
    {
        #region Fields
        private float _R;
        private float _G;
        private float _B;
        private float _A;
        #endregion

        public LinearColor()
            : this(0.0f, 0.0f, 0.0f, 0.0f)
        {
        }

        public LinearColor(float r, float g, float b, float a)
        {
            this._R = r;
            this._G = g;
            this._B = b;
            this._A = a;
        }

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._R);
            stream.Serialize(ref this._G);
            stream.Serialize(ref this._B);
            stream.Serialize(ref this._A);
        }

        public override string ToString()
        {
            return string.Format("{0}, {1}, {2}, {3}",
                                 this._R,
                                 this._G,
                                 this._B,
                                 this._A);
        }

        #region Properties
        public float R
        {
            get { return this._R; }
            set
            {
                if (Equals(value, this._R) == false)
                {
                    this._R = value;
                    this.NotifyPropertyChanged("R");
                }
            }
        }

        public float G
        {
            get { return this._G; }
            set
            {
                if (Equals(value, this._G) == false)
                {
                    this._G = value;
                    this.NotifyPropertyChanged("G");
                }
            }
        }

        public float B
        {
            get { return this._B; }
            set
            {
                if (Equals(value, this._B) == false)
                {
                    this._B = value;
                    this.NotifyPropertyChanged("B");
                }
            }
        }

        public float A
        {
            get { return this._A; }
            set
            {
                if (Equals(value, this._A) == false)
                {
                    this._A = value;
                    this.NotifyPropertyChanged("A");
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
