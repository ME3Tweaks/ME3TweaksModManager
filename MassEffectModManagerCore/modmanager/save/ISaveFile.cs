using System;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace ME3TweaksModManager.modmanager.save
{
    public interface ISaveFile
    {
        public MEGame Game { get; }
        public string SaveFilePath { get; }
        public DateTime Proxy_TimeStamp { get; }
        public string Proxy_DebugName { get; }
        public IPlayerRecord Proxy_PlayerRecord { get; }
        public string Proxy_BaseLevelName { get; }
        public ESFXSaveGameType SaveGameType { get; }
        public uint Version { get; }
        public int SaveNumber { get; }
    }
}
