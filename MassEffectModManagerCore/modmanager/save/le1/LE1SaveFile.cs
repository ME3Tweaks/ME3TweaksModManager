using ME3TweaksModManager.modmanager.save;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksModManager.modmanager.save.game2.FileFormats.Save;

namespace ME3TweaksModManager.modmanager.save.le1
{
    // Ported from https://github.com/electronicarts/MELE_ModdingSupport/blob/master/DataFormats/ME1_Savegame.h
    // Todo: all of it still

    class LE1SaveFile
    {

        class SaveTimeStamp
        {
            int Seconds;
            int Day;
            int Month;
            int Year;
        };

        class PlotQuestSaveRecord
        {
            int QuestCounter;
            int bQuestUpdated;
            int[] History;
        };

        class PlotCodexPageSaveRecord
        {
            int Page;
            int bNew;
        };

        class PlotCodexSaveRecord
        {
            PlotCodexPageSaveRecord[] Pages;
        };

        class PlotTableSaveRecord
        {
            int[] BoolVariables;
            int[] IntVariables;
            float[] FloatVariables;
            int QuestProgressCounter;
            PlotQuestSaveRecord[] QuestProgress;
            int[] QuestIds;
            PlotCodexSaveRecord[] CodexEntries;
            int[] CodexIds;
        };

        class MorphFeatureSaveRecord
        {
            String Feature;
            float Offset;
        };

        class OffsetBoneSaveRecord
        {
            String Name;
            Vector Offset;
        };

        class ScalarParameterSaveRecord
        {
            String Name;
            float Value;
        };

        class LinearColor
        {
            float R, G, B, A;
        };

        class VectorParameterSaveRecord
        {
            String Name;
            LinearColor Value;
        };

        class TextureParameterSaveRecord
        {
            String Name;
            String Value;
        };

        class MorphHeadSaveRecord
        {
            String HairMesh;
            String[] AccessoryMeshes;
            MorphFeatureSaveRecord[] MorphFeatures;
            OffsetBoneSaveRecord[] OffsetBones;
            Vector[] Lod0Vertices;
            Vector[] Lod1Vertices;
            Vector[] Lod2Vertices;
            Vector[] Lod3Vertices;
            ScalarParameterSaveRecord[] ScalarParameters;
            VectorParameterSaveRecord[] VectorParameters;
            TextureParameterSaveRecord[] TextureParameters;
        };

        class AppearanceSaveRecord
        {
            int bHasMorphHead;
            // Only serialized if bHasMorphHead is true
            MorphHeadSaveRecord MorphHead;
        };

        class SimpleTalentSaveRecord
        {
            int TalentID;
            int CurrentRank;
        };

        class ComplexTalentSaveRecord
        {
            int TalentID;
            int CurrentRank;
            int MaxRank;
            int LevelOffset;
            int LevelsPerRank;
            int VisualOrder;
            int[] PrereqIDs;
            int[] PrereqRanks;
        };

        class HotKeySaveRecord
        {
            int HotKeyPawn;
            int HotKeyEvent;
        };

        class PlayerRecord
        {
            int bIsFemale;
            int PlayerClassName;
            byte PlayerClass;
            int Level;
            float CurrentXP;
            String FirstName;
            int LastName;
            byte Origin;
            byte Notoriety;
            int SpecializationBonusId;
            byte SpectreRank;
            int TalentPoints;
            int TalentPoolPoints;
            String MappedTalent;
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
            String FaceCode;
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
            String LastPower;
            float HealthMax;
            HotKeySaveRecord[] HotKeys;
            String PrimaryWeapon;
            String SecondaryWeapon;
        };

        class ItemModSaveRecord
        {
            int ItemId;
            byte Sophistication;
            int Manufacturer;
            int PlotConditionalId;
        };

        class ItemSaveRecord
        {
            int ItemId;
            byte Sophistication;
            int Manufacturer;
            int PlotConditionalId;
            int bNewItem;
            int bJunkItem;
            ItemModSaveRecord[] XMods;
        };

        class HenchmanSaveRecord
        {
            String Tag;
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
        };

        class LEGACY_BaseObjectSaveRecord
        {
            String OwnerName;
            int bHasOwnerClass;

            // Only serialized if bHasOwnerClass is true
            String OwnerClassName;
        };

        class LEGACY_ActorSaveRecord : LEGACY_BaseObjectSaveRecord
        {
            Vector Location;
            Rotator Rotation;
            Vector Velocity;
            Vector Acceleration;
            int bScriptInitialized;
            int bHidden;
            int bStasis;
        };

        class LEGACY_BioPawnSaveRecord : LEGACY_ActorSaveRecord
        {
            float GrimeLevel;
            float GrimeDirtLevel;
            int TalkedToCount;
            int bHeadGearVisiblePreference;
        };

        class LEGACY_ActorBehaviorSaveRecord : LEGACY_BaseObjectSaveRecord
        {
            int bIsDead;
            int bGeneratedTreasure;
            int bChallengeScaled;
            int bHasOwner;
            // Only serialized if bHasOwner is true
            String ClassName;
            // Only serialized if bHasOwner is true, Owner is fully serialized into the actor behavior
            LEGACY_BaseObjectSaveRecord Owner;
        };

        class LEGACY_ArtPlaceableSaveRecord : LEGACY_ActorBehaviorSaveRecord
        {
            float Health;
            float CurrentHealth;
            int bEnabled;
            String CurrentFSMStateName;
            int bIsDestroyed;
            String State0;
            String State1;
            byte UseCase;
            int bUseCaseOverride;
            int bPlayerOnly;
            byte SkillDifficulty;
            int bHasInventory;
            // Only serialized if bHasInventory is true
            String ClassName;
            // Only serialized if bHasInventory is true, Inventory is fully serialized into the art placeable
            LEGACY_BaseObjectSaveRecord Inventory;
            int bSkilGameFailed;
            int bSkillGameXPAwarded;
        };

        class LEGACY_EpicPawnBehaviorSaveRecord : LEGACY_ActorBehaviorSaveRecord
        {
            float HealthCurrent;
            float ShieldCurrent;
            String FirstName;
            int LastName;
            float HealthMax;
            float HealthRegenRate;
            float RadarRange;
        };

        class LEGACY_SimpleTalentSaveRecord
        {
            int TalentId;
            int Ranks;
        };

        class LEGACY_ComplexTalentSaveRecord
        {
            int TalentId;
            int Ranks;
            int MaxRank;
            int LevelOffset;
            int LevelsPerRank;
            int VisualOrder;
            int[] PrereqTalendIdArray;
            int[] PrereqTalentRankArray;
        };

        class LEGACY_QuickSlotSaveRecord
        {
            int bHasQuickSlot;
            // Only serialized if bHasQuickSlot is true
            String ClassName;
            // Only serialized if bHasQuickSlot is true, quick slot item is fully serialized into the quickslot record
            LEGACY_BaseObjectSaveRecord Item;
        };

        class LEGACY_EquipmentSaveRecord
        {
            int bHasEquipment;
            // Only serialized if bHasEquipment is true
            String ClassName;
            // Only serialized if bHasEquipment is true
            LEGACY_BaseObjectSaveRecord Item;
        };

        class LEGACY_BioPawnBehaviorSaveRecord : LEGACY_EpicPawnBehaviorSaveRecord
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
            String ClassName;
            // Only serialized if bHasSquad is true    
            LEGACY_BaseObjectSaveRecord Squad;
            int bHasInventory;
            // Only serialized if bHasInventory is true
            //String ClassName;
            
            
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

        class LEGACY_ItemSaveRecord : LEGACY_BaseObjectSaveRecord
        {
            String ClassName;
            int Id;
            byte Sophistication;
            int Manufacturer;
            int PlotConditionalId;
        };

        class LEGACY_PlotItemSaveRecord
        {
            int LocalizedName;
            int LocalizedDesc;
            int ExportId;
            int BasePrice;
            int ShopGUIImageId;
            int PlotConditionalId;
        };

        class LEGACY_ItemXModSaveRecord : LEGACY_ItemSaveRecord
        {
            int bHasMod;
            // Only serialized if bHasMod is true
            String ClassName;
            // Only serialized if bHasMod is true
            int Type;
        };

        class LEGACY_XModdableSlotSpecRecord
        {
            int Type;
            LEGACY_ItemXModSaveRecord[] XMods;
        };

        class LEGACY_ItemXModdableSaveRecord : LEGACY_ItemSaveRecord
        {
            LEGACY_XModdableSlotSpecRecord[] SlotSpec;
        };

        class LEGACY_InventorySaveRecord : LEGACY_BaseObjectSaveRecord
        {
            LEGACY_ItemSaveRecord[] Items;
            LEGACY_PlotItemSaveRecord[] PlotItems;
            int Credits;
            int Grenades;
            float Medigel;
            float Salvage;
        };

        class LEGACY_BaseSquadSaveRecord : LEGACY_BaseObjectSaveRecord
        {
            int bHasInventory;
            // Only serialized if bHasInventory is true
            String ClassName;
            // Only serialized if bHasInventory is true, Inventory is fully serialized into the squad
            LEGACY_BaseObjectSaveRecord Inventory;
        };

        class LEGACY_BioSquadSaveRecord : LEGACY_BaseSquadSaveRecord
        {
            int SquadXP;
            int MaxLevel;
            int MinLevel;
            int SquadLevel;
        };

        class LEGACY_PlayerVehicleSaveRecord : LEGACY_BaseObjectSaveRecord
        {
            String ActorType;
            int bPowertrainEnabled;
            int bVehicleFunctionEnabled;
        };

        class LEGACY_ShopSaveRecord : LEGACY_BaseObjectSaveRecord
        {
            int LastPlayerLevel;
            int bIsInitialized;
            LEGACY_BaseObjectSaveRecord[] Inventory;
        };

        class LEGACY_WorldStreamingStateRecord
        {
            String Name;
            int bEnabled;
        };

        class LEGACY_WorldSaveRecord : LEGACY_BaseObjectSaveRecord
        {
            LEGACY_WorldStreamingStateRecord[] StreamingStates;
            String DestinationAreaMap;
            Vector Destination;
            String CinematicsSeen;
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
            String ClassName;
            // Only serailized if bHasLoot is true, Item is fully serialized into the world save object
            LEGACY_BaseObjectSaveRecord PendingLoot;
        };

        class LEGACY_LevelSaveRecord
        {
            String Name;
            LEGACY_BaseObjectSaveRecord[] LevelObjects;
        };

        class LEGACY_MapSaveRecord
        {
            String[] Keys;
            LEGACY_LevelSaveRecord[] LevelData;
        };

        class VehicleSaveRecord
        {
            String FirstName;
            int LastName;
            float HealthCurrent;
            float ShieldCurrent;
        };

        class ME1Savegame
        {
            // If the top 16 bits have any value set (is not 0), the serialization should be byte swapped. In practice this shouldn't be the case for the released version of the game
            // Valid shipping ME1 save version should be 50
            int Version;

            String CharacterID;

            // When was the career created
            SaveTimeStamp CreatedDate;

            PlotTableSaveRecord PlotData;

            // When was the savegame created
            SaveTimeStamp TimeStamp;
            int SecondsPlayed;

            PlayerRecord PlayerData;

            String BaseLevelName;
            String MapName;
            String ParentMapName;

            Vector Location;
            Rotator Rotation;

            HenchmanSaveRecord[] HenchmanData;

            String DisplayName;
            String Filename;

            // The rest of the data in the savegame only serialized for normal savegames, *not* for character export
            // This legacy data is largely unused but is included here for completeness
            LEGACY_MapSaveRecord[] MapData;

            VehicleSaveRecord[] VehicleData;
        };
    }
}
