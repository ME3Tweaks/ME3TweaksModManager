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
using MassEffectModManagerCore.modmanager.save.game2.FileFormats;

namespace MassEffectModManagerCore.modmanager.save.game3
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [OriginalName("MorphHeadSaveRecord")]
    public class MorphHead : IUnrealSerializable, INotifyPropertyChanged
    {
        #region Fields
        [OriginalName("HairMesh")]
        private string _HairMesh;

        [OriginalName("AccessoryMeshes")]
        private List<string> _AccessoryMeshes = new List<string>();

        [OriginalName("MorphFeatures")]
        private List<MorphFeature> _MorphFeatures = new List<MorphFeature>();

        [OriginalName("OffsetBones")]
        private List<OffsetBone> _OffsetBones = new List<OffsetBone>();

        [OriginalName("LOD0Vertices")]
        private List<Vector> _Lod0Vertices = new List<Vector>();

        [OriginalName("LOD1Vertices")]
        private List<Vector> _Lod1Vertices = new List<Vector>();

        [OriginalName("LOD2Vertices")]
        private List<Vector> _Lod2Vertices = new List<Vector>();

        [OriginalName("LOD3Vertices")]
        private List<Vector> _Lod3Vertices = new List<Vector>();

        [OriginalName("ScalarParameters")]
        private List<ScalarParameter> _ScalarParameters = new List<ScalarParameter>();

        [OriginalName("VectorParameters")]
        private BindingList<VectorParameter> _VectorParameters = new BindingList<VectorParameter>();

        [OriginalName("TextureParameters")]
        private List<TextureParameter> _TextureParameters = new List<TextureParameter>();
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._HairMesh);
            stream.Serialize(ref this._AccessoryMeshes);
            stream.Serialize(ref this._MorphFeatures);
            stream.Serialize(ref this._OffsetBones);
            stream.Serialize(ref this._Lod0Vertices);
            stream.Serialize(ref this._Lod1Vertices);
            stream.Serialize(ref this._Lod2Vertices);
            stream.Serialize(ref this._Lod3Vertices);
            stream.Serialize(ref this._ScalarParameters);
            stream.Serialize(ref this._VectorParameters);
            stream.Serialize(ref this._TextureParameters);
        }

        #region Properties
        public string HairMesh
        {
            get { return this._HairMesh; }
            set
            {
                if (value != this._HairMesh)
                {
                    this._HairMesh = value;
                    this.NotifyPropertyChanged("HairMesh");
                }
            }
        }

        [Editor(
            "System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
            , typeof(System.Drawing.Design.UITypeEditor))]
        public List<string> AccessoryMeshes
        {
            get { return this._AccessoryMeshes; }
            set
            {
                if (value != this._AccessoryMeshes)
                {
                    this._AccessoryMeshes = value;
                    this.NotifyPropertyChanged("AccessoryMeshes");
                }
            }
        }

        public List<MorphFeature> MorphFeatures
        {
            get { return this._MorphFeatures; }
            set
            {
                if (value != this._MorphFeatures)
                {
                    this._MorphFeatures = value;
                    this.NotifyPropertyChanged("MorphFeatures");
                }
            }
        }

        public List<OffsetBone> OffsetBones
        {
            get { return this._OffsetBones; }
            set
            {
                if (value != this._OffsetBones)
                {
                    this._OffsetBones = value;
                    this.NotifyPropertyChanged("OffsetBones");
                }
            }
        }

        public List<Vector> Lod0Vertices
        {
            get { return this._Lod0Vertices; }
            set
            {
                if (value != this._Lod0Vertices)
                {
                    this._Lod0Vertices = value;
                    this.NotifyPropertyChanged("Lod0Vertices");
                }
            }
        }

        public List<Vector> Lod1Vertices
        {
            get { return this._Lod1Vertices; }
            set
            {
                if (value != this._Lod1Vertices)
                {
                    this._Lod1Vertices = value;
                    this.NotifyPropertyChanged("Lod1Vertices");
                }
            }
        }

        public List<Vector> Lod2Vertices
        {
            get { return this._Lod2Vertices; }
            set
            {
                if (value != this._Lod2Vertices)
                {
                    this._Lod2Vertices = value;
                    this.NotifyPropertyChanged("Lod2Vertices");
                }
            }
        }

        public List<Vector> Lod3Vertices
        {
            get { return this._Lod3Vertices; }
            set
            {
                if (value != this._Lod3Vertices)
                {
                    this._Lod3Vertices = value;
                    this.NotifyPropertyChanged("Lod3Vertices");
                }
            }
        }

        public List<ScalarParameter> ScalarParameters
        {
            get { return this._ScalarParameters; }
            set
            {
                if (value != this._ScalarParameters)
                {
                    this._ScalarParameters = value;
                    this.NotifyPropertyChanged("ScalarParameters");
                }
            }
        }

        public BindingList<VectorParameter> VectorParameters
        {
            get { return this._VectorParameters; }
            set
            {
                if (value != this._VectorParameters)
                {
                    this._VectorParameters = value;
                    this.NotifyPropertyChanged("VectorParameters");
                }
            }
        }

        public List<TextureParameter> TextureParameters
        {
            get { return this._TextureParameters; }
            set
            {
                if (value != this._TextureParameters)
                {
                    this._TextureParameters = value;
                    this.NotifyPropertyChanged("TextureParameters");
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
        [OriginalName("MorphFeatureSaveRecord")]
        public class MorphFeature : IUnrealSerializable, INotifyPropertyChanged
        {
            #region Fields
            [OriginalName("Feature")]
            private string _Feature;

            [OriginalName("Offset")]
            private float _Offset;
            #endregion

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref this._Feature);
                stream.Serialize(ref this._Offset);
            }

            // for CollectionEditor
            [Browsable(false)]
            public string Name
            {
                get { return this._Feature; }
            }

            public override string ToString()
            {
                return this.Name ?? "(null)";
            }

            #region Properties
            public string Feature
            {
                get { return this._Feature; }
                set
                {
                    if (value != this._Feature)
                    {
                        this._Feature = value;
                        this.NotifyPropertyChanged("Feature");
                    }
                }
            }

            public float Offset
            {
                get { return this._Offset; }
                set
                {
                    if (Equals(value, this._Offset) == false)
                    {
                        this._Offset = value;
                        this.NotifyPropertyChanged("Offset");
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
        [OriginalName("OffsetBoneSaveRecord")]
        public class OffsetBone : IUnrealSerializable, INotifyPropertyChanged
        {
            #region Fields
            [OriginalName("Name")]
            private string _Name;

            [OriginalName("Name")]
            private Vector _Offset = new Vector();
            #endregion

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref this._Name);
                stream.Serialize(ref this._Offset);
            }

            // for CollectionEditor
            public override string ToString()
            {
                return this.Name ?? "(null)";
            }

            #region Properties
            public string Name
            {
                get { return this._Name; }
                set
                {
                    if (value != this._Name)
                    {
                        this._Name = value;
                        this.NotifyPropertyChanged("Name");
                    }
                }
            }

            public Vector Offset
            {
                get { return this._Offset; }
                set
                {
                    if (value != this._Offset)
                    {
                        this._Offset = value;
                        this.NotifyPropertyChanged("Offset");
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
        [OriginalName("ScalarParameterSaveRecord")]
        public class ScalarParameter : IUnrealSerializable, INotifyPropertyChanged
        {
            #region Fields
            [OriginalName("Name")]
            private string _Name;

            [OriginalName("Value")]
            private float _Value;
            #endregion

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref this._Name);
                stream.Serialize(ref this._Value);
            }

            // for CollectionEditor
            public override string ToString()
            {
                return this.Name ?? "(null)";
            }

            #region Properties
            public string Name
            {
                get { return this._Name; }
                set
                {
                    if (value != this._Name)
                    {
                        this._Name = value;
                        this.NotifyPropertyChanged("Name");
                    }
                }
            }

            public float Value
            {
                get { return this._Value; }
                set
                {
                    if (Equals(value, this._Value) == false)
                    {
                        this._Value = value;
                        this.NotifyPropertyChanged("Value");
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
        [OriginalName("VectorParameterSaveRecord")]
        public class VectorParameter : IUnrealSerializable, INotifyPropertyChanged
        {
            #region Fields
            [OriginalName("Name")]
            private string _Name;

            [OriginalName("Value")]
            private LinearColor _Value = new LinearColor();
            #endregion

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref this._Name);
                stream.Serialize(ref this._Value);
            }

            // for CollectionEditor
            public override string ToString()
            {
                return this.Name ?? "(null)";
            }

            #region Properties
            public string Name
            {
                get { return this._Name; }
                set
                {
                    if (value != this._Name)
                    {
                        this._Name = value;
                        this.NotifyPropertyChanged("Name");
                    }
                }
            }

            public LinearColor Value
            {
                get { return this._Value; }
                set
                {
                    if (value != this._Value)
                    {
                        this._Value = value;
                        this.NotifyPropertyChanged("Value");
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
        [OriginalName("TextureParameterSaveRecord")]
        public class TextureParameter : IUnrealSerializable, INotifyPropertyChanged
        {
            #region Fields
            [OriginalName("Name")]
            private string _Name;

            [OriginalName("Value")]
            private string _Value;
            #endregion

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref this._Name);
                stream.Serialize(ref this._Value);
            }

            // for CollectionEditor
            public override string ToString()
            {
                return this.Name ?? "(null)";
            }

            #region Properties
            public string Name
            {
                get { return this._Name; }
                set
                {
                    if (value != this._Name)
                    {
                        this._Name = value;
                        this.NotifyPropertyChanged("Name");
                    }
                }
            }

            public string Value
            {
                get { return this._Value; }
                set
                {
                    if (value != this._Value)
                    {
                        this._Value = value;
                        this.NotifyPropertyChanged("Value");
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
