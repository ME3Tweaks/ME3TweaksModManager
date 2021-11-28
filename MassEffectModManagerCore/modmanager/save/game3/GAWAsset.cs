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
using System.Globalization;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;

namespace ME3TweaksModManager.modmanager.save.game3
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [OriginalName("GAWAssetSaveInfo")]
    public class GAWAsset : IUnrealSerializable, INotifyPropertyChanged
    {
        #region Fields
        [OriginalName("Id")]
        private int _Id;

        [OriginalName("Strength")]
        private int _Strength;
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._Id);
            stream.Serialize(ref this._Strength);
        }

        // for CollectionEditor
        [Browsable(false)]
        public string Name
        {
            get { return this._Id.ToString(CultureInfo.InvariantCulture); }
        }

        public override string ToString()
        {
            return this.Name ?? "(null)";
        }

        #region Properties
        public int Id
        {
            get { return this._Id; }
            set
            {
                if (value != this._Id)
                {
                    this._Id = value;
                    this.NotifyPropertyChanged("Id");
                }
            }
        }

        public int Strength
        {
            get { return this._Strength; }
            set
            {
                if (value != this._Strength)
                {
                    this._Strength = value;
                    this.NotifyPropertyChanged("Strength");
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
