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
    [OriginalName("HotKeySaveRecord")]
    public class HotKey : IUnrealSerializable, INotifyPropertyChanged
    {
        #region Fields
        [OriginalName("PawnName")]
        private string _PawnName;

        [OriginalName("PowerID")]
        private int _PowerId;

        [OriginalName("PowerName")]
        private string _PowerName;
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._PawnName);
            stream.Serialize(ref this._PowerName, s => s.Version < 30, () => null);
            stream.Serialize(ref this._PowerId, s => s.Version >= 30, () => 0);
        }

        #region Properties
        public string PawnName
        {
            get { return this._PawnName; }
            set
            {
                if (value != this._PawnName)
                {
                    this._PawnName = value;
                    this.NotifyPropertyChanged("PawnName");
                }
            }
        }

        [Browsable(false)]
        public int PowerId
        {
            get { return this._PowerId; }
            set
            {
                if (value != this._PowerId)
                {
                    this._PowerId = value;
                    this.NotifyPropertyChanged("PowerId");
                }
            }
        }

        public string PowerName
        {
            get { return this._PowerName; }
            set
            {
                if (value != this._PowerName)
                {
                    this._PowerName = value;
                    this.NotifyPropertyChanged("PowerName");
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
