using ME3TweaksModManager.modmanager.save;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;
using ME3TweaksModManager.modmanager.save.game2.FileFormats.Save;

namespace ME3TweaksModManager.modmanager.save.le1
{
    // Ported from https://github.com/electronicarts/MELE_ModdingSupport/blob/master/DataFormats/ME1_Savegame.h
    // Todo: all of it still

    class LE1SaveFile
    {

        class SaveTimeStamp : IUnrealSerializable
        {
            int Seconds;
            int Day;
            int Month;
            int Year;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class PlotQuestSaveRecord : IUnrealSerializable
        {
            int QuestCounter;
            int bQuestUpdated;
            int[] History;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class PlotCodexPageSaveRecord : IUnrealSerializable
        {
            int Page;
            int bNew;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class PlotCodexSaveRecord : IUnrealSerializable
        {
            PlotCodexPageSaveRecord[] Pages;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class PlotTableSaveRecord : IUnrealSerializable
        {
            int[] BoolVariables;
            int[] IntVariables;
            float[] FloatVariables;
            int QuestProgressCounter;
            PlotQuestSaveRecord[] QuestProgress;
            int[] QuestIds;
            PlotCodexSaveRecord[] CodexEntries;
            int[] CodexIds;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class MorphFeatureSaveRecord : IUnrealSerializable
        {
            string Feature;
            float Offset;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class OffsetBoneSaveRecord : IUnrealSerializable
        {
            string Name;
            Vector Offset;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class ScalarParameterSaveRecord : IUnrealSerializable
        {
            string Name;
            float Value;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LinearColor : IUnrealSerializable
        {
            float R, G, B, A;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class VectorParameterSaveRecord : IUnrealSerializable
        {
            string Name;
            LinearColor Value;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class TextureParameterSaveRecord : IUnrealSerializable
        {
            string Name;
            string Value;

            public void Serialize(IUnrealStream stream)
            {

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

            }
        }
        class AppearanceSaveRecord : IUnrealSerializable
        {
            int bHasMorphHead;
            // Only serialized if bHasMorphHead is true
            MorphHeadSaveRecord MorphHead;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class SimpleTalentSaveRecord : IUnrealSerializable
        {
            int TalentID;
            int CurrentRank;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class ComplexTalentSaveRecord : IUnrealSerializable
        {
            int TalentID;
            int CurrentRank;
            int MaxRank;
            int LevelOffset;
            int LevelsPerRank;
            int VisualOrder;
            int[] PrereqIDs;
            int[] PrereqRanks;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class HotKeySaveRecord : IUnrealSerializable
        {
            int HotKeyPawn;
            int HotKeyEvent;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class PlayerRecord : IUnrealSerializable, IPlayerRecord
        {
            int bIsFemale;
            int PlayerClassName;
            byte PlayerClass;
            int Level;
            float CurrentXP;
            string FirstName;
            int LastName;
            byte Origin;
            byte Notoriety;
            int SpecializationBonusId;
            byte SpectreRank;
            int TalentPoints;
            int TalentPoolPoints;
            string MappedTalent;
            AppearanceSaveRecord Appearance;
            SimpleTalentSaveRecord[] SimpleTalents;
            ComplexTalentSaveRecord[] ComplexTalents;
            ItemSaveRecord[] Equipment;
            ItemSaveRecord[] Weapons;
            private ItemSaveRecord[] Items;
            ItemSaveRecord[] BuybackItems;
            int Credits;
            int Medigel;
            float Grenades;
            float Omnigel;
            string FaceCode;
            int bArmorOverridden;
            int AutoLevelUpTemplateID;
            float HealthPerLevel;
            float StabilityCurrent;
            byte Race;
            float ToxicCurrent;
            int Stamina;
            int Focus;
            int Precision;
            int Coordination;
            byte AttributePrimary;
            byte AttributeSecondary;
            float SkillCharm;
            float SkillIntimidate;
            float SkillHaggle;
            float HealthCurrent;
            float ShieldCurrent;
            int XPLevel;
            int bIsDriving;
            int[] GameOptions;
            int bHelmetShown;
            byte CurrentQuickSlot;
            byte LastQuickSlot;
            string LastPower;
            float HealthMax;
            HotKeySaveRecord[] HotKeys;
            string PrimaryWeapon;
            string SecondaryWeapon;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class ItemModSaveRecord : IUnrealSerializable
        {
            int ItemId;
            byte Sophistication;
            int Manufacturer;
            int PlotConditionalId;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class ItemSaveRecord : IUnrealSerializable
        {
            int ItemId;
            byte Sophistication;
            int Manufacturer;
            int PlotConditionalId;
            int bNewItem;
            int bJunkItem;
            ItemModSaveRecord[] XMods;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class HenchmanSaveRecord : IUnrealSerializable
        {
            string Tag;
            SimpleTalentSaveRecord[] SimpleTalents;
            ComplexTalentSaveRecord[] ComplexTalents;
            ItemSaveRecord[] Equipment;
            ItemSaveRecord[] Weapons;
            int TalentPoints;
            int TalentPoolPoints;
            int AutoLevelUpTemplateID;
            int LastName;
            int ClassName;
            byte ClassBase;
            float HealthPerLevel;
            float StabilityCurrent;
            byte Gender;
            byte Race;
            float ToxicCurrent;
            int Stamina;
            int Focus;
            int Precision;
            int Coordination;
            byte AttributePrimary;
            byte AttributeSecondary;
            float HealthCurrent;
            float ShieldCurrent;
            int XPLevel;
            int bHelmetShown;
            byte CurrentQuickSlot;
            float HealthMax;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LEGACY_BaseObjectSaveRecord : IUnrealSerializable
        {
            string OwnerName;
            int bHasOwnerClass;

            // Only serialized if bHasOwnerClass is true
            string OwnerClassName;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LEGACY_ActorSaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            Vector Location;
            Rotator Rotation;
            Vector Velocity;
            Vector Acceleration;
            int bScriptInitialized;
            int bHidden;
            int bStasis;
        };

        class LEGACY_BioPawnSaveRecord : LEGACY_ActorSaveRecord, IUnrealSerializable
        {
            float GrimeLevel;
            float GrimeDirtLevel;
            int TalkedToCount;
            int bHeadGearVisiblePreference;
        };

        class LEGACY_ActorBehaviorSaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            int bIsDead;
            int bGeneratedTreasure;
            int bChallengeScaled;
            int bHasOwner;
            // Only serialized if bHasOwner is true
            string ClassName;
            // Only serialized if bHasOwner is true, Owner is fully serialized into the actor behavior
            LEGACY_BaseObjectSaveRecord Owner;
        };

        class LEGACY_ArtPlaceableSaveRecord : LEGACY_ActorBehaviorSaveRecord, IUnrealSerializable
        {
            float Health;
            float CurrentHealth;
            int bEnabled;
            string CurrentFSMStateName;
            int bIsDestroyed;
            string State0;
            string State1;
            byte UseCase;
            int bUseCaseOverride;
            int bPlayerOnly;
            byte SkillDifficulty;
            int bHasInventory;
            // Only serialized if bHasInventory is true
            string ClassName;
            // Only serialized if bHasInventory is true, Inventory is fully serialized into the art placeable
            LEGACY_BaseObjectSaveRecord Inventory;
            int bSkilGameFailed;
            int bSkillGameXPAwarded;
        };

        class LEGACY_EpicPawnBehaviorSaveRecord : LEGACY_ActorBehaviorSaveRecord, IUnrealSerializable
        {
            float HealthCurrent;
            float ShieldCurrent;
            string FirstName;
            int LastName;
            float HealthMax;
            float HealthRegenRate;
            float RadarRange;
        };

        class LEGACY_SimpleTalentSaveRecord : IUnrealSerializable
        {
            int TalentId;
            int Ranks;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LEGACY_ComplexTalentSaveRecord : IUnrealSerializable
        {
            int TalentId;
            int Ranks;
            int MaxRank;
            int LevelOffset;
            int LevelsPerRank;
            int VisualOrder;
            int[] PrereqTalendIdArray;
            int[] PrereqTalentRankArray;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LEGACY_QuickSlotSaveRecord : IUnrealSerializable
        {
            int bHasQuickSlot;
            // Only serialized if bHasQuickSlot is true
            string ClassName;
            // Only serialized if bHasQuickSlot is true, quick slot item is fully serialized into the quickslot record
            LEGACY_BaseObjectSaveRecord Item;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LEGACY_EquipmentSaveRecord : IUnrealSerializable
        {
            int bHasEquipment;
            // Only serialized if bHasEquipment is true
            string ClassName;
            // Only serialized if bHasEquipment is true
            LEGACY_BaseObjectSaveRecord Item;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LEGACY_BioPawnBehaviorSaveRecord : LEGACY_EpicPawnBehaviorSaveRecord, IUnrealSerializable
        {
            int XPLevel;
            float HealthPerLevel;
            float StabilityCurrent;
            byte Gender;
            byte Race;
            float ToxicCurrent;
            int Stamina;
            int Focus;
            int Precision;
            int Coordination;
            byte QuickSlotCurrent;
            int bHasSquad;
            // Only serialized if bHasSquad is true
            string SquadObjectClassName;
            // Only serialized if bHasSquad is true    
            LEGACY_BaseObjectSaveRecord Squad;
            int bHasInventory;
            // Only serialized if bHasInventory is true
            string InventoryObjectClassName;


            // Only serialized if bHasInventory is true    
            LEGACY_BaseObjectSaveRecord Inventory;

            int Experience;
            int TalentPoints;
            int TalentPoolPoints;
            byte AttributePrimary;
            byte AttributeSecondary;
            byte ClassBase;
            int LocalizedClassName;
            int AutoLevelUpTemplateID;
            byte SpectreRank;
            byte BackgroundOrigin;
            byte BackgroundNotoriety;
            byte SpecializationBonusID;
            float SkillCharm;
            float SkillIntimidate;
            float SkillHaggle;
            float Audibility;
            float Blindness;
            float DamageDurationMult;
            float Deafness;
            int UnlootableGrenadeCount;
            int bHeadGearVisiblePreference;
            LEGACY_SimpleTalentSaveRecord[] SimpleTalents;
            LEGACY_ComplexTalentSaveRecord[] ComplexTalents;
            LEGACY_QuickSlotSaveRecord[] QuickSlots;
            LEGACY_EquipmentSaveRecord[] Equipment;
        };

        class LEGACY_ItemSaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            string ClassName;
            int Id;
            byte Sophistication;
            int Manufacturer;
            int PlotConditionalId;
        };

        class LEGACY_PlotItemSaveRecord : IUnrealSerializable
        {
            int LocalizedName;
            int LocalizedDesc;
            int ExportId;
            int BasePrice;
            int ShopGUIImageId;
            int PlotConditionalId;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LEGACY_ItemXModSaveRecord : LEGACY_ItemSaveRecord, IUnrealSerializable
        {
            int bHasMod;
            // Only serialized if bHasMod is true
            string ClassName;
            // Only serialized if bHasMod is true
            int Type;
        };

        class LEGACY_XModdableSlotSpecRecord : IUnrealSerializable
        {
            int Type;
            LEGACY_ItemXModSaveRecord[] XMods;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LEGACY_ItemXModdableSaveRecord : LEGACY_ItemSaveRecord, IUnrealSerializable
        {
            LEGACY_XModdableSlotSpecRecord[] SlotSpec;
        };

        class LEGACY_InventorySaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            LEGACY_ItemSaveRecord[] Items;
            LEGACY_PlotItemSaveRecord[] PlotItems;
            int Credits;
            int Grenades;
            float Medigel;
            float Salvage;
        };

        class LEGACY_BaseSquadSaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            int bHasInventory;
            // Only serialized if bHasInventory is true
            string ClassName;
            // Only serialized if bHasInventory is true, Inventory is fully serialized into the squad
            LEGACY_BaseObjectSaveRecord Inventory;
        };

        class LEGACY_BioSquadSaveRecord : LEGACY_BaseSquadSaveRecord, IUnrealSerializable
        {
            int SquadXP;
            int MaxLevel;
            int MinLevel;
            int SquadLevel;
        };

        class LEGACY_PlayerVehicleSaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            string ActorType;
            int bPowertrainEnabled;
            int bVehicleFunctionEnabled;
        };

        class LEGACY_ShopSaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            int LastPlayerLevel;
            int bIsInitialized;
            LEGACY_BaseObjectSaveRecord[] Inventory;
        };

        class LEGACY_WorldStreamingStateRecord : IUnrealSerializable
        {
            string Name;
            int bEnabled;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LEGACY_WorldSaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            LEGACY_WorldStreamingStateRecord[] StreamingStates;
            string DestinationAreaMap;
            Vector Destination;
            string CinematicsSeen;
            int[] ScannedClusters;
            int[] ScannedSystems;
            int[] ScannedPlanets;
            byte JournalSortMethod;
            int bJournalShowingMissions;
            int JournalLastSelectedMission;
            int JournalLastSelectedAssignment;
            int bCodexShowingPrimary;
            int CodexLastSelectedPrimary;
            int CodexLastSelectedSecondary;
            int CurrentTipID;
            int m_OverrideTip;
            byte[] BrowserAlerts; // SIZE 8
            int bHasLoot;
            // Only serialized if bHasLoot is true
            string ClassName;
            // Only serailized if bHasLoot is true, Item is fully serialized into the world save object
            LEGACY_BaseObjectSaveRecord PendingLoot;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LEGACY_LevelSaveRecord : IUnrealSerializable
        {
            string Name;
            LEGACY_BaseObjectSaveRecord[] LevelObjects;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LEGACY_MapSaveRecord : IUnrealSerializable
        {
            string[] Keys;
            LEGACY_LevelSaveRecord[] LevelData;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class VehicleSaveRecord : IUnrealSerializable
        {
            string FirstName;
            int LastName;
            float HealthCurrent;
            float ShieldCurrent;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
        class LE1Savegame : IUnrealSerializable
        {
            // If the top 16 bits have any value set (is not 0), the serialization should be byte swapped. In practice this shouldn't be the case for the released version of the game
            // Valid shipping ME1 save version should be 50
            int Version;

            string CharacterID;

            // When was the career created
            SaveTimeStamp CreatedDate;

            PlotTableSaveRecord PlotData;

            // When was the savegame created
            SaveTimeStamp TimeStamp;
            int SecondsPlayed;

            PlayerRecord PlayerData;

            string BaseLevelName;
            string MapName;
            string ParentMapName;

            Vector Location;
            Rotator Rotation;

            HenchmanSaveRecord[] HenchmanData;

            string DisplayName;
            string Filename;

            // The rest of the data in the savegame only serialized for normal savegames, *not* for character export
            // This legacy data is largely unused but is included here for completeness
            LEGACY_MapSaveRecord[] MapData;

            VehicleSaveRecord[] VehicleData;

            public void Serialize(IUnrealStream stream)
            {

            }
        }
    }
}
