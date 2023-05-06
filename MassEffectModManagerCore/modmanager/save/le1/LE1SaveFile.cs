using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Save;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Save;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;
using ME3TweaksModManager.modmanager.save.game2.FileFormats.Save;
using ME3TweaksModManager.modmanager.save.shared;

namespace ME3TweaksModManager.modmanager.save.le1
{
    // Ported from https://github.com/electronicarts/MELE_ModdingSupport/blob/master/DataFormats/ME1_Savegame.h
    // Todo: all of it still

    class LE1SaveFile : ISaveFile
    {
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
            int QuestProgressCounter;
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
            string Value;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Name);
                stream.Serialize(ref Value);
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
            int bHasMorphHead;

            // Only serialized if bHasMorphHead is true
            MorphHeadSaveRecord MorphHead;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref bHasMorphHead);
                if (bHasMorphHead != 0)
                {
                    // Only serialized if bHasMorphHead is true
                    stream.Serialize(ref MorphHead);
                }
            }
        }

        class SimpleTalentSaveRecord : IUnrealSerializable
        {
            int TalentID;
            int CurrentRank;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref TalentID);
                stream.Serialize(ref CurrentRank);
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
                stream.Serialize(ref TalentID);
                stream.Serialize(ref CurrentRank);
                stream.Serialize(ref MaxRank);
                stream.Serialize(ref LevelOffset);
                stream.Serialize(ref LevelsPerRank);
                stream.Serialize(ref VisualOrder);
                stream.Serialize(ref PrereqIDs);
                stream.Serialize(ref PrereqRanks);
            }
        }

        class HotKeySaveRecord : IUnrealSerializable
        {
            int HotKeyPawn;
            int HotKeyEvent;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref HotKeyPawn);
                stream.Serialize(ref HotKeyEvent);
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
            ItemSaveRecord[] Items;
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
            public int[] GameOptions;
            int bHelmetShown;
            byte CurrentQuickSlot;
            int LastQuickSlot;
            string LastPower;
            float HealthMax;
            HotKeySaveRecord[] HotKeys;
            string PrimaryWeapon;
            string SecondaryWeapon;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref bIsFemale);
                stream.Serialize(ref PlayerClassName);
                stream.Serialize(ref PlayerClass);
                stream.Serialize(ref Level);
                stream.Serialize(ref CurrentXP);
                stream.Serialize(ref FirstName);
                stream.Serialize(ref LastName);
                stream.Serialize(ref Origin);
                stream.Serialize(ref Notoriety);
                stream.Serialize(ref SpecializationBonusId);
                stream.Serialize(ref SpectreRank);
                stream.Serialize(ref TalentPoints);
                stream.Serialize(ref TalentPoolPoints);
                stream.Serialize(ref MappedTalent);
                stream.Serialize(ref Appearance);
                stream.Serialize(ref SimpleTalents);
                stream.Serialize(ref ComplexTalents);
                stream.Serialize(ref Equipment);
                stream.Serialize(ref Weapons);
                stream.Serialize(ref Items);
                stream.Serialize(ref BuybackItems);
                stream.Serialize(ref Credits);
                stream.Serialize(ref Medigel);
                stream.Serialize(ref Grenades);
                stream.Serialize(ref Omnigel);
                stream.Serialize(ref FaceCode);
                stream.Serialize(ref bArmorOverridden);
                stream.Serialize(ref AutoLevelUpTemplateID);
                stream.Serialize(ref HealthPerLevel);
                stream.Serialize(ref StabilityCurrent);
                stream.Serialize(ref Race);
                stream.Serialize(ref ToxicCurrent);
                stream.Serialize(ref Stamina);
                stream.Serialize(ref Focus);
                stream.Serialize(ref Precision);
                stream.Serialize(ref Coordination);
                stream.Serialize(ref AttributePrimary);
                stream.Serialize(ref AttributeSecondary);
                stream.Serialize(ref SkillCharm);
                stream.Serialize(ref SkillIntimidate);
                stream.Serialize(ref SkillHaggle);
                stream.Serialize(ref HealthCurrent);
                stream.Serialize(ref ShieldCurrent);
                stream.Serialize(ref XPLevel);
                stream.Serialize(ref bIsDriving);
                stream.Serialize(ref GameOptions);
                stream.Serialize(ref bHelmetShown);
                stream.Serialize(ref CurrentQuickSlot);
                stream.Serialize(ref LastQuickSlot);
                stream.Serialize(ref LastPower);
                stream.Serialize(ref HealthMax);
                stream.Serialize(ref HotKeys);
                stream.Serialize(ref PrimaryWeapon);
                stream.Serialize(ref SecondaryWeapon);

            }

            public bool Proxy_IsFemale
            {
                get => bIsFemale != 0;
                set => bIsFemale = value ? 1 : 0;
            }

            public string Proxy_FirstName
            {
                get => FirstName;
                set => FirstName = value;
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
                stream.Serialize(ref ItemId);
                stream.Serialize(ref Sophistication);
                stream.Serialize(ref Manufacturer);
                stream.Serialize(ref PlotConditionalId);
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
                stream.Serialize(ref ItemId);
                stream.Serialize(ref Sophistication);
                stream.Serialize(ref Manufacturer);
                stream.Serialize(ref PlotConditionalId);
                stream.Serialize(ref bNewItem);
                stream.Serialize(ref bJunkItem);
                stream.Serialize(ref XMods);
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
                stream.Serialize(ref Tag);
                stream.Serialize(ref SimpleTalents);
                stream.Serialize(ref ComplexTalents);
                stream.Serialize(ref Equipment);
                stream.Serialize(ref Weapons);
                stream.Serialize(ref TalentPoints);
                stream.Serialize(ref TalentPoolPoints);
                stream.Serialize(ref AutoLevelUpTemplateID);
                stream.Serialize(ref LastName);
                stream.Serialize(ref ClassName);
                stream.Serialize(ref ClassBase);
                stream.Serialize(ref HealthPerLevel);
                stream.Serialize(ref StabilityCurrent);
                stream.Serialize(ref Gender);
                stream.Serialize(ref Race);
                stream.Serialize(ref ToxicCurrent);
                stream.Serialize(ref Stamina);
                stream.Serialize(ref Focus);
                stream.Serialize(ref Precision);
                stream.Serialize(ref Coordination);
                stream.Serialize(ref AttributePrimary);
                stream.Serialize(ref AttributeSecondary);
                stream.Serialize(ref HealthCurrent);
                stream.Serialize(ref ShieldCurrent);
                stream.Serialize(ref XPLevel);
                stream.Serialize(ref bHelmetShown);
                stream.Serialize(ref CurrentQuickSlot);
                stream.Serialize(ref HealthMax);
            }
        }

        class LEGACY_BaseObjectSaveRecord : IUnrealSerializable
        {
            string OwnerName;
            int bHasOwnerClass; // BioWare has this in their ME1SaveGame.h but that doesn't seem accurate

            // Only serialized if bHasOwnerClass is true
            string OwnerClassName;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref OwnerName);
                stream.Serialize(ref bHasOwnerClass);
                // Only serialized if bHasOwnerClass is true
                if (bHasOwnerClass != 0)
                {
                    stream.Serialize(ref OwnerClassName);
                }
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

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref Location);
                stream.Serialize(ref Rotation);
                stream.Serialize(ref Velocity);
                stream.Serialize(ref Acceleration);
                stream.Serialize(ref bScriptInitialized);
                stream.Serialize(ref bHidden);
                stream.Serialize(ref bStasis);
            }
        };

        class LEGACY_BioPawnSaveRecord : LEGACY_ActorSaveRecord, IUnrealSerializable
        {
            float GrimeLevel;
            float GrimeDirtLevel;
            int TalkedToCount;
            int bHeadGearVisiblePreference;

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref GrimeLevel);
                stream.Serialize(ref GrimeDirtLevel);
                stream.Serialize(ref TalkedToCount);
                stream.Serialize(ref bHeadGearVisiblePreference);
            }
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

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref bIsDead);
                stream.Serialize(ref bGeneratedTreasure);
                stream.Serialize(ref bChallengeScaled);
                stream.Serialize(ref bHasOwner);
                if (bHasOwner != 0)
                {
                    // Only serialized if bHasOwner is true
                    stream.Serialize(ref ClassName);
                    // Only serialized if bHasOwner is true, Owner is fully serialized into the actor behavior
                    stream.Serialize(ref Owner);
                }
            }
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

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref Health);
                stream.Serialize(ref CurrentHealth);
                stream.Serialize(ref bEnabled);
                stream.Serialize(ref CurrentFSMStateName);
                stream.Serialize(ref bIsDestroyed);
                stream.Serialize(ref State0);
                stream.Serialize(ref State1);
                stream.Serialize(ref UseCase);
                stream.Serialize(ref bUseCaseOverride);
                stream.Serialize(ref bPlayerOnly);
                stream.Serialize(ref SkillDifficulty);
                stream.Serialize(ref bHasInventory);
                if (bHasInventory != 0)
                {
                    // Only serialized if bHasInventory is true
                    stream.Serialize(ref ClassName);
                    // Only serialized if bHasInventory is true, Inventory is fully serialized into the art placeable
                    stream.Serialize(ref Inventory);
                }

                stream.Serialize(ref bSkilGameFailed);
                stream.Serialize(ref bSkillGameXPAwarded);
            }
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

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref HealthCurrent);
                stream.Serialize(ref ShieldCurrent);
                stream.Serialize(ref FirstName);
                stream.Serialize(ref LastName);
                stream.Serialize(ref HealthMax);
                stream.Serialize(ref HealthRegenRate);
                stream.Serialize(ref RadarRange);
            }
        };

        class LEGACY_SimpleTalentSaveRecord : IUnrealSerializable
        {
            int TalentId;
            int Ranks;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref TalentId);
                stream.Serialize(ref Ranks);
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
                stream.Serialize(ref TalentId);
                stream.Serialize(ref Ranks);
                stream.Serialize(ref MaxRank);
                stream.Serialize(ref LevelOffset);
                stream.Serialize(ref LevelsPerRank);
                stream.Serialize(ref VisualOrder);
                stream.Serialize(ref PrereqTalendIdArray);
                stream.Serialize(ref PrereqTalentRankArray);
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
                stream.Serialize(ref bHasQuickSlot);
                if (bHasQuickSlot != 0)
                {
                    // Only serialized if bHasQuickSlot is true
                    stream.Serialize(ref ClassName);
                    // Only serialized if bHasQuickSlot is true, quick slot item is fully serialized into the quickslot record
                    stream.Serialize(ref Item);

                }
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
                stream.Serialize(ref bHasEquipment);
                if (bHasEquipment != 0)
                {
                    // Only serialized if bHasEquipment is true
                    stream.Serialize(ref ClassName);
                    stream.Serialize(ref Item);
                }
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

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref XPLevel);
                stream.Serialize(ref HealthPerLevel);
                stream.Serialize(ref StabilityCurrent);
                stream.Serialize(ref Gender);
                stream.Serialize(ref Race);
                stream.Serialize(ref ToxicCurrent);
                stream.Serialize(ref Stamina);
                stream.Serialize(ref Focus);
                stream.Serialize(ref Precision);
                stream.Serialize(ref Coordination);
                stream.Serialize(ref QuickSlotCurrent);
                stream.Serialize(ref bHasSquad);
                if (bHasSquad != 0)
                {
                    // Only serialized if bHasSquad is true
                    stream.Serialize(ref SquadObjectClassName);
                    stream.Serialize(ref Squad);
                }

                stream.Serialize(ref bHasInventory);
                if (bHasInventory != 0)
                {
                    // Only serialized if bHasInventory is true
                    stream.Serialize(ref InventoryObjectClassName);
                    stream.Serialize(ref Inventory);
                }

                stream.Serialize(ref Experience);
                stream.Serialize(ref TalentPoints);
                stream.Serialize(ref TalentPoolPoints);
                stream.Serialize(ref AttributePrimary);
                stream.Serialize(ref AttributeSecondary);
                stream.Serialize(ref ClassBase);
                stream.Serialize(ref LocalizedClassName);
                stream.Serialize(ref AutoLevelUpTemplateID);
                stream.Serialize(ref SpectreRank);
                stream.Serialize(ref BackgroundOrigin);
                stream.Serialize(ref BackgroundNotoriety);
                stream.Serialize(ref SpecializationBonusID);
                stream.Serialize(ref SkillCharm);
                stream.Serialize(ref SkillIntimidate);
                stream.Serialize(ref SkillHaggle);
                stream.Serialize(ref Audibility);
                stream.Serialize(ref Blindness);
                stream.Serialize(ref DamageDurationMult);
                stream.Serialize(ref Deafness);
                stream.Serialize(ref UnlootableGrenadeCount);
                stream.Serialize(ref bHeadGearVisiblePreference);
                stream.Serialize(ref SimpleTalents);
                stream.Serialize(ref ComplexTalents);
                stream.Serialize(ref QuickSlots);
                stream.Serialize(ref Equipment);

            }
        };

        class LEGACY_ItemSaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            string ClassName;
            int Id;
            byte Sophistication;
            int Manufacturer;
            int PlotConditionalId;

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref ClassName);
                stream.Serialize(ref Id);
                stream.Serialize(ref Sophistication);
                stream.Serialize(ref Manufacturer);
                stream.Serialize(ref PlotConditionalId);
            }
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
                stream.Serialize(ref LocalizedName);
                stream.Serialize(ref LocalizedDesc);
                stream.Serialize(ref ExportId);
                stream.Serialize(ref BasePrice);
                stream.Serialize(ref ShopGUIImageId);
                stream.Serialize(ref PlotConditionalId);
            }
        }

        class LEGACY_ItemXModSaveRecord : LEGACY_ItemSaveRecord, IUnrealSerializable
        {
            int bHasMod;

            // Only serialized if bHasMod is true
            string ClassName;

            // Only serialized if bHasMod is true
            int Type;

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);

                stream.Serialize(ref bHasMod);
                if (bHasMod != 0)
                {
                    // Only serialized if bHasMod is true
                    stream.Serialize(ref ClassName);
                    // Only serialized if bHasMod is true
                    stream.Serialize(ref Type);
                }
            }
        };

        class LEGACY_XModdableSlotSpecRecord : IUnrealSerializable
        {
            int Type;
            LEGACY_ItemXModSaveRecord[] XMods;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref XMods);
            }
        }

        class LEGACY_ItemXModdableSaveRecord : LEGACY_ItemSaveRecord, IUnrealSerializable
        {
            LEGACY_XModdableSlotSpecRecord[] SlotSpec;

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref SlotSpec);
            }
        };

        class LEGACY_InventorySaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            LEGACY_ItemSaveRecord[] Items;
            LEGACY_PlotItemSaveRecord[] PlotItems;
            int Credits;
            int Grenades;
            float Medigel;
            float Salvage;

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref Items);
                stream.Serialize(ref PlotItems);
                stream.Serialize(ref Credits);
                stream.Serialize(ref Grenades);
                stream.Serialize(ref Medigel);
                stream.Serialize(ref Salvage);
            }
        };

        class LEGACY_BaseSquadSaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            int bHasInventory;

            // Only serialized if bHasInventory is true
            string ClassName;

            // Only serialized if bHasInventory is true, Inventory is fully serialized into the squad
            LEGACY_BaseObjectSaveRecord Inventory;

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref bHasInventory);
                if (bHasInventory != 0)
                {
                    // Only serialized if bHasInventory is true
                    stream.Serialize(ref ClassName);
                    // Only serialized if bHasInventory is true, Inventory is fully serialized into the squad
                    stream.Serialize(ref Inventory);
                }
            }
        };

        class LEGACY_BioSquadSaveRecord : LEGACY_BaseSquadSaveRecord, IUnrealSerializable
        {
            int SquadXP;
            int MaxLevel;
            int MinLevel;
            int SquadLevel;

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref SquadXP);
                stream.Serialize(ref MaxLevel);
                stream.Serialize(ref MinLevel);
                stream.Serialize(ref SquadLevel);
            }
        };

        class LEGACY_PlayerVehicleSaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            string ActorType;
            int bPowertrainEnabled;
            int bVehicleFunctionEnabled;

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref ActorType);
                stream.Serialize(ref bPowertrainEnabled);
                stream.Serialize(ref bVehicleFunctionEnabled);
            }
        };

        class LEGACY_ShopSaveRecord : LEGACY_BaseObjectSaveRecord, IUnrealSerializable
        {
            int LastPlayerLevel;
            int bIsInitialized;
            LEGACY_BaseObjectSaveRecord[] Inventory;

            public void Serialize(IUnrealStream stream)
            {
                base.Serialize(stream);
                stream.Serialize(ref LastPlayerLevel);
                stream.Serialize(ref bIsInitialized);
                stream.Serialize(ref Inventory);
            }
        };

        class LEGACY_WorldStreamingStateRecord : IUnrealSerializable
        {
            string Name;
            int bEnabled;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Name);
                stream.Serialize(ref bEnabled);
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
                base.Serialize(stream);
                stream.Serialize(ref StreamingStates);
                stream.Serialize(ref DestinationAreaMap);
                stream.Serialize(ref Destination);
                stream.Serialize(ref CinematicsSeen);
                stream.Serialize(ref ScannedClusters);
                stream.Serialize(ref ScannedSystems);
                stream.Serialize(ref ScannedPlanets);
                stream.Serialize(ref JournalSortMethod);
                stream.Serialize(ref bJournalShowingMissions);
                stream.Serialize(ref JournalLastSelectedMission);
                stream.Serialize(ref JournalLastSelectedAssignment);
                stream.Serialize(ref bCodexShowingPrimary);
                stream.Serialize(ref CodexLastSelectedPrimary);
                stream.Serialize(ref CodexLastSelectedSecondary);
                stream.Serialize(ref CurrentTipID);
                stream.Serialize(ref m_OverrideTip);
                stream.Serialize(ref BrowserAlerts); // SIZE 8
                stream.Serialize(ref bHasLoot);
                if (bHasLoot != 0)
                {
                    // Only serialized if bHasLoot is true
                    stream.Serialize(ref ClassName);
                    // Only serailized if bHasLoot is true, Item is fully serialized into the world save object
                    stream.Serialize(ref PendingLoot);
                }
            }
        }

        class LEGACY_LevelSaveRecord : IUnrealSerializable
        {
            string Name; // The name of the level object in memory (Level xxx:persistentlevel)
            LEGACY_WorldSaveRecord[] LevelObjects; // Objects stored within the level

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Name);
                Debug.WriteLine($"Read {Name} ending at 0x{stream.Stream.Position:X8}");
                stream.Serialize(ref LevelObjects);
                stream.Stream.ReadInt32(); // Not sure what this integer is...
            }
        }

        class HACK_LevelRecord : IUnrealSerializable
        {
            string Key;
            LEGACY_LevelSaveRecord[] LevelData;

            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref Key);
                stream.Serialize(ref LevelData);
            }
        }

        class LEGACY_MapSaveRecord : IUnrealSerializable
        {
            HACK_LevelRecord[] LevelRecords;

            //string[] Keys; // Map names (e.g. BIOA_PRO00)
            //LEGACY_LevelSaveRecord[] LevelData; // The list of level objects in the map (e.g. a DSG)

            public void Serialize(IUnrealStream stream)
            {
                // Special serialization
                stream.Serialize(ref LevelRecords);
                //stream.Serialize(ref Keys);
                //stream.Serialize(ref LevelData);

                //// This is a map of data
                //// We use parse the key and then choose the data type to deserialize it as

                //// Serialized as:
                //// INT COUNT
                //// for count
                ////     (4 BYTE LEN) STRING
                ////     LevelRecord
                //int count = 0;
                //stream.Serialize(ref count);

                //string recordType = null;
                //stream.Serialize(ref recordType);

                ////LEGACY_Leve
                ////if (stream.Loading)
                ////{

                ////}
                ////else
                ////{
                ////    stream.Serialize();
                ////}
                ////Debug.WriteLine("hi");
                ////stream.Serialize(ref Keys);
                ////stream.Serialize(ref LevelData);
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
                stream.Serialize(ref FirstName);
                stream.Serialize(ref LastName);
                stream.Serialize(ref HealthCurrent);
                stream.Serialize(ref ShieldCurrent);
            }
        }

        uint SaveFormatVersion;

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
        LEGACY_MapSaveRecord MapData;

        VehicleSaveRecord[] VehicleData;


        /// <summary>
        /// Used for save file compression
        /// </summary>
        private class ChunkHeader : IUnrealSerializable
        {
            public uint CompressedSize;
            public uint UncompressedSize;
            public void Serialize(IUnrealStream stream)
            {
                stream.Serialize(ref CompressedSize);
                stream.Serialize(ref UncompressedSize);
            }
        }

        public void Serialize(IUnrealStream stream)
        {
            if (stream.Loading)
            {
                // We must first decompress the save data
                var saveMagic = stream.Stream.ReadUInt32();
                if (saveMagic == 0x9E2A83C1)
                {
                    var blockSize = stream.Stream.ReadUInt32();
                    // Entire compressed size/uncompressed size
                    ChunkHeader fullHeader = new ChunkHeader();
                    fullHeader.Serialize(stream);
                    byte[] decompressed = new byte[fullHeader.UncompressedSize];
                    MemoryStream uncompressedSaveData = new MemoryStream(decompressed);

                    List<ChunkHeader> chunkHeaders = new();
                    while (true)
                    {
                        var chunk = new ChunkHeader();
                        chunk.Serialize(stream);
                        chunkHeaders.Add(chunk);
                        if (chunk.UncompressedSize < blockSize)
                            break; // Found final one
                    }

                    for (int i = 0; i < chunkHeaders.Count; i++)
                    {
                        var chunk = chunkHeaders[i];
                        byte[] decomp = new byte[chunk.UncompressedSize];
                        var result = Zlib.Decompress(stream.Stream.ReadToBuffer(chunk.CompressedSize), decomp);
                        if (result != chunk.UncompressedSize)
                            Debug.WriteLine(@"uh oh");
                        uncompressedSaveData.Write(decomp);
                    }

                    uncompressedSaveData.Position = 0;

#if DEBUG
                    // You can edit this to save the decompressed data to a file for testing.
                    //uncompressedSaveData.WriteToFile(@"B:\UserProfile\Documents\BioWare\Mass Effect Legendary Edition\Save\ME1\Jlock00\Jlock_00_01.decompressed");
#endif
                    stream = new UnrealStream(uncompressedSaveData, true, stream.Version);
                }
                else
                {
                    // File is decompressed already - don't know if game will support this, but whatever ;)
                    stream.Stream.Position -= 4;
                }
            }

            stream.Serialize(ref SaveFormatVersion);
            stream.Serialize(ref CharacterID);
            stream.Serialize(ref CreatedDate);
            stream.Serialize(ref PlotData);
            stream.Serialize(ref TimeStamp);
            stream.Serialize(ref SecondsPlayed);
            stream.Serialize(ref PlayerData);
            stream.Serialize(ref BaseLevelName);
            stream.Serialize(ref MapName);
            stream.Serialize(ref ParentMapName);
            stream.Serialize(ref Location);
            stream.Serialize(ref Rotation);
            stream.Serialize(ref HenchmanData);
            stream.Serialize(ref DisplayName);
            stream.Serialize(ref Filename);

            // This stuff doesn't work... will need to figure out why later
            // Debug.WriteLine($@"MapData begins at 0x{stream.Stream.Position:X8}");
            //stream.Serialize(ref MapData);
            //stream.Serialize(ref VehicleData);

            if (!stream.Loading)
            {
                // We must compress the stream data
            }
        }

        // ISaveFile for unified interface
        public MEGame Game => MEGame.LE1;
        public string SaveFilePath { get; set; }
        public DateTime Proxy_TimeStamp => TimeStamp.ToDate();
        public string Proxy_TimePlayed => MSaveShared.GetTimePlayed(SecondsPlayed);
        public string Proxy_Difficulty => MSaveShared.GetDifficultyString(PlayerData.GameOptions[0], MEGame.LE1);
        public bool Proxy_IsFemale => PlayerData?.Proxy_IsFemale ?? false;
        public string Proxy_DebugName => null; // LE1 does not support these
        public IPlayerRecord Proxy_PlayerRecord => PlayerData;
        public string Proxy_BaseLevelName => MapName ?? BaseLevelName; // We failover to BaseLevelName if unknown map is found like BIOA_CRD00
        public ESFXSaveGameType SaveGameType { get; set; }
        public uint Version => SaveFormatVersion;
        public int SaveNumber { get; set; }
        public bool IsValid { get; set; }
    }
}
