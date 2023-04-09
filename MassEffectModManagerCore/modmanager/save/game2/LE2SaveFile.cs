using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Unreal;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;
using ME3TweaksModManager.modmanager.save.game2.FileFormats.Save;
using Microsoft.WindowsAPICodePack.Sensors;
using Microsoft.WindowsAPICodePack.Win32Native.NamedPipe;
using Vector = ME3TweaksModManager.modmanager.save.game2.FileFormats.Save.Vector;

namespace ME3TweaksModManager.modmanager.save.game2
{
    class LE2SaveFile : IUnrealSerializable, ISaveFile
    {
        // Based on EA repo ME2SaveGame.h
        // (c) 2021 Electronic Arts Inc.  All rights reserved.
        // 
        // FOR REFERENCE PURPOSES ONLY. 
        // Electronic Arts does not support or condone any specific mod for use with EA’s games. EA’s User Agreement applies and EA reserves all rights. Use mods with caution at your own risk.
        //
        // ME2 Savegame file format
        // 
        // All the data in these structs is laid out in the order in which data is serialized the structs are mainly just used as 
        // a way of organizing the data, and are not meant to indicate how the data is organized in memory
        // 
        // NOTES:
        //   - All arrays are serialized with a preceeding integer denoting the number of elements in the array
        //   - Footer for save file contains:
        //      - DWORD Checksum

        //        typedef unsigned char byte;
        //typedef int DWORD;

        //        class string { };
        //        class Vector { };
        //        class Rotator { };
        //        class Vector2D { };

        class SaveTimeStamp : IUnrealSerializable
        {
            int Seconds;
            int Day;
            int Month;
            int Year;
            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Seconds);
                stream.Serialize(ref Day);
                stream.Serialize(ref Month);
                stream.Serialize(ref Year);
            }
        };

        class Guid : IUnrealSerializable
        {
            int A;
            int B;
            int C;
            int D;
            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref A);
                stream.Serialize(ref B);
                stream.Serialize(ref C);
                stream.Serialize(ref D);
            }
        };

        class LevelSaveRecord : IUnrealSerializable
        {
            string LevelName;
            int bShouldBeLoaded;
            int bShouldBeVisible;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref LevelName);
                stream.Serialize(ref bShouldBeLoaded);
                stream.Serialize(ref bShouldBeVisible);
            }
        }
        class StreamingStateSaveRecord : IUnrealSerializable
        {
            string Name;
            int bActive;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Name);
                stream.Serialize(ref bActive);
            }
        }
        class KismetBoolSaveRecord : IUnrealSerializable
        {
            Guid BoolGUID;
            int bValue;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref BoolGUID);
                stream.Serialize(ref bValue);
            }
        }
        class DoorSaveRecord : IUnrealSerializable
        {
            Guid DoorGUID;
            byte CurrentState;
            byte OldState;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref DoorGUID);
                stream.Serialize(ref CurrentState);
                stream.Serialize(ref OldState);

            }
        }
        class MorphFeatureSaveRecord : IUnrealSerializable
        {
            string Feature;
            float Offset;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Feature);
                stream.Serialize(ref Offset);
            }
        }
        class OffsetBoneSaveRecord : IUnrealSerializable
        {
            string Name;
            Vector Offset;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Name);
                stream.Serialize(ref Offset);
            }
        }
        class ScalarParameterSaveRecord : IUnrealSerializable
        {
            string Name;
            float Value;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Name);
                stream.Serialize(ref Value);
            }
        }
        class LinearColor : IUnrealSerializable
        {
            float R;
            float G;
            float B;
            float A;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref R);
                stream.Serialize(ref G);
                stream.Serialize(ref B);
                stream.Serialize(ref A);
            }
        }
        class VectorParameterSaveRecord : IUnrealSerializable
        {
            string Name;
            LinearColor Value;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Name);
                stream.Serialize(ref Value);
            }
        }
        class TextureParameterSaveRecord : IUnrealSerializable
        {
            string Name;
            string Texture;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Name);
                stream.Serialize(ref Texture);
            }
        }
        class MorphHeadSaveRecord : IUnrealSerializable
        {
            string HairMesh;
            string[] AccessoryMeshes;
            MorphFeatureSaveRecord[] MorphFeatures;
            OffsetBoneSaveRecord[] OffsetBones;
            Vector[] Lod0Vertices;
            Vector[] Lod1Vertices;
            Vector[] Lod2Vertices;
            Vector[] Lod3Vertices;
            ScalarParameterSaveRecord[] ScalarParameters;
            VectorParameterSaveRecord[] VectorParameters;
            TextureParameterSaveRecord[] TextureParameters;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref HairMesh);
                stream.Serialize(ref AccessoryMeshes);
                stream.Serialize(ref MorphFeatures);
                stream.Serialize(ref OffsetBones);
                stream.Serialize(ref Lod0Vertices);
                stream.Serialize(ref Lod1Vertices);
                stream.Serialize(ref Lod2Vertices);
                stream.Serialize(ref Lod3Vertices);
                stream.Serialize(ref ScalarParameters);
                stream.Serialize(ref VectorParameters);
                stream.Serialize(ref TextureParameters);
            }
        }
        class AppearanceSaveRecord : IUnrealSerializable
        {
            byte CombatAppearance;
            int CasualID;
            int FullBodyID;
            int TorsoID;
            int ShoulderID;
            int ArmID;
            int LegID;
            int SpecID;
            int Tint1ID;
            int Tint2ID;
            int Tint3ID;
            int PatternID;
            int PatternColorID;
            int HelmetID;

            int bHasMorphHead;
            // Only serialized if bHasMorphHead is true
            MorphHeadSaveRecord MorphHead;


            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref CombatAppearance);
                stream.Serialize(ref CasualID);
                stream.Serialize(ref FullBodyID);
                stream.Serialize(ref TorsoID);
                stream.Serialize(ref ShoulderID);
                stream.Serialize(ref ArmID);
                stream.Serialize(ref LegID);
                stream.Serialize(ref SpecID);
                stream.Serialize(ref Tint1ID);
                stream.Serialize(ref Tint2ID);
                stream.Serialize(ref Tint3ID);
                stream.Serialize(ref PatternID);
                stream.Serialize(ref PatternColorID);
                stream.Serialize(ref HelmetID);

                stream.Serialize(ref bHasMorphHead);
                if (bHasMorphHead != 0)
                {
                    // Only serialized if bHasMorphHead is true
                    stream.Serialize(ref MorphHead);
                }
            }
        };

        class PowerSaveRecord : IUnrealSerializable
        {
            string PowerName;
            float CurrentRank;
            string PowerClassName;
            int WheelDisplayIndex;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref PowerName);
                stream.Serialize(ref CurrentRank);
                stream.Serialize(ref PowerClassName);
                stream.Serialize(ref WheelDisplayIndex);
            }
        }
        class WeaponSaveRecord : IUnrealSerializable
        {
            string WeaponClassName;
            int AmmoUsedCount;
            int TotalAmmo;
            int bLastWeapon;
            int bCurrentWeapon;
            string AmmoPowerName;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref WeaponClassName);
                stream.Serialize(ref AmmoUsedCount);
                stream.Serialize(ref TotalAmmo);
                stream.Serialize(ref bLastWeapon);
                stream.Serialize(ref bCurrentWeapon);
                stream.Serialize(ref AmmoPowerName);
            }
        }
        class HotKeySaveRecord : IUnrealSerializable
        {
            string PawnName;
            int PowerID;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref PawnName);
                stream.Serialize(ref PowerID);
            }
        }
        class ME1ImportBonusSaveRecord : IUnrealSerializable
        {
            int IMportedME1Level;
            int StartingME2Level;
            float BonusXP;
            float BonusCredits;
            float BonusResources;
            float BonusParagon;
            float BonusRenegade;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref IMportedME1Level);
                stream.Serialize(ref StartingME2Level);
                stream.Serialize(ref BonusXP);
                stream.Serialize(ref BonusCredits);
                stream.Serialize(ref BonusResources);
                stream.Serialize(ref BonusParagon);
                stream.Serialize(ref BonusRenegade);
            }
        }
        class PlayerRecord : IUnrealSerializable
        {
            int bIsFemale;
            string PlayerClassName;
            int Level;
            float CurrentXP;
            string FirstName;

            // stringref
            int LastName;

            byte Origin;
            byte Notoriety;

            int TalentPoints;
            string MappedPower1;
            string MappedPower2;
            string MappedPower3;

            AppearanceSaveRecord Appearance;

            PowerSaveRecord[] Powers;
            WeaponSaveRecord[] Weapons;
            string[] LoadoutWeapons; // 6 items
            HotKeySaveRecord[] HotKeys;

            int Credits;
            int Medigel;
            int Eezo;
            int Iridium;
            int Palladium;
            int Platinum;
            int Probes;
            float CurrentFuel;

            string FaceCode;
            // stringref
            int ClassFriendlyName;

            ME1ImportBonusSaveRecord ME1ImportBonuses;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref bIsFemale);
                stream.Serialize(ref PlayerClassName);
                stream.Serialize(ref Level);
                stream.Serialize(ref CurrentXP);
                stream.Serialize(ref FirstName);
                stream.Serialize(ref LastName);
                stream.Serialize(ref Origin);
                stream.Serialize(ref Notoriety);
                stream.Serialize(ref TalentPoints);
                stream.Serialize(ref MappedPower1);
                stream.Serialize(ref MappedPower2);
                stream.Serialize(ref MappedPower3);
                stream.Serialize(ref Appearance);
                stream.Serialize(ref Powers);
                stream.Serialize(ref Weapons);
                stream.Serialize(ref LoadoutWeapons, numValues: 6); // 6 items with no count
                stream.Serialize(ref HotKeys);
                stream.Serialize(ref Credits);
                stream.Serialize(ref Medigel);
                stream.Serialize(ref Eezo);
                stream.Serialize(ref Iridium);
                stream.Serialize(ref Palladium);
                stream.Serialize(ref Platinum);
                stream.Serialize(ref Probes);
                stream.Serialize(ref CurrentFuel);
                stream.Serialize(ref FaceCode);
                stream.Serialize(ref ClassFriendlyName);
                stream.Serialize(ref ME1ImportBonuses);
            }
        }
        class HenchmanSaveRecord : IUnrealSerializable
        {
            string Tag;
            PowerSaveRecord[] Powers;
            int CharacterLevel;
            int TalentPoints;
            string[] LoadoutWeapons; // 6 items
            string MappedPower;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Tag);
                stream.Serialize(ref Powers);
                stream.Serialize(ref CharacterLevel);
                stream.Serialize(ref TalentPoints);
                stream.Serialize(ref LoadoutWeapons, numValues: 6); // 6 items with no count
                stream.Serialize(ref MappedPower);
            }
        }
        class PlotQuestSaveRecord : IUnrealSerializable
        {
            int QuestCounter;
            int bQuestUpdated;
            int[] History;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref QuestCounter);
                stream.Serialize(ref bQuestUpdated);
                stream.Serialize(ref History);
            }
        }
        class PlotCodexPageSaveRecord : IUnrealSerializable
        {
            int Page;
            int bNew;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Page);
                stream.Serialize(ref bNew);
            }
        }
        class PlotCodexSaveRecord : IUnrealSerializable
        {
            PlotCodexPageSaveRecord[] Pages;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Pages);
            }
        }
        class PlotTableSaveRecord : IUnrealSerializable
        {
            int[] BoolVariables;
            int[] IntVariables;
            float[] FloatVariables;
            int[] QuestProgressCounter;
            PlotQuestSaveRecord[] QuestProgress;
            int[] QuestIds;
            PlotCodexSaveRecord[] CodexEntries;
            int[] CodexIds;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref BoolVariables);
                stream.Serialize(ref IntVariables);
                stream.Serialize(ref FloatVariables);
                stream.Serialize(ref QuestProgressCounter);
                stream.Serialize(ref QuestProgress);
                stream.Serialize(ref QuestIds);
                stream.Serialize(ref CodexEntries);
                stream.Serialize(ref CodexIds);
            }
        }
        class PlanetSaveRecord : IUnrealSerializable
        {
            int PlanetID;
            int bVisited;
            Vector2D[] Probes;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref PlanetID);
                stream.Serialize(ref bVisited);
                stream.Serialize(ref Probes);
            }
        }
        class GalaxyMapSaveRecord : IUnrealSerializable
        {
            PlanetSaveRecord[] Planets;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Planets);
            }
        }
        class ME1PlotTableRecord : IUnrealSerializable
        {
            int[] BoolVariables;
            int[] IntVariables;
            float[] FloatVariables;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref BoolVariables);
                stream.Serialize(ref IntVariables);
                stream.Serialize(ref FloatVariables);
            }
        }
        class DependentDLCRecord : IUnrealSerializable
        {
            int ModuleID;
            string Name;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref ModuleID);
                stream.Serialize(ref Name);
            }
        }


        // If the top 16 bits have any value set (is not 0), the serialization should be byte swapped. In practice this shouldn't be the case for the released version of the game
        // Valid shipping ME2 save version should be 30
        int SaveFormatVersion;

        string DebugName;
        float SecondsPlayed;
        int Disc;
        string BaseLevelName;
        byte Difficulty;

        // 0 - Not finished
        // 1 - Out in a blaze of glory
        // 2 - Lived to fight again
        int EndGameState;

        // When was the savegame created
        SaveTimeStamp TimeStamp;
        Vector SaveLocation;
        Rotator SaveRotation;
        int CurrentLoadingTip;

        LevelSaveRecord[] LevelRecords;
        StreamingStateSaveRecord[] StreamingRecords;
        KismetBoolSaveRecord[] KismetRecords;
        DoorSaveRecord[] DoorRecords;

        // Dead pawn guids
        Guid[] PawnRecords;

        PlayerRecord PlayerData;

        HenchmanSaveRecord[] HenchmanData;

        PlotTableSaveRecord PlotRecord;
        ME1PlotTableRecord ME1PlotRecord;

        GalaxyMapSaveRecord GalaxyMapRecord;

        DependentDLCRecord[] DependentDLC;
        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref SaveFormatVersion);
            stream.Serialize(ref DebugName);
            stream.Serialize(ref SecondsPlayed);
            stream.Serialize(ref Disc);
            stream.Serialize(ref BaseLevelName);
            stream.Serialize(ref Difficulty);
            stream.Serialize(ref EndGameState);
            stream.Serialize(ref TimeStamp);
            stream.Serialize(ref SaveLocation);
            stream.Serialize(ref SaveRotation);
            stream.Serialize(ref CurrentLoadingTip);
            stream.Serialize(ref LevelRecords);
            stream.Serialize(ref StreamingRecords);
            stream.Serialize(ref KismetRecords);
            stream.Serialize(ref DoorRecords);
            stream.Serialize(ref PawnRecords);
            stream.Serialize(ref PlayerData);
            stream.Serialize(ref HenchmanData);
            stream.Serialize(ref PlotRecord);
            stream.Serialize(ref ME1PlotRecord);
            stream.Serialize(ref GalaxyMapRecord);
            stream.Serialize(ref DependentDLC);
        }


        // ISaveFile for unified interface
        public MEGame Game => MEGame.LE2;
        public string SaveFilePath { get; set; }
        public DateTime Proxy_TimeStamp => DateTime.Now; // Todo: IMPLEMENT
        public string Proxy_DebugName => DebugName;
        public IPlayerRecord Proxy_PlayerRecord { get; }
        public string Proxy_BaseLevelName => BaseLevelName;
        public ESFXSaveGameType SaveGameType { get; set; }
        public uint Version => 30; // File save will always be version 30
        public int SaveNumber { get; set; }
    };
}
