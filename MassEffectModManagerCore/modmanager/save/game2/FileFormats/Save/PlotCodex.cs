using System.Collections.Generic;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats.Save
{
    // 00BAEED0
    public class PlotCodex : IUnrealSerializable
    {
        public List<PlotCodexPage> Pages;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize<PlotCodexPage>(ref this.Pages);
        }
    }
}
