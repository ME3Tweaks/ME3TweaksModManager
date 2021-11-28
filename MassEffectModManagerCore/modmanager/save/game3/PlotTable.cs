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

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;

namespace ME3TweaksModManager.modmanager.save.game3
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [OriginalName("PlotTableSaveRecord")]
    public class PlotTable : IPlotTable, IUnrealSerializable, INotifyPropertyChanged
    {
        public PlotTable()
        {
            this._Helpers = new PlotTableWrapper(this);
            this._BoolVariablesWrapper = new BitArrayWrapper(this._BoolVariables);
        }

        #region Fields
        private readonly PlotTableWrapper _Helpers;

        [OriginalName("BoolVariables")]
        private BitArray _BoolVariables = new BitArray(0);

        private readonly BitArrayWrapper _BoolVariablesWrapper;

        [OriginalName("IntVariables")]
        private List<IntVariablePair> _IntVariables = new List<IntVariablePair>();

        [OriginalName("FloatVariables")]
        private List<FloatVariablePair> _FloatVariables = new List<FloatVariablePair>();

        [OriginalName("QuestProgressCounter")]
        private int _QuestProgressCounter;

        [OriginalName("QuestProgress")]
        private List<PlotQuest> _QuestProgress = new List<PlotQuest>();

        [OriginalName("QuestIDs")]
        private List<int> _QuestIDs = new List<int>();

        [OriginalName("CodexEntries")]
        private List<PlotCodex> _CodexEntries = new List<PlotCodex>();

        [OriginalName("CodexIDs")]
        private List<int> _CodexIDs = new List<int>();
        #endregion

        #region Helpers
        public bool GetBoolVariable(int index)
        {
            if (index >= this._BoolVariables.Count)
            {
                return false;
            }

            return this._BoolVariables[index];
        }

        public void SetBoolVariable(int index, bool value)
        {
            if (index >= this._BoolVariables.Count)
            {
                this._BoolVariables.Length = index + 1;
            }

            this._BoolVariables[index] = value;
        }

        public int GetIntVariable(int index)
        {
            var variable = this._IntVariables
                .FirstOrDefault(v => v.Index == index);
            if (variable == null)
            {
                return 0;
            }
            return variable.Value;
        }

        public void SetIntVariable(int index, int value)
        {
            var targets = this._IntVariables
                .Where(v => v.Index == index)
                .ToArray();

            if (targets.Length == 0)
            {
                this._IntVariables.Add(new IntVariablePair()
                {
                    Index = index,
                    Value = value,
                });
                return;
            }

            targets[0].Value = value;

            for (int i = 1; i < targets.Length; i++)
            {
                this._IntVariables.Remove(targets[i]);
            }
        }

        public float GetFloatVariable(int index)
        {
            var variable = this._FloatVariables
                .FirstOrDefault(v => v.Index == index);
            if (variable == null)
            {
                return 0;
            }
            return variable.Value;
        }

        public void SetFloatVariable(int index, float value)
        {
            var targets = this._FloatVariables
                .Where(v => v.Index == index)
                .ToArray();

            if (targets.Length == 0)
            {
                this._FloatVariables.Add(new FloatVariablePair()
                {
                    Index = index,
                    Value = value,
                });
                return;
            }

            targets[0].Value = value;

            for (int i = 1; i < targets.Length; i++)
            {
                this._FloatVariables.Remove(targets[i]);
            }
        }
        #endregion

        #region Serialize
        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._BoolVariables);

            if (stream.Version >= 56)
            {
                stream.Serialize(ref this._IntVariables);
                stream.Serialize(ref this._FloatVariables);
            }
            else
            {
                if (stream.Loading)
                {
                    var oldIntVariables = new List<int>();
                    stream.Serialize(ref oldIntVariables);
                    this._IntVariables = new List<IntVariablePair>();
                    for (int i = 0; i < oldIntVariables.Count; i++)
                    {
                        if (oldIntVariables[i] == 0)
                        {
                            continue;
                        }

                        this._IntVariables.Add(new IntVariablePair()
                        {
                            Index = i,
                            Value = oldIntVariables[i],
                        });
                    }

                    var oldFloatVariables = new List<float>();
                    stream.Serialize(ref oldFloatVariables);
                    this._FloatVariables = new List<FloatVariablePair>();
                    for (int i = 0; i < oldFloatVariables.Count; i++)
                    {
                        if (Equals(oldFloatVariables[i], 0.0f) == true)
                        {
                            continue;
                        }

                        this._FloatVariables.Add(new FloatVariablePair()
                        {
                            Index = i,
                            Value = oldFloatVariables[i],
                        });
                    }
                }
                else if (!stream.Loading)
                {
                    var oldIntVariables = new List<int>();
                    if (this._IntVariables != null)
                    {
                        foreach (var intVariable in this._IntVariables)
                        {
                            oldIntVariables[intVariable.Index] = intVariable.Value;
                        }
                    }
                    stream.Serialize(ref oldIntVariables);

                    var oldFloatVariables = new List<float>();
                    if (this._FloatVariables != null)
                    {
                        foreach (var floatVariable in this._FloatVariables)
                        {
                            oldFloatVariables[floatVariable.Index] = floatVariable.Value;
                        }
                    }
                    stream.Serialize(ref oldFloatVariables);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }

            stream.Serialize(ref this._QuestProgressCounter);
            stream.Serialize(ref this._QuestProgress);
            stream.Serialize(ref this._QuestIDs);
            stream.Serialize(ref this._CodexEntries);
            stream.Serialize(ref this._CodexIDs);
        }
        #endregion

        #region Properties
        public PlotTableWrapper Helpers
        {
            get { return this._Helpers; }
        }

        [Browsable(false)]
        public BitArray BoolVariables
        {
            get { return this._BoolVariables; }
        }

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

        public List<IntVariablePair> IntVariables
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

        public List<FloatVariablePair> FloatVariables
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

        public int QuestProgressCounter
        {
            get { return this._QuestProgressCounter; }
            set
            {
                if (value != this._QuestProgressCounter)
                {
                    this._QuestProgressCounter = value;
                    this.NotifyPropertyChanged("QuestProgressCounter");
                }
            }
        }

        public List<PlotQuest> QuestProgress
        {
            get { return this._QuestProgress; }
            set
            {
                if (value != this._QuestProgress)
                {
                    this._QuestProgress = value;
                    this.NotifyPropertyChanged("QuestProgress");
                }
            }
        }

        public List<int> QuestIDs
        {
            get { return this._QuestIDs; }
            set
            {
                if (value != this._QuestIDs)
                {
                    this._QuestIDs = value;
                    this.NotifyPropertyChanged("QuestIDs");
                }
            }
        }

        public List<PlotCodex> CodexEntries
        {
            get { return this._CodexEntries; }
            set
            {
                if (value != this._CodexEntries)
                {
                    this._CodexEntries = value;
                    this.NotifyPropertyChanged("CodexEntries");
                }
            }
        }

        public List<int> CodexIDs
        {
            get { return this._CodexIDs; }
            set
            {
                if (value != this._CodexIDs)
                {
                    this._CodexIDs = value;
                    this.NotifyPropertyChanged("CodexIDs");
                }
            }
        }
        #endregion

        #region PropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion

        #region Children
        [OriginalName("IntVariablePair")]
        public class IntVariablePair : IUnrealSerializable, INotifyPropertyChanged
        {
            #region Fields
            [OriginalName("Index")]
            private int _Index;

            [OriginalName("Value")]
            private int _Value;
            #endregion

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref this._Index);
                stream.Serialize(ref this._Value);
            }

            // for CollectionEditor
            [Browsable(false)]
            public string Name
            {
                get { return this._Index.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            }

            public override string ToString()
            {
                return this.Name ?? "(null)";
            }

            #region Properties
            public int Index
            {
                get { return this._Index; }
                set
                {
                    if (value != this._Index)
                    {
                        this._Index = value;
                        this.NotifyPropertyChanged("Index");
                    }
                }
            }

            public int Value
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

        [OriginalName("FloatVariablePair")]
        public class FloatVariablePair : IUnrealSerializable, INotifyPropertyChanged
        {
            #region Fields
            [OriginalName("Index")]
            private int _Index;

            [OriginalName("Value")]
            private float _Value;
            #endregion

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref this._Index);
                stream.Serialize(ref this._Value);
            }

            // for CollectionEditor
            [Browsable(false)]
            public string Name
            {
                get { return this._Index.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            }

            public override string ToString()
            {
                return this.Name ?? "(null)";
            }

            #region Properties
            public int Index
            {
                get { return this._Index; }
                set
                {
                    if (value != this._Index)
                    {
                        this._Index = value;
                        this.NotifyPropertyChanged("Index");
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

        [OriginalName("PlotQuest")]
        public class PlotQuest : IUnrealSerializable, INotifyPropertyChanged
        {
            #region Fields
            [OriginalName("QuestCounter")]
            private int _QuestCounter;

            [OriginalName("QuestUpdated")]
            private bool _QuestUpdated;

            [OriginalName("ActiveGoal")]
            private int _ActiveGoal;

            [OriginalName("History")]
            private List<int> _History = new List<int>();
            #endregion

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref this._QuestCounter);
                stream.Serialize(ref this._QuestUpdated);
                stream.Serialize(ref this._ActiveGoal, s => s.Version < 57, () => 0);
                stream.Serialize(ref this._History);
            }

            #region Properties
            public int QuestCounter
            {
                get { return this._QuestCounter; }
                set
                {
                    if (value != this._QuestCounter)
                    {
                        this._QuestCounter = value;
                        this.NotifyPropertyChanged("QuestCounter");
                    }
                }
            }

            public bool QuestUpdated
            {
                get { return this._QuestUpdated; }
                set
                {
                    if (value != this._QuestUpdated)
                    {
                        this._QuestUpdated = value;
                        this.NotifyPropertyChanged("QuestUpdated");
                    }
                }
            }

            public int ActiveGoal
            {
                get { return this._ActiveGoal; }
                set
                {
                    if (value != this._ActiveGoal)
                    {
                        this._ActiveGoal = value;
                        this.NotifyPropertyChanged("ActiveGoal");
                    }
                }
            }

            public List<int> History
            {
                get { return this._History; }
                set
                {
                    if (value != this._History)
                    {
                        this._History = value;
                        this.NotifyPropertyChanged("History");
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

        public class PlotCodex : IUnrealSerializable, INotifyPropertyChanged
        {
            #region Fields
            [OriginalName("Pages")]
            private List<PlotCodexPage> _Pages = new List<PlotCodexPage>();
            #endregion

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref this._Pages);
            }

            #region Properties
            public List<PlotCodexPage> Pages
            {
                get { return this._Pages; }
                set
                {
                    if (value != this._Pages)
                    {
                        this._Pages = value;
                        this.NotifyPropertyChanged("Pages");
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
            [OriginalName("PlotCodexPage")]
            public class PlotCodexPage : IUnrealSerializable, INotifyPropertyChanged
            {
                #region Fields
                [OriginalName("Page")]
                private int _Page;

                [OriginalName("bNew")]
                private bool _IsNew;
                #endregion

                public void Serialize(IUnrealStream stream)
                {
                    stream.Serialize(ref this._Page);
                    stream.Serialize(ref this._IsNew);
                }

                #region Properties
                public int Page
                {
                    get { return this._Page; }
                    set
                    {
                        if (value != this._Page)
                        {
                            this._Page = value;
                            this.NotifyPropertyChanged("Page");
                        }
                    }
                }

                public bool IsNew
                {
                    get { return this._IsNew; }
                    set
                    {
                        if (value != this._IsNew)
                        {
                            this._IsNew = value;
                            this.NotifyPropertyChanged("IsNew");
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
        #endregion
    }
}
