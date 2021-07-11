using System.Collections.Generic;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats.Save
{
    // 00BAEDD0
    public class PlotQuest : IUnrealSerializable
    {
        public uint QuestCounter; // +00
        public bool QuestUpdated; // +04
        public List<int> History; // +08

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.QuestCounter);
            stream.Serialize(ref this.QuestUpdated);
            stream.Serialize(ref this.History);
        }
    }
}
