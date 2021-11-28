using System.Collections.Generic;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats.Save
{
    // 00BAE380
    public class GalaxyMap : IUnrealSerializable
    {
        public List<Planet> Planets;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.Planets);
        }
    }
}
