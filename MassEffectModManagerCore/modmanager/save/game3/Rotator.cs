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
    public class Rotator : IUnrealSerializable, INotifyPropertyChanged
    {
        private int _Pitch;
        private int _Yaw;
        private int _Roll;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._Pitch);
            stream.Serialize(ref this._Yaw);
            stream.Serialize(ref this._Roll);
        }

        public override string ToString()
        {
            return string.Format("{0}, {1}, {2}",
                                 this._Pitch,
                                 this._Yaw,
                                 this._Roll);
        }

        #region Properties
        public int Pitch
        {
            get { return this._Pitch; }
            set
            {
                if (value != this._Pitch)
                {
                    this._Pitch = value;
                    this.NotifyPropertyChanged("Pitch");
                }
            }
        }

        public int Yaw
        {
            get { return this._Yaw; }
            set
            {
                if (value != this._Yaw)
                {
                    this._Yaw = value;
                    this.NotifyPropertyChanged("Yaw");
                }
            }
        }

        public int Roll
        {
            get { return this._Roll; }
            set
            {
                if (value != this._Roll)
                {
                    this._Roll = value;
                    this.NotifyPropertyChanged("Roll");
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
