using System;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;

namespace ME3TweaksModManager.modmanager.save.shared
{
    public interface ISaveFile : IUnrealSerializable
    {
        public MEGame Game { get; }
        public string SaveFilePath { get; set; }
        public DateTime Proxy_TimeStamp { get; }
        public string Proxy_DebugName { get; }
        public IPlayerRecord Proxy_PlayerRecord { get; }
        public string Proxy_BaseLevelName { get; }
        public ESFXSaveGameType SaveGameType { get; set; }
        public uint Version { get; }
        public int SaveNumber { get; set; }

        /// <summary>
        /// Returns the time played in the form '0h 1m' where 0 and 1 are numbers.
        /// </summary>
        public string Proxy_TimePlayed { get; }

        /// <summary>
        /// Returns the difficulty as a string
        /// </summary>
        public string Proxy_Difficulty { get; }

        /// <summary>
        /// Returns if the player is female
        /// </summary>
        public bool Proxy_IsFemale { get; }

        /// <summary>
        /// If save fully serialized and CRC check passed
        /// </summary>
        bool IsValid { get; set; }
    }
}
