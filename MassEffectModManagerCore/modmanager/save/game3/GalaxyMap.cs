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
    [OriginalName("GalaxyMapSaveRecord")]
    public class GalaxyMap : IUnrealSerializable, INotifyPropertyChanged
    {
        #region Fields
        [OriginalName("Planets")]
        private List<Planet> _Planets = new List<Planet>();

        [OriginalName("Systems")]
        private List<System> _Systems = new List<System>();
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._Planets);
            stream.Serialize(ref this._Systems, s => s.Version < 51, () => new List<System>());
        }

        #region Properties
        public List<Planet> Planets
        {
            get { return this._Planets; }
            set
            {
                if (value != this._Planets)
                {
                    this._Planets = value;
                    this.NotifyPropertyChanged("Planets");
                }
            }
        }

        public List<System> Systems
        {
            get { return this._Systems; }
            set
            {
                if (value != this._Systems)
                {
                    this._Systems = value;
                    this.NotifyPropertyChanged("Systems");
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

        #region Children
        [TypeConverter(typeof(ExpandableObjectConverter))]
        [OriginalName("PlanetSaveRecord")]
        public class Planet : IUnrealSerializable, INotifyPropertyChanged
        {
            #region Fields
            [OriginalName("PlanetID")]
            private int _Id;

            [OriginalName("bVisited")]
            private bool _Visited;

            [OriginalName("Probes")]
            private List<Vector2D> _Probes = new List<Vector2D>();

            [OriginalName("bShowAsScanned")]
            private bool _ShowAsScanned;
            #endregion

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref this._Id);
                stream.Serialize(ref this._Visited);
                stream.Serialize(ref this._Probes);
                stream.Serialize(ref this._ShowAsScanned, s => s.Version < 51, () => false);
            }

            // for CollectionEditor
            [Browsable(false)]
            public string Name
            {
                get { return this._Id.ToString(global::System.Globalization.CultureInfo.InvariantCulture); }
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

            public bool Visited
            {
                get { return this._Visited; }
                set
                {
                    if (value != this._Visited)
                    {
                        this._Visited = value;
                        this.NotifyPropertyChanged("Visited");
                    }
                }
            }

            public List<Vector2D> Probes
            {
                get { return this._Probes; }
                set
                {
                    if (value != this._Probes)
                    {
                        this._Probes = value;
                        this.NotifyPropertyChanged("Probes");
                    }
                }
            }

            public bool ShowAsScanned
            {
                get { return this._ShowAsScanned; }
                set
                {
                    if (value != this._ShowAsScanned)
                    {
                        this._ShowAsScanned = value;
                        this.NotifyPropertyChanged("ShowAsScanned");
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

        [TypeConverter(typeof(ExpandableObjectConverter))]
        [OriginalName("SystemSaveRecord")]
        public class System : IUnrealSerializable, INotifyPropertyChanged
        {
            #region Fields
            [OriginalName("SystemID")]
            private int _Id;

            [OriginalName("fReaperAlertLevel")]
            private float _ReaperAlertLevel;

            [OriginalName("bReapersDetected")]
            private bool _ReapersDetected;
            #endregion

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref this._Id);
                stream.Serialize(ref this._ReaperAlertLevel);
                stream.Serialize(ref this._ReapersDetected, s => s.Version < 58, () => false);
            }

            // for CollectionEditor
            [Browsable(false)]
            public string Name
            {
                get { return this._Id.ToString(global::System.Globalization.CultureInfo.InvariantCulture); }
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

            public float ReaperAlertLevel
            {
                get { return this._ReaperAlertLevel; }
                set
                {
                    if (Equals(value, this._ReaperAlertLevel) == false)
                    {
                        this._ReaperAlertLevel = value;
                        this.NotifyPropertyChanged("ReaperAlertLevel");
                    }
                }
            }

            public bool ReapersDetected
            {
                get { return this._ReapersDetected; }
                set
                {
                    if (value != this._ReapersDetected)
                    {
                        this._ReapersDetected = value;
                        this.NotifyPropertyChanged("ReapersDetected");
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
        #endregion
    }
}
