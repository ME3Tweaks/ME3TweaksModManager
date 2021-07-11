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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using MassEffectModManagerCore.modmanager.save.game2.FileFormats;

namespace MassEffectModManagerCore.modmanager.save.game3
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [OriginalName("SFXSaveGame")]
    public class SFXSaveGameFile : IUnrealSerializable, INotifyPropertyChanged
    {
        private Endian _Endian;
        private uint _Version;
        private uint _Checksum;

        #region Fields
        [OriginalName("DebugName")]
        private string _DebugName;

        [OriginalName("SecondsPlayed")]
        private float _SecondsPlayed;

        [OriginalName("Disc")]
        private int _Disc;

        [OriginalName("BaseLevelName")]
        private string _BaseLevelName;

        [OriginalName("BaseLevelNameDisplayOverrideAsRead")]
        private string _BaseLevelNameDisplayOverrideAsRead;

        [OriginalName("Difficulty")]
        private DifficultyOptions _Difficulty;

        [OriginalName("EndGameState")]
        private EndGameState _EndGameState;

        [OriginalName("TimeStamp")]
        private SaveTimeStamp _TimeStamp = new SaveTimeStamp();

        [OriginalName("SaveLocation")]
        private Vector _Location = new Vector();

        [OriginalName("SaveRotation")]
        private Rotator _Rotation = new Rotator();

        [OriginalName("CurrentLoadingTip")]
        private int _CurrentLoadingTip;

        [OriginalName("LevelRecords")]
        private List<Level> _Levels = new List<Level>();

        [OriginalName("StreamingRecords")]
        private List<StreamingState> _StreamingRecords = new List<StreamingState>();

        [OriginalName("KismetRecords")]
        private List<KismetBool> _KismetRecords = new List<KismetBool>();

        [OriginalName("DoorRecords")]
        private List<Door> _Doors = new List<Door>();

        [OriginalName("PlaceableRecords")]
        private List<Placeable> _Placeables = new List<Placeable>();

        [OriginalName("PawnRecords")]
        private List<Guid> _Pawns = new List<Guid>();

        [OriginalName("PlayerRecord")]
        private Player _Player = new Player();

        [OriginalName("HenchmanRecords")]
        private List<Henchman> _Henchmen = new List<Henchman>();

        [OriginalName("PlotRecord")]
        private PlotTable _Plot = new PlotTable();

        [OriginalName("ME1PlotRecord")]
        private ME1PlotTable _Me1Plot = new ME1PlotTable();

        [OriginalName("PlayerVariableRecords")]
        private List<PlayerVariable> _PlayerVariables = new List<PlayerVariable>();

        [OriginalName("GalaxyMapRecord")]
        private GalaxyMap _GalaxyMap = new GalaxyMap();

        [OriginalName("DependentDLC")]
        private List<DependentDLC> _DependentDLC = new List<DependentDLC>();

        [OriginalName("TreasureRecords")]
        private List<LevelTreasure> _Treasures = new List<LevelTreasure>();

        [OriginalName("UseModuleRecords")]
        private List<Guid> _UseModules = new List<Guid>();

        [OriginalName("ConversationMode")]
        private AutoReplyModeOptions _ConversationMode;

        [OriginalName("ObjectiveMarkerRecords")]
        private List<ObjectiveMarker> _ObjectiveMarkers = new List<ObjectiveMarker>();

        [OriginalName("SavedObjectiveText")]
        private int _SavedObjectiveText;
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._DebugName);
            stream.Serialize(ref this._SecondsPlayed);
            stream.Serialize(ref this._Disc);
            stream.Serialize(ref this._BaseLevelName);
            stream.Serialize(ref this._BaseLevelNameDisplayOverrideAsRead, s => s.Version < 36, () => "None");
            stream.SerializeEnum(ref this._Difficulty);

            if (stream.Version >= 43 && stream.Version <= 46)
            {
                byte unknown = 0;
                stream.Serialize(ref unknown);
            }

            stream.SerializeEnum(ref this._EndGameState);
            stream.Serialize(ref this._TimeStamp);
            stream.Serialize(ref this._Location);
            stream.Serialize(ref this._Rotation);
            stream.Serialize(ref this._CurrentLoadingTip);
            stream.Serialize(ref this._Levels);
            stream.Serialize(ref this._StreamingRecords);
            stream.Serialize(ref this._KismetRecords);
            stream.Serialize(ref this._Doors);
            stream.Serialize(ref this._Placeables, s => s.Version < 46, () => new List<Placeable>());
            stream.Serialize(ref this._Pawns);
            stream.Serialize(ref this._Player);
            stream.Serialize(ref this._Henchmen);
            stream.Serialize(ref this._Plot);
            stream.Serialize(ref this._Me1Plot);
            stream.Serialize(ref this._PlayerVariables, s => s.Version < 34, () => new List<PlayerVariable>());
            stream.Serialize(ref this._GalaxyMap);
            stream.Serialize(ref this._DependentDLC);
            stream.Serialize(ref this._Treasures, s => s.Version < 35, () => new List<LevelTreasure>());
            stream.Serialize(ref this._UseModules, s => s.Version < 39, () => new List<Guid>());
            stream.SerializeEnum(ref this._ConversationMode,
                                 s => s.Version < 49,
                                 () => AutoReplyModeOptions.AllDecisions);
            stream.Serialize(ref this._ObjectiveMarkers, s => s.Version < 52, () => new List<ObjectiveMarker>());
            stream.Serialize(ref this._SavedObjectiveText, s => s.Version < 52, () => 0);
        }

        #region Properties
        [Browsable(false)]
        public Endian Endian
        {
            get { return this._Endian; }
            set
            {
                if (value != this._Endian)
                {
                    this._Endian = value;
                    this.NotifyPropertyChanged("Endian");
                }
            }
        }

        [Browsable(false)]
        public uint Version
        {
            get { return this._Version; }
            set
            {
                if (value != this._Version)
                {
                    this._Version = value;
                    this.NotifyPropertyChanged("Version");
                }
            }
        }

        [Browsable(false)]
        public uint Checksum
        {
            get { return this._Checksum; }
            set
            {
                if (value != this._Checksum)
                {
                    this._Checksum = value;
                    this.NotifyPropertyChanged("Checksum");
                }
            }
        }

        private static class Categories
        {
            public const string Basic = "Basic";
            public const string Location = "Location";
            public const string Squad = "Squad";
            public const string Plot = "Plot";
            public const string Uncategorized = "Uncategorized";
        }

        public string DebugName
        {
            get { return this._DebugName; }
            set
            {
                if (value != this._DebugName)
                {
                    this._DebugName = value;
                    this.NotifyPropertyChanged("DebugName");
                }
            }
        }

        public float SecondsPlayed
        {
            get { return this._SecondsPlayed; }
            set
            {
                if (Equals(value, this._SecondsPlayed) == false)
                {
                    this._SecondsPlayed = value;
                    this.NotifyPropertyChanged("SecondsPlayed");
                }
            }
        }

        public int Disc
        {
            get { return this._Disc; }
            set
            {
                if (value != this._Disc)
                {
                    this._Disc = value;
                    this.NotifyPropertyChanged("Disc");
                }
            }
        }

        public string BaseLevelName
        {
            get { return this._BaseLevelName; }
            set
            {
                if (value != this._BaseLevelName)
                {
                    this._BaseLevelName = value;
                    this.NotifyPropertyChanged("BaseLevelName");
                }
            }
        }

        public string BaseLevelNameDisplayOverrideAsRead
        {
            get { return this._BaseLevelNameDisplayOverrideAsRead; }
            set
            {
                if (value != this._BaseLevelNameDisplayOverrideAsRead)
                {
                    this._BaseLevelNameDisplayOverrideAsRead = value;
                    this.NotifyPropertyChanged("BaseLevelNameDisplayOverrideAsRead");
                }
            }
        }

        public DifficultyOptions Difficulty
        {
            get { return this._Difficulty; }
            set
            {
                if (value != this._Difficulty)
                {
                    this._Difficulty = value;
                    this.NotifyPropertyChanged("Difficulty");
                }
            }
        }

        [Description(
            "Note: this value was re-used from Mass Effect 2, and the value of 'LivedToFightAgain' is what indicates that the save can be imported. It has nothing to do with your ending of Mass Effect 3."
            )]
        public EndGameState EndGameState
        {
            get { return this._EndGameState; }
            set
            {
                if (value != this._EndGameState)
                {
                    this._EndGameState = value;
                    this.NotifyPropertyChanged("EndGameState");
                }
            }
        }

        public SaveTimeStamp TimeStamp
        {
            get { return this._TimeStamp; }
            set
            {
                if (value != this._TimeStamp)
                {
                    this._TimeStamp = value;
                    this.NotifyPropertyChanged("TimeStamp");
                }
            }
        }

        public Vector Location
        {
            get { return this._Location; }
            set
            {
                if (value != this._Location)
                {
                    this._Location = value;
                    this.NotifyPropertyChanged("Location");
                }
            }
        }

        public Rotator Rotation
        {
            get { return this._Rotation; }
            set
            {
                if (value != this._Rotation)
                {
                    this._Rotation = value;
                    this.NotifyPropertyChanged("Rotation");
                }
            }
        }

        public int CurrentLoadingTip
        {
            get { return this._CurrentLoadingTip; }
            set
            {
                if (value != this._CurrentLoadingTip)
                {
                    this._CurrentLoadingTip = value;
                    this.NotifyPropertyChanged("CurrentLoadingTip");
                }
            }
        }

        public List<Level> Levels
        {
            get { return this._Levels; }
            set
            {
                if (value != this._Levels)
                {
                    this._Levels = value;
                    this.NotifyPropertyChanged("Levels");
                }
            }
        }

        public List<StreamingState> StreamingRecords
        {
            get { return this._StreamingRecords; }
            set
            {
                if (value != this._StreamingRecords)
                {
                    this._StreamingRecords = value;
                    this.NotifyPropertyChanged("StreamingRecords");
                }
            }
        }

        public List<KismetBool> KismetRecords
        {
            get { return this._KismetRecords; }
            set
            {
                if (value != this._KismetRecords)
                {
                    this._KismetRecords = value;
                    this.NotifyPropertyChanged("KismetRecords");
                }
            }
        }

        public List<Door> Doors
        {
            get { return this._Doors; }
            set
            {
                if (value != this._Doors)
                {
                    this._Doors = value;
                    this.NotifyPropertyChanged("Doors");
                }
            }
        }

        public List<Placeable> Placeables
        {
            get { return this._Placeables; }
            set
            {
                if (value != this._Placeables)
                {
                    this._Placeables = value;
                    this.NotifyPropertyChanged("Placeables");
                }
            }
        }

        public List<Guid> Pawns
        {
            get { return this._Pawns; }
            set
            {
                if (value != this._Pawns)
                {
                    this._Pawns = value;
                    this.NotifyPropertyChanged("Pawns");
                }
            }
        }

        public Player Player
        {
            get { return this._Player; }
            set
            {
                if (value != this._Player)
                {
                    this._Player = value;
                    this.NotifyPropertyChanged("Player");
                }
            }
        }

        public List<Henchman> Henchmen
        {
            get { return this._Henchmen; }
            set
            {
                if (value != this._Henchmen)
                {
                    this._Henchmen = value;
                    this.NotifyPropertyChanged("Henchmen");
                }
            }
        }

        public PlotTable Plot
        {
            get { return this._Plot; }
            set
            {
                if (value != this._Plot)
                {
                    this._Plot = value;
                    this.NotifyPropertyChanged("Plot");
                }
            }
        }

        [Browsable(false)]
        // ReSharper disable InconsistentNaming
        public ME1PlotTable ME1Plot
        // ReSharper restore InconsistentNaming
        {
            get { return this._Me1Plot; }
            set
            {
                if (value != this._Me1Plot)
                {
                    this._Me1Plot = value;
                    this.NotifyPropertyChanged("ME1Plot");
                }
            }
        }

        public List<PlayerVariable> PlayerVariables
        {
            get { return this._PlayerVariables; }
            set
            {
                if (value != this._PlayerVariables)
                {
                    this._PlayerVariables = value;
                    this.NotifyPropertyChanged("PlayerVariables");
                }
            }
        }

        public GalaxyMap GalaxyMap
        {
            get { return this._GalaxyMap; }
            set
            {
                if (value != this._GalaxyMap)
                {
                    this._GalaxyMap = value;
                    this.NotifyPropertyChanged("GalaxyMap");
                }
            }
        }

        public List<DependentDLC> DependentDLC
        {
            get { return this._DependentDLC; }
            set
            {
                if (value != this._DependentDLC)
                {
                    this._DependentDLC = value;
                    this.NotifyPropertyChanged("DependentDLC");
                }
            }
        }

        public List<LevelTreasure> Treasures
        {
            get { return this._Treasures; }
            set
            {
                if (value != this._Treasures)
                {
                    this._Treasures = value;
                    this.NotifyPropertyChanged("Treasures");
                }
            }
        }

        public List<Guid> UseModules
        {
            get { return this._UseModules; }
            set
            {
                if (value != this._UseModules)
                {
                    this._UseModules = value;
                    this.NotifyPropertyChanged("UseModules");
                }
            }
        }


        public AutoReplyModeOptions ConversationMode
        {
            get { return this._ConversationMode; }
            set
            {
                if (value != this._ConversationMode)
                {
                    this._ConversationMode = value;
                    this.NotifyPropertyChanged("ConversationMode");
                }
            }
        }

        public List<ObjectiveMarker> ObjectiveMarkers
        {
            get { return this._ObjectiveMarkers; }
            set
            {
                if (value != this._ObjectiveMarkers)
                {
                    this._ObjectiveMarkers = value;
                    this.NotifyPropertyChanged("ObjectiveMarkers");
                }
            }
        }

        public int SavedObjectiveText
        {
            get { return this._SavedObjectiveText; }
            set
            {
                if (value != this._SavedObjectiveText)
                {
                    this._SavedObjectiveText = value;
                    this.NotifyPropertyChanged("SavedObjectiveText");
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

        public static SFXSaveGameFile Read(Stream input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            var save = new SFXSaveGameFile()
            {
                _Version = input.ReadUInt32()
            };

            if (save._Version != 29 && save._Version.Swap() != 29 &&
                save._Version != 59 && save._Version.Swap() != 59)
            {
                throw new FormatException("unexpected version");
            }
            var endian = save._Version == 29 || save._Version == 59
                             ? Endian.Little
                             : Endian.Big;
            if (endian == Endian.Big)
            {
                save._Version = save._Version.Swap();
            }

            var reader = new UnrealStream(input, true, save._Version);
            save.Serialize(reader);

            if (save._Version >= 27)
            {
                if (input.Position != input.Length - 4)
                {
                    throw new FormatException("bad checksum position");
                }

                save._Checksum = input.ReadUInt32();
            }

            if (input.Position != input.Length)
            {
                throw new FormatException("did not consume entire file");
            }

            save.Endian = endian;
            return save;
        }

        public static void Write(SFXSaveGameFile save, Stream output)
        {
            if (save == null)
            {
                throw new ArgumentNullException("save");
            }

            if (output == null)
            {
                throw new ArgumentNullException("output");
            }

            using (var memory = new MemoryStream())
            {
                memory.WriteUInt32(save.Version);

                var writer = new UnrealStream(memory, false, save._Version);
                save.Serialize(writer);

                if (save._Version >= 27)
                {
                    memory.Position = 0;
                    uint checksum = 0;

                    var buffer = new byte[1024];
                    while (memory.Position < memory.Length)
                    {
                        int read = memory.Read(buffer, 0, 1024);
                        checksum = Crc32.Compute(buffer, 0, read, checksum);
                    }

                    save._Checksum = checksum;
                    memory.WriteUInt32(checksum);
                }

                memory.Position = 0;
                output.WriteFromStream(memory, memory.Length);
            }
        }
    }
}
