using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace MassEffectModManagerCore.modmanager.save
{
    public interface ISaveFile
    {
        public MEGame Game { get; }
        public string SaveFilePath { get; }
        public DateTime Proxy_TimeStamp { get; }
        public IPlayerRecord Proxy_PlayerRecord { get; }
        public string Proxy_BaseLevelName { get; }
        public ESFXSaveGameType SaveGameType { get; }
        public uint Version { get; }
        public int SaveNumber { get; }
    }
}
