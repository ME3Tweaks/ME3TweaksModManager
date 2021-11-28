using System.Collections.Generic;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats.Save
{
    // 00BAE400
    public class Planet : IUnrealSerializable
    {
        public int PlanetID; // +0
        public bool Visited; // +4
        public List<Vector2D> Probes; // +8

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.PlanetID);
            stream.Serialize(ref this.Visited);
            stream.Serialize(ref this.Probes);
        }
    }
}
