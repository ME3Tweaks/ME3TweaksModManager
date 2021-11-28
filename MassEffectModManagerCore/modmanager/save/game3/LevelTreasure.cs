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

using System.Collections.Generic;
using System.ComponentModel;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;

namespace ME3TweaksModManager.modmanager.save.game3
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [OriginalName("LevelTreasureSaveRecord")]
    public class LevelTreasure : IUnrealSerializable, INotifyPropertyChanged
    {
        #region Fields
        [OriginalName("LevelName")]
        private string _LevelName;

        [OriginalName("nCredits")]
        private int _Credits;

        [OriginalName("nXP")]
        private int _XP;

        [OriginalName("Items")]
        private List<string> _Items = new List<string>();
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._LevelName);
            stream.Serialize(ref this._Credits);
            stream.Serialize(ref this._XP);
            stream.Serialize(ref this._Items);
        }

        #region Properties
        public string LevelName
        {
            get { return this._LevelName; }
            set
            {
                if (value != this._LevelName)
                {
                    this._LevelName = value;
                    this.NotifyPropertyChanged("LevelName");
                }
            }
        }

        public int Credits
        {
            get { return this._Credits; }
            set
            {
                if (value != this._Credits)
                {
                    this._Credits = value;
                    this.NotifyPropertyChanged("Credits");
                }
            }
        }

        public int XP
        {
            get { return this._XP; }
            set
            {
                if (value != this._XP)
                {
                    this._XP = value;
                    this.NotifyPropertyChanged("XP");
                }
            }
        }

        [Editor(
            "System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
            , typeof(System.Drawing.Design.UITypeEditor))]
        public List<string> Items
        {
            get { return this._Items; }
            set
            {
                if (value != this._Items)
                {
                    this._Items = value;
                    this.NotifyPropertyChanged("Items");
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
